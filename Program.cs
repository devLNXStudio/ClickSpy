namespace ClickSpy
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    /// <summary>
    /// App Entry point.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }

    /// <summary>
    /// Capture mode enum.
    /// </summary>
    public enum CaptureMode
    {
        Monitor,
        Window
    }

    /// <summary>
    /// Live long (for the app) and notify (tray).
    /// </summary>
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;

        private static readonly ConcurrentQueue<Point> s_clickQueue = new ConcurrentQueue<Point>();
        public static CaptureMode CurrentCaptureMode { get; private set; } = CaptureMode.Monitor;

        public TrayApplicationContext()
        {
            Directory.CreateDirectory("screenshots");
            EventLogger.Log("Aplikacja uruchomiona.");

            var contextMenu = new ContextMenuStrip();
            var monitorMenuItem = new ToolStripMenuItem("Mode: Screen", null, SwitchMode) { Checked = true, CheckOnClick = true };
            var windowMenuItem = new ToolStripMenuItem("Mode: Window", null, SwitchMode) { CheckOnClick = true };
            contextMenu.Items.Add(monitorMenuItem);
            contextMenu.Items.Add(windowMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, Exit));

            _trayIcon = new NotifyIcon()
            {
                Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("ClickSpy.spy-icon.ico")), // Use a default icon
                ContextMenuStrip = contextMenu, 
                Visible = true,
                Text = "Activity Log"
            };

            _cancellationTokenSource = new CancellationTokenSource();
            MouseHook.Start(s_clickQueue);
            _processingTask = Task.Run(() => ProcessClickQueue(_cancellationTokenSource.Token));
        }

        private void SwitchMode(object sender, EventArgs e)
        {
            var clickedItem = (ToolStripMenuItem)sender;

            // Zapewnienie, ¿e tylko jedna opcja jest zaznaczona (radio button behavior)
            var monitorItem = (ToolStripMenuItem)_trayIcon.ContextMenuStrip.Items[0];
            var windowItem = (ToolStripMenuItem)_trayIcon.ContextMenuStrip.Items[1];

            if (clickedItem == monitorItem)
            {
                CurrentCaptureMode = CaptureMode.Monitor;
                windowItem.Checked = false;
                EventLogger.Log("Switched: Screen.");
            }
            else if (clickedItem == windowItem)
            {
                CurrentCaptureMode = CaptureMode.Window;
                monitorItem.Checked = false;
                EventLogger.Log("Switched: Window.");
            }
            // Upewnij siê, ¿e klikniêty element jest zaznaczony
            clickedItem.Checked = true;
        }

        private void Exit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _cancellationTokenSource.Cancel();
            MouseHook.Stop();
            EventLogger.Log("App Stopped.");
            Application.Exit();
        }

        private async Task ProcessClickQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (s_clickQueue.TryDequeue(out Point clickPoint))
                {
                    try
                    {
                        ScreenCapture.CaptureAndLog(clickPoint, CurrentCaptureMode);
                    }
                    catch (Exception ex)
                    {
                        EventLogger.Log($"[ERROR] Error while screenshoting: {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(100, token);
                }
            }
        }
    }

    /// <summary>
    /// Screenshot class responsible for capturing the screen or window based on the click point and mode.
    /// </summary>
    public static class ScreenCapture
    {
        public static void CaptureAndLog(Point clickPoint, CaptureMode mode)
        {
            Rectangle bounds;
            string captureTargetInfo;

            if (mode == CaptureMode.Window)
            {
                IntPtr windowHandle = WinApi.WindowFromPoint(clickPoint);
                windowHandle = WinApi.GetAncestor(windowHandle, WinApi.GA_ROOT);

                WinApi.GetWindowRect(windowHandle, out WinApi.RECT windowRect);
                bounds = windowRect.ToRectangle();
                captureTargetInfo = $"Okno '{WinApi.GetWindowText(windowHandle)}'";
            }
            else
            {
                IntPtr monitorHandle = WinApi.MonitorFromPoint(clickPoint, WinApi.MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new WinApi.MONITORINFOEX();
                monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                WinApi.GetMonitorInfo(monitorHandle, ref monitorInfo);
                bounds = monitorInfo.rcMonitor.ToRectangle();
                captureTargetInfo = $"Screen '{new string(monitorInfo.szDevice).TrimEnd('\0')}'";
            }

            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            WinApi.GetWindowThreadProcessId(WinApi.WindowFromPoint(clickPoint), out uint processId);
            string processName = "N/A";
            try { processName = Process.GetProcessById((int)processId).ProcessName; } catch { /* Ignoruj */ }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string fileName = $"capture_{timestamp}.png";
            string filePath = Path.Combine("screenshots", fileName);

            using (var bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }
                bmp.Save(filePath, ImageFormat.Png);
            }

            EventLogger.Log($"Clicked: {clickPoint.X},{clickPoint.Y}. Process: {processName}. Target: {captureTargetInfo}. Screenshot: {filePath}");
        }
    }

    /// <summary>
    /// Log class responsible for writing events to a log file.
    /// </summary>
    public static class EventLogger
    {
        private static readonly string s_logFilePath = "log.txt";
        private static readonly object s_lock = new object();

        public static void Log(string message)
        {
            try
            {
                lock (s_lock)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}{Environment.NewLine}";
                    File.AppendAllText(s_logFilePath, logEntry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Can't write a log file: {ex.Message}");
            }
        }
    }


    /// <summary>
    /// MouseHook class responsible for capturing mouse clicks globally.
    /// </summary>
    public static class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        private static IntPtr s_hookID = IntPtr.Zero;
        private static WinApi.LowLevelMouseProc s_proc;
        private static ConcurrentQueue<Point> s_queue;

        public static void Start(ConcurrentQueue<Point> queue)
        {
            s_queue = queue;
            s_proc = HookCallback;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                s_hookID = WinApi.SetWindowsHookEx(WH_MOUSE_LL, s_proc, WinApi.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public static void Stop()
        {
            if (s_hookID != IntPtr.Zero)
            {
                WinApi.UnhookWindowsHookEx(s_hookID);
                s_hookID = IntPtr.Zero;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                var hookStruct = (WinApi.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(WinApi.MSLLHOOKSTRUCT));
                s_queue.Enqueue(new Point(hookStruct.pt.x, hookStruct.pt.y));
            }
            return WinApi.CallNextHookEx(s_hookID, nCode, wParam, lParam);
        }
    }

    /// <summary>
    /// Win32Api imports and structures used for mouse hooks and screen/window information retrieval.
    /// </summary>
    internal static class WinApi
    {
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point p);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static string GetWindowText(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;
            StringBuilder builder = new StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const uint GA_ROOT = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }



        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; public int Top; public int Right; public int Bottom;
            public Rectangle ToRectangle() => new Rectangle(Left, Top, Right - Left, Bottom - Top);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
    }


}