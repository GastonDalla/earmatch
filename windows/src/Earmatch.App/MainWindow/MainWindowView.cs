using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earmatch.App.Practice;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

public sealed class MainWindowView : Grid
{
    private readonly MainWindowModel _model;
    private Sidebar _sidebar = null!;
    private PaneView _pane = null!;
    private bool _isTicking;

    public MainWindowView(MainWindowModel model)
    {
        _model = model;
        Background = WindowTheme.Background;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Auto);
        UseLayoutRounding = true;
        Build();
        model.Changed += () =>
        {
            _sidebar.Refresh();
            _pane.Refresh();
        };
        model.PaneChanged += () =>
        {
            _sidebar.Refresh();
            ShowPane();
        };
        model.LanguageChanged += Build;
        Loaded += (_, _) => SetTicking(true);
        Unloaded += (_, _) => SetTicking(false);
        IsVisibleChanged += (_, _) => SetTicking(IsVisible);
    }

    public Sidebar Sidebar => _sidebar;

    private void Build()
    {
        var keepsFocus = _pane?.IsKeyboardFocusWithin == true;
        Children.Clear();
        _sidebar = new Sidebar(_model);
        Children.Add(_sidebar);
        var hairline = new Border { Background = WindowTheme.Hairline };
        SetColumn(hairline, 1);
        Children.Add(hairline);
        ShowPane();
        if (keepsFocus && _pane is SetupPane setup) Dispatcher.BeginInvoke(setup.FocusLanguage, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ShowPane()
    {
        if (_pane is not null && Children.Contains(_pane)) Children.Remove(_pane);
        _pane = _model.Pane switch
        {
            MainWindowPane.Live => new LivePane(_model),
            MainWindowPane.Overlay => new OverlayPane(_model),
            MainWindowPane.Practice => new PracticePane(_model),
            _ => new SetupPane(_model),
        };
        SetColumn(_pane, 2);
        Children.Add(_pane);
    }

    private void SetTicking(bool on)
    {
        if (on == _isTicking) return;
        _isTicking = on;
        if (on) CompositionTarget.Rendering += OnFrame;
        else CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e) => _pane.Tick(DateTimeOffset.Now);
}
