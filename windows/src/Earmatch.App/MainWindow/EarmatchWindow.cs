using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The main window. Closing it hides it, like the macOS window: Earmatch keeps running in the tray until Quit.
public sealed class EarmatchWindow : Window
{
    private readonly MainWindowModel _model;
    private readonly MainWindowView _view;

    public EarmatchWindow(MainWindowModel model)
    {
        _model = model;
        _view = new MainWindowView(model);
        Title = "Earmatch";
        Icon = AppIcon.WindowIcon;
        Width = MainWindowMetrics.DefaultSize.Width;
        Height = MainWindowMetrics.DefaultSize.Height;
        MinWidth = MainWindowMetrics.MinimumSize.Width;
        MinHeight = MainWindowMetrics.MinimumSize.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = WindowTheme.Background;
        Content = _view;
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this, WindowTheme.SidebarColor, System.Windows.Media.Color.FromRgb(235, 235, 235));
    }

    public bool IsQuitting { get; set; }

    public void ShowAndActivate()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (IsQuitting) return;
        e.Cancel = true;
        Hide();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Space)
        {
            if (!Sidebar.IsKeyboardNavigating)
            {
                Sidebar.IsKeyboardNavigating = true;
                _view.Sidebar.Refresh();
            }
        }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        var digit = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => (char)('1' + (e.Key - Key.D1)),
            >= Key.NumPad1 and <= Key.NumPad9 => (char)('1' + (e.Key - Key.NumPad1)),
            _ => '\0',
        };
        if (digit != '\0' && MainWindowPane.FromKeyEquivalent(digit.ToString()) is { } pane)
        {
            _model.Select(pane);
            e.Handled = true;
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (!Sidebar.IsKeyboardNavigating) return;
        Sidebar.IsKeyboardNavigating = false;
        _view.Sidebar.Refresh();
    }
}
