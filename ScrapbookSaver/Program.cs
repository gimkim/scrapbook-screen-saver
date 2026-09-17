using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms.Integration;

namespace ScrapbookSaver;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        ApplicationConfiguration.Initialize();
        var options = SaverOptions.Load();
        var command = args.Length == 0 ? "/c" : args[0].ToLowerInvariant();

        if (command is "--gpu-info")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "gpu-info.txt");
            File.WriteAllText(destination, $"WPF rendering tier: {System.Windows.Media.RenderCapability.Tier >> 16}{Environment.NewLine}");
            return;
        }

        if (command is "--fullscreen-smoke-test")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "fullscreen-smoke.txt");
            var forms = Screen.AllScreens.Select(screen => new SaverForm(options, screen.Bounds)).ToArray();
            if (forms.Length == 0) return;
            using var timer = new System.Windows.Forms.Timer { Interval = 4500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var lines = new List<string> { $"WPF rendering tier: {System.Windows.Media.RenderCapability.Tier >> 16}", $"Displays: {forms.Length}" };
                for (var i = 0; i < forms.Length; i++)
                    lines.Add($"Display {i + 1}: screen {Screen.AllScreens[i].Bounds.Width}x{Screen.AllScreens[i].Bounds.Height}, " +
                        $"form {forms[i].Bounds.Width}x{forms[i].Bounds.Height}, view {forms[i].ViewWidth:0}x{forms[i].ViewHeight:0} at {forms[i].RenderScale:0.##}x, " +
                        $"visible {forms[i].VisibleCards}, prefetched {forms[i].PrefetchedCards}");
                File.WriteAllLines(destination, lines);
                forms[0].Close();
            };
            var context = new SaverContext(forms);
            timer.Start();
            Application.Run(context);
            return;
        }

        if (command is "--render-gpu-test" or "--render-gpu-random-test")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "gpu-preview.png");
            if (command == "--render-gpu-random-test")
            {
                options.Animation = EntranceAnimation.Random;
                options.IntervalSeconds = 1;
            }
            if (args.Length > 2) options.ImageFolders = args.Skip(2)
                .Select(path => new ImageFolderSetting
                {
                    Path = path.TrimStart('!'),
                    Enabled = !path.StartsWith('!')
                }).ToList();
            using var form = new SaverForm(options, Screen.PrimaryScreen!.Bounds, true)
            {
                Location = new Point(-3000, -3000),
                ShowInTaskbar = false
            };
            using var captureTimer = new System.Windows.Forms.Timer { Interval = command == "--render-gpu-random-test" ? 7500 : 2500 };
            captureTimer.Tick += (_, _) =>
            {
                captureTimer.Stop();
                form.SaveSnapshot(destination);
                File.WriteAllText(Path.ChangeExtension(destination, ".txt"),
                    $"WPF rendering tier: {System.Windows.Media.RenderCapability.Tier >> 16}{Environment.NewLine}" +
                    $"Source images: {form.SourceImages}{Environment.NewLine}" +
                    $"Visible cards: {form.VisibleCards}{Environment.NewLine}" +
                    $"Prefetched cards: {form.PrefetchedCards}{Environment.NewLine}" +
                    $"Entrance animations: {form.AnimationSummary}{Environment.NewLine}");
                form.Close();
            };
            form.Shown += (_, _) => captureTimer.Start();
            Application.Run(form);
            return;
        }

        if (command is "--render-settings-test" or "--render-random-settings-test")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "settings-preview.png");
            if (command == "--render-random-settings-test") options.Animation = EntranceAnimation.Random;
            if (args.Length > 5) options.ImageFolders = args.Skip(5)
                .Select(path => new ImageFolderSetting
                {
                    Path = path.TrimStart('!'),
                    Enabled = !path.StartsWith('!')
                }).ToList();
            var window = new SettingsWindow(options)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = -3000,
                Top = -3000,
                ShowInTaskbar = false,
                MinWidth = 300,
                MinHeight = 260
            };
            if (int.TryParse(args.ElementAtOrDefault(2), out var width)) window.Width = width;
            if (int.TryParse(args.ElementAtOrDefault(3), out var height)) window.Height = height;
            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(() =>
            {
                window.SaveSnapshot(destination, args.ElementAtOrDefault(4) == "bottom");
                File.WriteAllText(Path.ChangeExtension(destination, ".txt"),
                    $"DPI scale: {System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX:0.##}x{Environment.NewLine}" +
                    $"Window size: {window.ActualWidth:0}x{window.ActualHeight:0} DIPs{Environment.NewLine}");
                window.Close();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            new System.Windows.Application().Run(window);
            return;
        }

        if (command is "--render-preview-window-test")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "preview-window.png");
            var owner = new SettingsWindow(options)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = -3000,
                Top = -3000,
                ShowInTaskbar = false
            };
            owner.Loaded += (_, _) => owner.Dispatcher.BeginInvoke(() =>
            {
                var preview = new PreviewWindow(options, owner) { ShowInTaskbar = false };
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    preview.SaveSnapshot(destination);
                    preview.Close();
                };
                preview.Loaded += (_, _) => timer.Start();
                preview.ShowDialog();
                owner.Close();
            });
            new System.Windows.Application().Run(owner);
            return;
        }

        if (command is "/c" or "-c" or "--config" || command.StartsWith("/c:"))
        {
            new System.Windows.Application().Run(new SettingsWindow(options));
            return;
        }

        if (command is "--preview")
        {
            Application.Run(new SaverForm(options, Screen.PrimaryScreen!.Bounds, true));
            return;
        }

        if (command is "--render-test")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "preview.png");
            using var scene = new CollageScene(options, new Size(1280, 720));
            for (var i = 0; i < Math.Min(options.MaxPhotos, 16); i++) scene.AddNext();
            using var output = new Bitmap(1280, 720);
            using (var graphics = Graphics.FromImage(output)) scene.Draw(graphics, 1);
            output.Save(destination, ImageFormat.Png);
            return;
        }

        if (command is "--render-animation-test")
        {
            var destination = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "animation-test");
            Directory.CreateDirectory(destination);
            foreach (var animation in new[] { EntranceAnimation.Fade, EntranceAnimation.Slide, EntranceAnimation.Zoom })
            {
                options.Animation = animation;
                using var scene = new CollageScene(options, new Size(1280, 720));
                scene.AddNext();
                foreach (var progress in new[] { 0d, 0.5d, 1d })
                {
                    using var output = new Bitmap(1280, 720);
                    using (var graphics = Graphics.FromImage(output)) scene.Draw(graphics, progress);
                    output.Save(Path.Combine(destination, $"{animation}-{progress:0.0}.png"), ImageFormat.Png);
                }
            }
            return;
        }

        if (command is "/p" or "-p" || command.StartsWith("/p:"))
        {
            string? rawHandle = command.StartsWith("/p:") ? args[0][3..] : args.ElementAtOrDefault(1);
            if (long.TryParse(rawHandle, out var handle) && handle != 0)
                Application.Run(new SaverForm(options, Screen.PrimaryScreen!.Bounds, false, new IntPtr(handle)));
            return;
        }

        if (command is "/s" or "-s" or "--screensaver")
        {
            var forms = Screen.AllScreens.Select(screen => new SaverForm(options, screen.Bounds)).ToArray();
            if (forms.Length == 0) return;
            Application.Run(new SaverContext(forms));
        }
    }
}

