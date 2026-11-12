using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Earmatch.App.Panes;
using Earmatch.Core;

namespace Earmatch.App;

public static class WindowRenderCommand
{
    public static int Run(WindowRenderRequest request)
    {
        try
        {
            Directory.CreateDirectory(request.Directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cannot create {request.Directory}: {exception.Message}");
            return 1;
        }
        var now = DateTimeOffset.Now;
        var overview = WindowRenderSample.PracticeOverview(now, TimeZoneInfo.Local);
        foreach (var shot in WindowRenderShot.All)
        {
            var path = Path.Combine(request.Directory, shot.FileName);
            try
            {
                File.WriteAllBytes(path, Render(shot, request.Size, overview, now));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Cannot write {path}: {exception.Message}");
                return 1;
            }
            Console.WriteLine(path);
        }
        return 0;
    }

    private static byte[] Render(WindowRenderShot shot, WindowSize size, PracticeOverview overview, DateTimeOffset now)
    {
        var model = new MainWindowModel(WindowRenderSample.Settings, shot.Pane, WindowRenderSample.StatusContext(shot.DeckCount),
            WindowRenderSample.Decks(shot.DeckCount), overview, WindowRenderSample.PracticeLive, now);
        var frame = new MainWindowFrame(model) { Width = size.Width, Height = size.Height };
        frame.Measure(new Size(size.Width, size.Height));
        frame.Arrange(new Rect(0, 0, size.Width, size.Height));
        frame.UpdateLayout();
        foreach (var pane in Descendants<PaneView>(frame)) pane.Tick(now);
        frame.UpdateLayout();

        var scale = WindowRenderShot.Scale;
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(frame);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
