using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms.Integration;
using Microsoft.Win32;

namespace ScrapbookSaver;

internal static class WallpaperStartup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ScrapbookWallpaper";
    private static string Command => $"\"{Environment.ProcessPath}\" --startup";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(Key); return string.Equals(key?.GetValue(Name) as string, Command, StringComparison.OrdinalIgnoreCase); }
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue(Name, Command);
        else key.DeleteValue(Name, false);
    }
}

internal static class WallpaperApplication
{
    public static void Run(string[] args)
    {
        if (args.Length == 2 && args[0] == "--diagnose-shell")
        {
            File.WriteAllText(args[1], DesktopHost.Describe());
            return;
        }
        using var mutex = new Mutex(true, @"Local\ScrapbookWallpaper", out var first);
        if (!first) return;
        using var context = new WallpaperContext();
        if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase)) context.OpenSettings();
        if (args.Length == 2 && args[0] == "--smoke-test") context.StartSmokeTest(args[1]);
        Application.Run(context);
    }
}

internal sealed class WallpaperContext : ApplicationContext
{
    private readonly List<WallpaperForm> windows = [];
    private readonly NotifyIcon tray;
    private readonly System.Windows.Forms.Timer monitor = new() { Interval = 3000 };
    private readonly ToolStripMenuItem pause = new("Pause") { CheckOnClick = true };
    private IntPtr desktop;
    private string layout = "";
    private SettingsWindow? settings;
    private bool exiting;
    private bool locked;
    private System.Windows.Forms.Timer? smoke;

    public WallpaperContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(pause);
        pause.CheckedChanged += (_, _) => Rebuild();
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = AppArtwork.Icon, Text = "Scrapbook Live Wallpaper", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => OpenSettings();
        monitor.Tick += (_, _) =>
        {
            var nextLayout = string.Join(";", Screen.AllScreens.Select(s => s.Bounds.ToString()));
            if (desktop == IntPtr.Zero || !DesktopHost.IsWindow(desktop) || windows.Any(w => w.IsDisposed) || layout != nextLayout) Rebuild();
            else DesktopHost.MaintainLayerOrder(windows.Select(w => w.Handle));
        };
        SystemEvents.SessionSwitch += OnSessionSwitch;
        Rebuild();
        monitor.Start();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) locked = true;
        else if (e.Reason == SessionSwitchReason.SessionUnlock) locked = false;
        else return;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var window in windows) window.Dispose();
        windows.Clear();
        if (exiting || pause.Checked || locked) return;
        desktop = DesktopHost.Find();
        if (desktop == IntPtr.Zero)
        {
            tray.Text = "Scrapbook: waiting for Windows desktop";
            return;
        }
        try
        {
            var options = SaverOptions.Load();
            foreach (var screen in Screen.AllScreens)
            {
                var window = new WallpaperForm(options, desktop, screen.Bounds);
                windows.Add(window);
                window.Show();
                window.AttachToDesktop();
            }
            layout = string.Join(";", Screen.AllScreens.Select(s => s.Bounds.ToString()));
            tray.Text = "Scrapbook Live Wallpaper";
        }
        catch (Exception ex)
        {
            foreach (var window in windows) window.Dispose();
            windows.Clear();
            desktop = IntPtr.Zero;
            tray.Text = "Scrapbook: desktop attachment failed";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    internal void OpenSettings()
    {
        if (settings != null) { settings.Activate(); return; }
        settings = new SettingsWindow(SaverOptions.Load());
        // Modeless WPF windows need keyboard forwarding from the WinForms message loop.
        ElementHost.EnableModelessKeyboardInterop(settings);
        settings.Closed += (_, _) => { settings = null; if (!exiting) Rebuild(); };
        settings.Show();
        settings.Activate();
    }

    internal void StartSmokeTest(string path)
    {
        smoke = new System.Windows.Forms.Timer { Interval = 8000 };
        smoke.Tick += (_, _) =>
        {
            smoke.Stop();
            DesktopHost.GetWindowRect(desktop, out var hostBounds);
            File.WriteAllLines(path, new[] { $"Host={desktop}; Rect={hostBounds}", $"Displays={Screen.AllScreens.Length}", $"Windows={windows.Count}", $"StartupEnabled={WallpaperStartup.Enabled}" }
                .Concat(windows.Select(w => { DesktopHost.GetWindowRect(w.Handle, out var rect); return $"ParentMatches={DesktopHost.GetParent(w.Handle) == desktop}; ScreenRect={rect}; Photos={w.VisibleCards}; Sources={w.SourceCount}"; })));
            ExitThread();
        };
        smoke.Start();
    }

    protected override void ExitThreadCore()
    {
        exiting = true;
        settings?.Close();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            monitor.Dispose();
            smoke?.Dispose();
            foreach (var window in windows) window.Dispose();
            tray.Visible = false;
            tray.ContextMenuStrip?.Dispose();
            tray.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class WallpaperForm : Form
{
    private readonly ElementHost host = new() { Dock = DockStyle.Fill };
    private readonly GpuCollageView scene;
    private readonly System.Windows.Forms.Timer timer;
    private readonly IntPtr desktop;
    private readonly Rectangle screenBounds;
    public int VisibleCards => scene.VisibleCount;
    public int SourceCount => scene.SourceCount;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000080; // No activation; tool window.
            return cp;
        }
    }
    public WallpaperForm(SaverOptions options, IntPtr desktop, Rectangle bounds)
    {
        this.desktop = desktop;
        screenBounds = bounds;
        FormBorderStyle = FormBorderStyle.None;
        AllowTransparency = true;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(24, 25, 29);
        Controls.Add(host);
        scene = new GpuCollageView(options);
        host.Child = scene.Root;
        timer = new System.Windows.Forms.Timer { Interval = options.IntervalSeconds * 1000 };
        timer.Tick += (_, _) => scene.RequestNext();
    }
    public void AttachToDesktop()
    {
        DesktopHost.Attach(Handle, desktop);
        var point = screenBounds.Location;
        if (!DesktopHost.ScreenToClient(desktop, ref point)) throw new System.ComponentModel.Win32Exception();
        if (!DesktopHost.SetWindowPos(Handle, IntPtr.Zero, point.X, point.Y, screenBounds.Width, screenBounds.Height, 0x0034))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        scene.RequestNext();
        timer.Start();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); scene.Dispose(); host.Dispose(); }
        base.Dispose(disposing);
    }
}

