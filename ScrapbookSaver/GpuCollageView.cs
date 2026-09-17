using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace ScrapbookSaver;

// WPF composites cached image sprites and their transforms on the GPU when a
// hardware rendering tier is available. File decoding and sprite rendering
// happen ahead of time on background threads.
internal sealed class GpuCollageView : IDisposable
{
    private const int PrefetchCapacity = 5;
    private static readonly SemaphoreSlim DecodeSlots = new(2);
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

    private readonly SaverOptions options;
    private readonly Random random = new();
    private readonly Task<List<string>> catalogTask;
    private List<string> paths = [];
    private readonly Queue<PreparedCard> ready = new();
    private readonly Queue<UIElement> visible = new();
    private Queue<string> deck = new();
    private double viewWidth;
    private double viewHeight;
    private double renderScale = 1;
    private int generation;
    private bool prefetchRunning;
    private bool catalogLoaded;
    private bool pendingPhoto;
    private bool disposed;
    private readonly int[] animationCounts = new int[4];

    public Canvas Root { get; } = new()
    {
        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 25, 29)),
        ClipToBounds = true,
        Focusable = true
    };
    public int PrefetchedCount => ready.Count;
    public int VisibleCount => visible.Count;
    public int SourceCount => paths.Count;
    public string AnimationSummary => $"Fade={animationCounts[1]}, Slide={animationCounts[2]}, Zoom={animationCounts[3]}";
    public double ViewWidth => viewWidth;
    public double ViewHeight => viewHeight;
    public double RenderScale => renderScale;

    public GpuCollageView(SaverOptions options)
    {
        this.options = options;
        var folders = options.EnabledFolders.ToArray();
        catalogTask = Task.Run(() => folders.SelectMany(folder =>
        {
            try { return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray(); }
            catch { return []; }
        }).Where(path => Extensions.Contains(Path.GetExtension(path)))
          .Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        Root.SizeChanged += (_, e) =>
        {
            if (Math.Abs(e.NewSize.Width - viewWidth) < 1 && Math.Abs(e.NewSize.Height - viewHeight) < 1) return;
            viewWidth = e.NewSize.Width;
            viewHeight = e.NewSize.Height;
            renderScale = Math.Max(1, VisualTreeHelper.GetDpi(Root).DpiScaleX);
            generation++;
            ready.Clear();
            visible.Clear();
            Root.Children.Clear();
            pendingPhoto = true;
            EnsurePrefetch();
        };
    }

    public void RequestNext()
    {
        if (disposed) return;
        if (catalogLoaded && paths.Count == 0)
        {
            ShowMessage("No images found in enabled folders. Open settings with /c.");
            return;
        }
        if (viewWidth < 100 || viewHeight < 100) return;
        if (ready.Count > 0) ShowNext();
        else pendingPhoto = true;
        EnsurePrefetch();
    }

    private void EnsurePrefetch()
    {
        if (prefetchRunning || disposed || viewWidth < 100 || viewHeight < 100) return;
        _ = FillPrefetchAsync();
    }

    private async Task FillPrefetchAsync()
    {
        prefetchRunning = true;
        var consecutiveFailures = 0;
        try
        {
            if (!catalogLoaded)
            {
                paths = await catalogTask;
                if (disposed) return;
                catalogLoaded = true;
            }
            if (paths.Count == 0)
            {
                ShowMessage("No images found in enabled folders. Open settings with /c.");
                return;
            }
            while (!disposed && ready.Count < PrefetchCapacity)
            {
                var currentGeneration = generation;
                var path = NextPath();
                var scale = renderScale;
                var maxWidth = (int)(viewWidth * scale * random.Next(options.MinWidthPercent, options.MaxWidthPercent + 1) / 100);
                var maxHeight = (int)(viewHeight * scale * 0.58);
                PreparedPixels? pixels = null;
                await DecodeSlots.WaitAsync();
                try { pixels = await Task.Run(() => PrepareCard(path, maxWidth, maxHeight, options)); }
                finally { DecodeSlots.Release(); }
                if (disposed) return;
                if (currentGeneration != generation) { consecutiveFailures = 0; continue; }
                if (pixels == null)
                {
                    if (++consecutiveFailures >= paths.Count)
                    {
                        if (visible.Count == 0) ShowMessage("No readable images found in the selected folders.");
                        break;
                    }
                    continue;
                }
                consecutiveFailures = 0;
                var source = BitmapSource.Create(pixels.Width, pixels.Height, 96 * scale, 96 * scale,
                    PixelFormats.Pbgra32, null, pixels.Bytes, pixels.Stride);
                source.Freeze();
                ready.Enqueue(new PreparedCard(source, pixels.Width / scale, pixels.Height / scale));
                if (pendingPhoto)
                {
                    pendingPhoto = false;
                    ShowNext();
                }
            }
        }
        catch
        {
            if (!disposed && visible.Count == 0) ShowMessage("Unable to prepare images. Check the selected folders.");
        }
        finally { prefetchRunning = false; }
    }

    private string NextPath()
    {
        if (deck.Count == 0) deck = new Queue<string>(paths.OrderBy(_ => random.Next()));
        return deck.Dequeue();
    }

    private void ShowNext()
    {
        if (ready.Count == 0) return;
        var card = ready.Dequeue();
        var angle = (random.NextDouble() * 2 - 1) * options.MaxTiltDegrees;
        var radians = Math.Abs(angle) * Math.PI / 180;
        var halfWidth = (card.Width * Math.Cos(radians) + card.Height * Math.Sin(radians)) / 2;
        var halfHeight = (card.Height * Math.Cos(radians) + card.Width * Math.Sin(radians)) / 2;
        var centerX = RandomCenter(viewWidth, halfWidth, options.EdgeOverflowPercent);
        var centerY = RandomCenter(viewHeight, halfHeight, options.EdgeOverflowPercent);

        var sprite = new System.Windows.Controls.Image
        {
            Source = card.Source,
            Width = card.Width,
            Height = card.Height,
            Stretch = Stretch.Fill,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
        };
        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        var transforms = new TransformGroup();
        transforms.Children.Add(scale);
        transforms.Children.Add(new RotateTransform(angle));
        transforms.Children.Add(translate);
        sprite.RenderTransform = transforms;
        Canvas.SetLeft(sprite, centerX - card.Width / 2d);
        Canvas.SetTop(sprite, centerY - card.Height / 2d);
        Root.Children.Add(sprite);
        visible.Enqueue(sprite);
        while (visible.Count > options.MaxPhotos) Root.Children.Remove(visible.Dequeue());

        var duration = TimeSpan.FromMilliseconds(options.AnimationDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var entrance = EntranceAnimationPicker.Choose(options.Animation, random);
        if (entrance != EntranceAnimation.None) animationCounts[(int)entrance]++;
        switch (entrance)
        {
            case EntranceAnimation.Fade:
                sprite.Opacity = 0;
                sprite.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
                break;
            case EntranceAnimation.Slide:
                var distance = Math.Min(viewWidth, viewHeight) * 0.25;
                var direction = random.Next(4);
                if (direction < 2)
                {
                    translate.X = direction == 0 ? -distance : distance;
                    translate.BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation(translate.X, 0, duration) { EasingFunction = easing });
                }
                else
                {
                    translate.Y = direction == 2 ? -distance : distance;
                    translate.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(translate.Y, 0, duration) { EasingFunction = easing });
                }
                break;
            case EntranceAnimation.Zoom:
                scale.ScaleX = scale.ScaleY = 0.2;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.2, 1, duration) { EasingFunction = easing });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.2, 1, duration) { EasingFunction = easing });
                break;
        }
        EnsurePrefetch();
    }

    private double RandomCenter(double span, double halfSize, int edgeOverflowPercent)
    {
        var overflow = 2 * halfSize * edgeOverflowPercent / 100.0;
        var min = halfSize - overflow;
        var max = span - halfSize + overflow;
        if (max <= min) return span / 2;
        return min + random.NextDouble() * (max - min);
    }

    private void ShowMessage(string message)
    {
        if (Root.Children.OfType<TextBlock>().Any()) return;
        Root.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = System.Windows.Media.Brushes.White,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 24,
            Margin = new Thickness(30)
        });
    }

    private static PreparedPixels? PrepareCard(string path, int maxWidth, int maxHeight, SaverOptions options)
    {
        try
        {
            using var source = Drawing.Image.FromFile(path);
            using var oriented = new Drawing.Bitmap(source);
            ApplyOrientation(oriented, source);
            var scale = Math.Min((double)maxWidth / oriented.Width, (double)maxHeight / oriented.Height);
            var width = Math.Max(1, (int)(oriented.Width * scale));
            var height = Math.Max(1, (int)(oriented.Height * scale));
            var border = options.BorderPixels;
            var pad = border + (options.DropShadow ? 18 : 4);
            using var bitmap = new Drawing.Bitmap(width + 2 * pad, height + 2 * pad, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.Clear(Drawing.Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var frame = new Drawing.Rectangle(pad - border, pad - border, width + 2 * border, height + 2 * border);
                if (options.DropShadow)
                {
                    using var shadow = new Drawing.SolidBrush(Drawing.Color.FromArgb(75, 0, 0, 0));
                    for (var i = 12; i >= 3; i -= 3)
                    {
                        using var haze = new Drawing.SolidBrush(Drawing.Color.FromArgb(9, 0, 0, 0));
                        graphics.FillRectangle(haze, frame.X + 5 - i / 2f, frame.Y + 7 - i / 2f, frame.Width + i, frame.Height + i);
                    }
                    graphics.FillRectangle(shadow, frame.X + 5, frame.Y + 7, frame.Width, frame.Height);
                }
                if (border > 0)
                {
                    using var white = new Drawing.SolidBrush(Drawing.Color.White);
                    graphics.FillRectangle(white, frame);
                }
                graphics.DrawImage(oriented, new Drawing.Rectangle(pad, pad, width, height));
            }
            var rect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            try
            {
                var stride = Math.Abs(data.Stride);
                var bytes = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                return new PreparedPixels(bitmap.Width, bitmap.Height, stride, bytes);
            }
            finally { bitmap.UnlockBits(data); }
        }
        catch { return null; }
    }

    private static void ApplyOrientation(Drawing.Bitmap bitmap, Drawing.Image source)
    {
        const int orientationId = 0x0112;
        if (!source.PropertyIdList.Contains(orientationId)) return;
        var orientation = source.GetPropertyItem(orientationId)?.Value;
        var value = orientation is { Length: > 0 } ? orientation[0] : (byte)1;
        var flip = value switch
        {
            2 => Drawing.RotateFlipType.RotateNoneFlipX,
            3 => Drawing.RotateFlipType.Rotate180FlipNone,
            4 => Drawing.RotateFlipType.Rotate180FlipX,
            5 => Drawing.RotateFlipType.Rotate90FlipX,
            6 => Drawing.RotateFlipType.Rotate90FlipNone,
            7 => Drawing.RotateFlipType.Rotate270FlipX,
            8 => Drawing.RotateFlipType.Rotate270FlipNone,
            _ => Drawing.RotateFlipType.RotateNoneFlipNone
        };
        bitmap.RotateFlip(flip);
    }

    public void SaveSnapshot(string destination)
    {
        Root.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(Root.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(Root.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(destination);
        encoder.Save(stream);
    }

    public void Dispose()
    {
        disposed = true;
        generation++;
        ready.Clear();
        visible.Clear();
        Root.Children.Clear();
    }

    private sealed record PreparedPixels(int Width, int Height, int Stride, byte[] Bytes);
    private sealed record PreparedCard(BitmapSource Source, double Width, double Height);
}