internal enum EntranceAnimation { None, Fade, Slide, Zoom, Random }

internal static class EntranceAnimationPicker
{
    public static EntranceAnimation Choose(EntranceAnimation selected, Random random) =>
        selected == EntranceAnimation.Random ? (EntranceAnimation)random.Next(1, 4) : selected;
}

public sealed class ImageFolderSetting
{
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

internal sealed class SaverOptions
{
    public string Folder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public List<string> Folders { get; set; } = [];
    public List<ImageFolderSetting> ImageFolders { get; set; } = [];
    [JsonIgnore] public IEnumerable<string> EnabledFolders => ImageFolders.Where(folder => folder.Enabled).Select(folder => folder.Path);
    public int IntervalSeconds { get; set; } = 1;
    public int MinWidthPercent { get; set; } = 30;
    public int MaxWidthPercent { get; set; } = 40;
    public int EdgeOverflowPercent { get; set; } = 10;
    public int BorderPixels { get; set; } = 10;
    public int MaxTiltDegrees { get; set; } = 15;
    public int MaxPhotos { get; set; } = 32;
    public bool DropShadow { get; set; } = true;
    public EntranceAnimation Animation { get; set; } = EntranceAnimation.Random;
    public int AnimationDurationMs { get; set; } = 500;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScrapbookSaver", "settings.json");