internal static class DesktopHost
{
    private static IntPtr progman, worker, icons;
    private static bool raisedDesktop;
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? name);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int length);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRect { public int Left, Top, Right, Bottom; public override string ToString() => $"{Left},{Top} - {Right},{Bottom}"; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    public static void Attach(IntPtr window, IntPtr parent)
    {
        // Raised desktops require an opaque layered sibling of WorkerW under Progman,
        // below SHELLDLL_DefView. Parenting inside WorkerW can render behind the static image.
        // Reference: rocksdanister/lively, Core/WinDesktopCore.cs (core-separation).
        SetWindowLong(window, -16, (GetWindowLong(window, -16) & unchecked((int)~0x80000000u)) | 0x40000000);
        if (raisedDesktop)
        {
            SetWindowLong(window, -20, GetWindowLong(window, -20) | 0x00080000);
            if (!SetLayeredWindowAttributes(window, 0, 255, 2)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        SetParent(window, parent);
        if (GetParent(window) != parent) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        MaintainLayerOrder([window]);
    }

    public static void MaintainLayerOrder(IEnumerable<IntPtr> windows)
    {
        if (!raisedDesktop || !IsWindow(progman)) return;
        icons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (icons == IntPtr.Zero) return;
        foreach (var window in windows)
            SetWindowPos(window, icons, 0, 0, 0, 0, 0x0013);
        if (worker != IntPtr.Zero) SetWindowPos(worker, new IntPtr(1), 0, 0, 0, 0, 0x0013);
    }

    public static string Describe()
    {
        var lines = new List<string>();
        void Visit(IntPtr window, int depth)
        {
            var name = new System.Text.StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            GetWindowRect(window, out var bounds);
            lines.Add($"{new string(' ', depth * 2)}{window}: {name}; Visible={IsWindowVisible(window)}; Style=0x{GetWindowLong(window, -16):X8}; ExStyle=0x{GetWindowLong(window, -20):X8}; Rect={bounds}");
            if (depth >= 3) return;
            for (var child = GetWindow(window, 5); child != IntPtr.Zero; child = GetWindow(child, 2)) Visit(child, depth + 1);
        }
        var programManager = FindWindow("Progman", null);
        if (programManager != IntPtr.Zero) Visit(programManager, 0);
        EnumWindows((window, _) => { var name = new System.Text.StringBuilder(256); GetClassName(window, name, name.Capacity); if (name.ToString() == "WorkerW") Visit(window, 0); return true; }, IntPtr.Zero);
        return string.Join(Environment.NewLine, lines);
    }

    public static IntPtr Find()
    {
        progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;
        raisedDesktop = (GetWindowLong(progman, -20) & 0x00200000) != 0;
        // Explorer's desktop host protocol is undocumented; re-discover after shell restarts.
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), 2, 1000, out _);
        worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        icons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (raisedDesktop) return worker != IntPtr.Zero && icons != IntPtr.Zero ? progman : IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                worker = FindWindowEx(IntPtr.Zero, window, "WorkerW", null);
                if (worker != IntPtr.Zero) return false;
            }
            return true;
        }, IntPtr.Zero);
        return worker;
    }
}
