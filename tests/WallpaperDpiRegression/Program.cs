using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;

// Interactive Windows regression: exercises the real WPF Closed callback without
// saving or changing user settings. Run with the normal wallpaper app stopped.
internal static class Program
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type ContextType = Assembly.Load("ScrapbookWallpaper").GetType("ScrapbookSaver.WallpaperContext", true)!;
    private static int failures;

    [STAThread]
    private static int Main()
    {
        using var mutex = new Mutex(true, @"Local\ScrapbookWallpaper", out var first);
        if (!first) { Console.Error.WriteLine("Stop ScrapbookWallpaper before running the regression."); return 2; }
        // Use the app's generated initializer, including its actual DPI default.
        ContextType.Assembly.GetTypes().Single(type => type.Name == "ApplicationConfiguration")
            .GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, null);
        using var context = (ApplicationContext)Activator.CreateInstance(ContextType)!;
        using var timer = new System.Windows.Forms.Timer { Interval = 1800 };
        var stage = 0;
        timer.Tick += (_, _) =>
        {
            try
            {
                if (stage % 2 == 0)
                {
                    Check(context, stage == 0 ? "initial" : $"after-settings-close-{stage / 2}");
                    if (stage == 6)
                    {
                        CheckCallerContexts(context);
                        timer.Stop();
                        context.ExitThread();
                        return;
                    }
                    ContextType.GetMethod("OpenSettings", Members)!.Invoke(context, null);
                }
                else
                {
                    var settings = (System.Windows.Window)ContextType.GetField("settings", Members)!.GetValue(context)!;
                    // Dispatch through the WPF HWND so Windows installs that window's
                    // DPI context, just as it does for a Save/Cancel input message.
                    if (!PostMessage(new WindowInteropHelper(settings).Handle, 0x0010, IntPtr.Zero, IntPtr.Zero))
                        throw new System.ComponentModel.Win32Exception();
                }
                stage++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                failures++;
                timer.Stop();
                context.ExitThread();
            }
        };
        timer.Start();
        Application.Run(context);
        Console.WriteLine(failures == 0 ? "PASS: initial, repeated settings-close, and mixed caller DPI contexts" : $"FAIL: {failures} geometry/check errors");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckCallerContexts(ApplicationContext context)
    {
        foreach (var mode in new[] { -1, -2, -3, -4 })
        {
            var caller = new IntPtr(mode);
            var previous = SetThreadDpiAwarenessContext(caller);
            if (previous == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            try
            {
                // Prime the WinForms cache in the caller's context, as settings or
                // other UI code can do before invoking a rebuild.
                _ = Screen.AllScreens.Select(screen => screen.Bounds).ToArray();
                ContextType.GetMethod("Rebuild", Members)!.Invoke(context, null);
                if (!AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), caller))
                {
                    Console.Error.WriteLine($"Caller DPI context {mode} was not restored.");
                    failures++;
                }
                Check(context, $"caller-context-{mode}");
            }
            finally { SetThreadDpiAwarenessContext(previous); }
        }
    }

    private static void Check(ApplicationContext context, string phase)
    {
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            var monitors = new List<Rect>();
            MonitorCallback callback = (IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data) => { monitors.Add(rect); return true; };
            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception();
            var windows = ((IEnumerable)ContextType.GetField("windows", Members)!.GetValue(context)!).Cast<Form>().ToArray();
            var desktop = (IntPtr)ContextType.GetField("desktop", Members)!.GetValue(context)!;
            Console.WriteLine($"{phase}: monitors={monitors.Count}, windows={windows.Length}, desktop-awareness={Awareness(desktop)}");
            var actual = new List<Rect>();
            foreach (var window in windows)
            {
                if (!GetWindowRect(window.Handle, out var rect)) throw new System.ComponentModel.Win32Exception();
                actual.Add(rect);
                var matches = monitors.Contains(rect) && GetParent(window.Handle) == desktop;
                Console.WriteLine($"  actual={rect}; awareness={Awareness(window.Handle)}; dpi={GetDpiForWindow(window.Handle)}; matches-monitor={matches}");
                if (!matches) failures++;
            }
            foreach (var expected in monitors)
            {
                Console.WriteLine($"  expected={expected}");
                if (actual.Count(rect => rect.Equals(expected)) != 1) failures++;
            }
            if (windows.Length != monitors.Count) failures++;
        }
        finally { SetThreadDpiAwarenessContext(previous); }
    }

    private static int Awareness(IntPtr window) => GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(window));
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
        public override readonly string ToString() => $"{Left},{Top} - {Right},{Bottom}";
    }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