    public static SaverOptions Load()
    {
        try
        {
            var options = JsonSerializer.Deserialize<SaverOptions>(File.ReadAllText(SettingsPath)) ?? new();
            options.Normalize();
            return options;
        }
        catch
        {
            var options = new SaverOptions();
            options.Normalize();
            return options;
        }
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Normalize()
    {
        Folders ??= [];
        ImageFolders ??= [];
        if (ImageFolders.Count == 0)
        {
            var legacyPaths = Folders.Count > 0 ? Folders : [Folder];
            ImageFolders = legacyPaths.Select(path => new ImageFolderSetting { Path = path, Enabled = true }).ToList();
        }
        ImageFolders = ImageFolders.Where(folder => folder != null && !string.IsNullOrWhiteSpace(folder.Path))
            .Select(folder => new ImageFolderSetting { Path = folder.Path.Trim(), Enabled = folder.Enabled })
            .GroupBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        Folders = ImageFolders.Select(folder => folder.Path).ToList();
        if (Folders.Count > 0) Folder = Folders[0];
        IntervalSeconds = Math.Clamp(IntervalSeconds, 1, 120);
        MinWidthPercent = Math.Clamp(MinWidthPercent, 5, 70);
        MaxWidthPercent = Math.Clamp(MaxWidthPercent, MinWidthPercent, 80);
        EdgeOverflowPercent = Math.Clamp(EdgeOverflowPercent, 0, 25);
        BorderPixels = Math.Clamp(BorderPixels, 0, 30);
        MaxTiltDegrees = Math.Clamp(MaxTiltDegrees, 0, 20);
        MaxPhotos = Math.Clamp(MaxPhotos, 1, 100);
        AnimationDurationMs = Math.Clamp(AnimationDurationMs, 200, 5000);
        if (!Enum.IsDefined(Animation)) Animation = EntranceAnimation.Fade;
    }
}

internal sealed class SaverContext : ApplicationContext
{
    private readonly SaverForm[] forms;
    private bool closing;
    public SaverContext(SaverForm[] forms)
    {
        this.forms = forms;
        foreach (var form in forms)
        {
            form.FormClosed += (_, _) =>
            {
                if (closing) return;
                closing = true;
                foreach (var other in this.forms.Where(other => !other.IsDisposed)) other.Close();
                ExitThread();
            };
            form.Show();
        }
    }
}

internal sealed class SaverForm : Form
{
    private readonly SaverOptions options;
    private readonly bool preview;
    private readonly IntPtr parentHandle;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly ElementHost host = new() { Dock = DockStyle.Fill };
    private GpuCollageView? scene;
    private Point? initialMouse;
    private DateTime shownAt;

    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr handle, out Rectangle rectangle);

    public SaverForm(SaverOptions options, Rectangle bounds, bool preview = false, IntPtr parentHandle = default)
    {
        this.options = options;
        this.preview = preview;
        this.parentHandle = parentHandle;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = preview ? FormBorderStyle.Sizable : FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = preview ? new Rectangle(bounds.X + 80, bounds.Y + 60, Math.Min(1000, bounds.Width - 160), Math.Min(650, bounds.Height - 120)) : bounds;
        Text = "Scrapbook Screen Saver — Press Esc to close";
        TopMost = !preview && parentHandle == IntPtr.Zero;
        ShowInTaskbar = preview;
        BackColor = Color.FromArgb(24, 25, 29);
        KeyPreview = true;
        Controls.Add(host);
        timer.Interval = options.IntervalSeconds * 1000;
        timer.Tick += (_, _) => scene?.RequestNext();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape || (!preview && parentHandle == IntPtr.Zero)) Close(); };
        MouseDown += (_, _) => { if (!preview && parentHandle == IntPtr.Zero) Close(); };
        MouseMove += (_, e) => HandleMouseMove(e.Location);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (parentHandle != IntPtr.Zero)
        {
            SetParent(Handle, parentHandle);
            if (GetClientRect(parentHandle, out var rect)) Bounds = new Rectangle(0, 0, rect.Width, rect.Height);
        }
        else if (!preview) Cursor.Hide();
        shownAt = DateTime.UtcNow;
        scene = new GpuCollageView(options);
        host.Child = scene.Root;
        scene.Root.PreviewMouseMove += (_, args) =>
        {
            var point = args.GetPosition(scene.Root);
            HandleMouseMove(new Point((int)point.X, (int)point.Y));
        };
        scene.Root.PreviewMouseDown += (_, _) => { if (!preview && parentHandle == IntPtr.Zero) Close(); };
        scene.Root.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Escape || (!preview && parentHandle == IntPtr.Zero)) Close();
        };
        scene.Root.Focus();
        scene.RequestNext();
        timer.Start();
    }

    private void HandleMouseMove(Point location)
    {
        if (preview || parentHandle != IntPtr.Zero || DateTime.UtcNow - shownAt < TimeSpan.FromMilliseconds(900)) return;
        initialMouse ??= location;
        if (Math.Abs(location.X - initialMouse.Value.X) > 8 || Math.Abs(location.Y - initialMouse.Value.Y) > 8) Close();
    }

    public void SaveSnapshot(string destination) => scene?.SaveSnapshot(destination);
    public int PrefetchedCards => scene?.PrefetchedCount ?? 0;
    public int VisibleCards => scene?.VisibleCount ?? 0;
    public int SourceImages => scene?.SourceCount ?? 0;
    public string AnimationSummary => scene?.AnimationSummary ?? "";
    public double ViewWidth => scene?.ViewWidth ?? 0;
    public double ViewHeight => scene?.ViewHeight ?? 0;
    public double RenderScale => scene?.RenderScale ?? 1;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            scene?.Dispose();
            host.Dispose();
            if (!preview && parentHandle == IntPtr.Zero) Cursor.Show();
        }
        base.Dispose(disposing);
    }
}

