using System.Windows.Controls;
using Earmatch.App.Theme;

namespace Earmatch.App.Panes;

public sealed class MainWindowFrame : Grid
{
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
