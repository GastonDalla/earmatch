using System.Windows;
using System.Windows.Media.Imaging;

namespace Earmatch.App;

public static class AppIcon
{
    private static BitmapImage? _large;

    public static BitmapImage Large => _large ??= Load("Assets/AppIcon.png");

    public static System.Drawing.Icon TrayIcon(int size)
    {
        var resource = Application.GetResourceStream(Uri("Assets/AppIcon.ico"))
            ?? throw new InvalidOperationException("The app icon resource is missing.");
        using var stream = resource.Stream;
        return new System.Drawing.Icon(stream, size, size);
    }

    public static BitmapFrame WindowIcon => BitmapFrame.Create(Uri("Assets/AppIcon.ico"));

    private static BitmapImage Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = Uri(path);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static Uri Uri(string path) => new($"pack://application:,,,/Earmatch;component/{path}", UriKind.Absolute);
}
