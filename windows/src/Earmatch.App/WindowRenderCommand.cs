using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Earmatch.App.Controls;
using Earmatch.App.Panes;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App;

/// `Earmatch --render-window <dir>` (WindowRenderCommand.swift): renders every WindowRenderShot of the sample
/// session to PNG at twice the window's size, so it can be compared with the macOS renders, and exits.
public static class WindowRenderCommand
{
    /// The Windows 11 title bar above the client area; renders include a sketch of it, as macOS renders sketch the
    /// traffic lights, so both are the window's full size.
    public const double TitleBarHeight = 32;

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
        var frame = new Grid { Background = WindowTheme.Background, Width = size.Width, Height = size.Height };
        frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TitleBarHeight) });
        frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        frame.Children.Add(TitleBarSketch());
        var view = new MainWindowView(model);
        Grid.SetRow(view, 1);
        frame.Children.Add(view);
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

    /// The dark Windows 11 title bar the window gets: the icon and title on the left, the caption buttons on the right.
    private static Grid TitleBarSketch()
    {
        var bar = new Grid { Background = WindowTheme.Sidebar };
        var icon = new Image { Source = AppIcon.Large, Width = 16, Height = 16 };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var title = Ui.Text("Earmatch", new TypeStyle(12, FontWeights.Normal), WindowTheme.White(0.92));
        var left = Ui.HStack(10, icon, title);
        left.Margin = new Thickness(14, 0, 0, 0);
        left.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(left);
        var buttons = Ui.HStack(0, CaptionButton(""), CaptionButton(""), CaptionButton(""));
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        bar.Children.Add(buttons);
        return bar;
    }

    private static FrameworkElement CaptionButton(string glyph)
    {
        var text = Ui.Text(glyph, new TypeStyle(10, FontWeights.Normal), WindowTheme.White(0.92));
        text.FontFamily = AppFonts.Icons;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        return new Border { Width = 46, Height = TitleBarHeight, Child = text };
    }
}