internal sealed class CollageScene : IDisposable
{
    private readonly SaverOptions options;
    private readonly Random random = new();
    private readonly List<PhotoCard> cards = [];
    private readonly List<string> photoPaths;
    private Queue<string> deck = new();
    private Size size;

    public bool IsAnimating => options.Animation != EntranceAnimation.None &&
        cards.Any(card => Stopwatch.GetElapsedTime(card.StartTimestamp).TotalMilliseconds < options.AnimationDurationMs);

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

    public CollageScene(SaverOptions options, Size size)
    {
        this.options = options;
        this.size = size;
        photoPaths = options.EnabledFolders.SelectMany(folder =>
        {
            try { return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray(); }
            catch { return []; }
        }).Where(path => Extensions.Contains(Path.GetExtension(path)))
          .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Resize(Size newSize)
    {
        size = newSize;
        foreach (var card in cards) card.Dispose();
        cards.Clear();
    }

    public void AddNext()
    {
        if (size.Width < 100 || size.Height < 100 || photoPaths.Count == 0) return;
        for (var attempt = 0; attempt < Math.Min(photoPaths.Count, 12); attempt++)
        {
            if (deck.Count == 0) deck = new Queue<string>(photoPaths.OrderBy(_ => random.Next()));
            var path = deck.Dequeue();
            try
            {
                using var source = Image.FromFile(path);
                using var oriented = new Bitmap(source);
                ApplyOrientation(oriented, source);
                var maxCardWidth = size.Width * random.Next(options.MinWidthPercent, options.MaxWidthPercent + 1) / 100;
                var maxCardHeight = (int)(size.Height * 0.58);
                var scale = Math.Min((double)maxCardWidth / oriented.Width, (double)maxCardHeight / oriented.Height);
                var width = Math.Max(1, (int)(oriented.Width * scale));
                var height = Math.Max(1, (int)(oriented.Height * scale));
                using var bitmap = new Bitmap(width, height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(oriented, 0, 0, width, height);
                }
                var angle = (float)(random.NextDouble() * 2 - 1) * options.MaxTiltDegrees;
                var radians = Math.Abs(angle) * Math.PI / 180;
                var surface = ComposeCard(bitmap);
                var halfWidth = (int)Math.Ceiling((surface.Width * Math.Cos(radians) + surface.Height * Math.Sin(radians)) / 2);
                var halfHeight = (int)Math.Ceiling((surface.Height * Math.Cos(radians) + surface.Width * Math.Sin(radians)) / 2);
                var overflowX = halfWidth * 2 * options.EdgeOverflowPercent / 100.0;
                var overflowY = halfHeight * 2 * options.EdgeOverflowPercent / 100.0;
                var minX = halfWidth - overflowX;
                var maxX = size.Width - halfWidth + overflowX;
                var minY = halfHeight - overflowY;
                var maxY = size.Height - halfHeight + overflowY;
                var centerX = maxX > minX ? (int)(minX + random.NextDouble() * (maxX - minX)) : size.Width / 2;
                var centerY = maxY > minY ? (int)(minY + random.NextDouble() * (maxY - minY)) : size.Height / 2;
                cards.Add(new PhotoCard(surface, new Point(centerX, centerY), angle, Stopwatch.GetTimestamp(),
                    random.Next(4), EntranceAnimationPicker.Choose(options.Animation, random)));
                if (cards.Count > options.MaxPhotos)
                {
                    cards[0].Dispose();
                    cards.RemoveAt(0);
                }
                return;
            }
            catch { /* Skip unreadable or cloud-only photos and try another. */ }
        }
    }

    private Bitmap ComposeCard(Bitmap photo)
    {
        var border = options.BorderPixels;
        var pad = border + (options.DropShadow ? 18 : 4);
        var surface = new Bitmap(photo.Width + pad * 2, photo.Height + pad * 2, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(surface);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var frame = new Rectangle(pad - border, pad - border, photo.Width + 2 * border, photo.Height + 2 * border);
        if (options.DropShadow)
        {
            using var shadow = new SolidBrush(Color.FromArgb(75, 0, 0, 0));
            for (var i = 12; i >= 3; i -= 3)
            {
                using var haze = new SolidBrush(Color.FromArgb(9, 0, 0, 0));
                graphics.FillRectangle(haze, frame.X + 5 - i / 2f, frame.Y + 7 - i / 2f, frame.Width + i, frame.Height + i);
            }
            graphics.FillRectangle(shadow, frame.X + 5, frame.Y + 7, frame.Width, frame.Height);
        }
        if (border > 0)
        {
            using var white = new SolidBrush(Color.White);
            graphics.FillRectangle(white, frame);
        }
        graphics.DrawImage(photo, pad, pad, photo.Width, photo.Height);
        return surface;
    }

    private static void ApplyOrientation(Bitmap bitmap, Image source)
    {
        const int orientationId = 0x0112;
        if (!source.PropertyIdList.Contains(orientationId)) return;
        var orientation = source.GetPropertyItem(orientationId)?.Value;
        var value = orientation is { Length: > 0 } ? orientation[0] : (byte)1;
        var flip = value switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        bitmap.RotateFlip(flip);
    }

    public void Draw(Graphics graphics, double? progressOverride = null)
    {
        graphics.Clear(Color.FromArgb(24, 25, 29));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        foreach (var card in cards)
        {
            var progress = progressOverride ?? Math.Clamp(
                Stopwatch.GetElapsedTime(card.StartTimestamp).TotalMilliseconds / options.AnimationDurationMs, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var alpha = card.Animation == EntranceAnimation.Fade ? eased : 1d;
            var scale = card.Animation == EntranceAnimation.Zoom ? 0.2 + 0.8 * eased : 1d;
            var slide = card.Animation == EntranceAnimation.Slide ? (int)(Math.Min(size.Width, size.Height) * 0.25 * (1 - eased)) : 0;
            var offsetX = card.SlideDirection switch { 0 => -slide, 1 => slide, _ => 0 };
            var offsetY = card.SlideDirection switch { 2 => -slide, 3 => slide, _ => 0 };
            var saved = graphics.Save();
            var width = card.Surface.Width;
            var height = card.Surface.Height;
            graphics.TranslateTransform(card.Center.X + offsetX, card.Center.Y + offsetY);
            graphics.RotateTransform(card.Angle);
            graphics.ScaleTransform((float)scale, (float)scale);
            var target = new Rectangle(-width / 2, -height / 2, width, height);
            if (alpha < 0.999)
            {
                using var attributes = new ImageAttributes();
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)alpha });
                graphics.DrawImage(card.Surface, target, 0, 0, width, height, GraphicsUnit.Pixel, attributes);
            }
            else graphics.DrawImage(card.Surface, target);
            graphics.Restore(saved);
        }
        if (photoPaths.Count == 0)
        {
            using var font = new Font("Segoe UI", 18);
            using var brush = new SolidBrush(Color.White);
            graphics.DrawString("No images found in enabled folders. Open settings with /c.", font, brush, 30, 30);
        }
    }

    public void Dispose()
    {
        foreach (var card in cards) card.Dispose();
        cards.Clear();
    }

    private sealed record PhotoCard(Bitmap Surface, Point Center, float Angle, long StartTimestamp,
        int SlideDirection, EntranceAnimation Animation) : IDisposable
    {
        public void Dispose() => Surface.Dispose();
    }
}
