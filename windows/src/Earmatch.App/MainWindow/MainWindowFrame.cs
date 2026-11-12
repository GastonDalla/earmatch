using System.Windows.Controls;
using Earmatch.App.Theme;

namespace Earmatch.App.Panes;

/// The window's whole client area, as the window shows it and `--render-window` draws it: the content reaches the top
/// edge like the macOS window's full-size content view (the sidebar's colour under the title bar band), with the
/// Windows caption buttons over its top-right corner instead of the traffic lights at the top left.
public sealed class MainWindowFrame : Grid
{
    /// The band at the top that drags the window, like the macOS title bar: the sidebar keeps it free (its brand starts
    /// below it, where macOS leaves room for the traffic lights) and the pane header sits in it.
    public const double TitleBandHeight = 48;

    public MainWindowFrame(MainWindowModel model)
    {
        Background = WindowTheme.Background;
        View = new MainWindowView(model);
        Children.Add(View);
        Children.Add(Captions);
        model.LanguageChanged += Captions.Relabel;
    }

    public MainWindowView View { get; }

    public CaptionButtons Captions { get; } = new();
}
