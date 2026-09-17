using System.IO;
using System.Windows.Media.Imaging;

namespace ScrapbookSaver;

internal static class AppArtwork
{
    public static Icon Icon { get; } = LoadIcon();
    public static BitmapSource WindowIcon { get; } = LoadWindowIcon();

    private static Icon LoadIcon()
    {
        using var stream = typeof(AppArtwork).Assembly.GetManifestResourceStream("Scrapbook.Icon")
            ?? throw new InvalidOperationException("Missing embedded application icon.");
        using var icon = new Icon(stream, SystemInformation.SmallIconSize);
        return (Icon)icon.Clone();
    }

    private static BitmapSource LoadWindowIcon()
    {
        using var stream = typeof(AppArtwork).Assembly.GetManifestResourceStream("Scrapbook.Artwork")
            ?? throw new InvalidOperationException("Missing embedded application artwork.");
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
