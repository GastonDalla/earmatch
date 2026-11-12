using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The sidebar (MainWindowSidebar.swift): the app's icon, name and version, the four sections with their shortcuts,
/// and Earmatch's status at the bottom. Up and Down move between sections while the list has the keyboard.
public sealed class Sidebar : Grid
{
    private readonly MainWindowModel _model;
    private readonly List<(MainWindowPane Pane, Border Row, Icon Icon, TextBlock Title, Border Ring)> _rows = [];
    private readonly Stack _list = new() { Spacing = 2 };
    private readonly Border _status;
    private readonly System.Windows.Shapes.Ellipse _statusDot = new() { Width = 8, Height = 8 };
    private readonly TextBlock _statusTitle = Ui.Text("", TypeStyle.Caption, WindowTheme.White(0.92 * 0.85), wraps: true);

    public Sidebar(MainWindowModel model)
    {
        _model = model;
        Width = MainWindowMetrics.SidebarWidth;
        Background = WindowTheme.Sidebar;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var brand = Brand(model.VersionLabel);
        // macOS leaves 48 points for the traffic lights over the sidebar; on Windows that band drags the window.
        brand.Margin = new Thickness(12 + 6, MainWindowFrame.TitleBandHeight, 12, Spacing.Large);
        Children.Add(brand);

        foreach (var pane in MainWindowPane.AllCases) _list.Children.Add(MakeRow(pane));
        _list.Margin = new Thickness(12, 0, 12, 0);
        _list.Focusable = true;
        _list.FocusVisualStyle = null;
        AutomationProperties.SetName(_list, WindowText.Sections);
        _list.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Up or Key.Down)) return;
            _model.Select(_model.Pane.Moved(e.Key == Key.Up ? -1 : 1));
            e.Handled = true;
        };
        _list.IsKeyboardFocusedChanged += (_, _) => Refresh();
        SetRow(_list, 1);
        Children.Add(_list);

        _statusTitle.MaxHeight = AppFonts.LineHeight(12) * 2;
        _status = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10 - 1, Spacing.Small - 1, 10 - 1, Spacing.Small - 1),
            Margin = new Thickness(12, Spacing.Medium, 12, Spacing.Medium),
            Child = Ui.HStack(Spacing.Small, _statusDot, _statusTitle),
        };
        SetRow(_status, 3);
        Children.Add(_status);
        Refresh();
    }

    /// Set by the window while the keyboard drives it, so the focus ring shows only then.
    public static bool IsKeyboardNavigating { get; set; }

    private static FrameworkElement Brand(string versionLabel)
    {
        var icon = new Image { Source = AppIcon.Large, Width = 32, Height = 32 };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var name = Ui.Text("Earmatch", new TypeStyle(15, FontWeights.Bold), WindowTheme.PrimaryText);
        var version = Ui.Text(versionLabel, new TypeStyle(11, FontWeights.Normal, IsMono: true), WindowTheme.SecondaryText);
        var brand = Ui.HStack(10, icon, Ui.VStack(1, name, version));
        AutomationProperties.SetName(brand, $"Earmatch {versionLabel}");
        return brand;
    }

    private Border MakeRow(MainWindowPane pane)
    {
        var icon = new Icon(IconFor(pane), pane == MainWindowPane.Setup ? 17 : 16.5, WindowTheme.SecondaryText, 1.6)
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var iconBox = new Border { Width = 20, Child = icon };
        var title = Ui.Text(pane.Title, TypeStyle.BodyStrong, WindowTheme.PrimaryText);
        title.Margin = new Thickness(10, 0, 0, 0);
        var shortcut = Ui.Text($"Ctrl+{pane.ShortcutNumber}", new TypeStyle(11, FontWeights.Medium), WindowTheme.SectionText);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(iconBox);
        SetColumn(title, 1);
        grid.Children.Add(title);
        SetColumn(shortcut, 2);
        grid.Children.Add(shortcut);
        var ring = new Border
        {
            BorderBrush = WindowTheme.Accent,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(-1),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };
        var content = new Grid();
        content.Children.Add(new Border { Padding = new Thickness(10, 0, 10, 0), Child = grid });
        content.Children.Add(ring);
        var row = new Border { Height = 34, CornerRadius = new CornerRadius(9), Child = content, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        row.MouseLeftButtonUp += (_, _) => _model.Select(pane);
        AutomationProperties.SetName(row, pane.Title);
        AutomationProperties.SetHelpText(row, WindowText.Shortcut(pane.ShortcutNumber));
        _rows.Add((pane, row, icon, title, ring));
        return row;
    }

    private static IconKind IconFor(MainWindowPane pane) => pane switch
    {
        MainWindowPane.Live => IconKind.Live,
        MainWindowPane.Overlay => IconKind.Overlay,
        MainWindowPane.Practice => IconKind.Metronome,
        _ => IconKind.Gear,
    };

    public void Refresh()
    {
        foreach (var (pane, row, icon, title, ring) in _rows)
        {
            var isSelected = pane == _model.Pane;
            row.Background = isSelected ? WindowTheme.Solid(WindowTheme.AccentColor, 0.13) : Brushes.Transparent;
            icon.Brush = isSelected ? WindowTheme.Accent : WindowTheme.SecondaryText;
            title.Foreground = isSelected ? WindowTheme.PrimaryText : WindowTheme.White(0.92 * 0.75);
            ring.Visibility = isSelected && _list.IsKeyboardFocused && IsKeyboardNavigating ? Visibility.Visible : Visibility.Hidden;
        }
        var status = _model.WindowStatus;
        var colour = WindowTheme.ColorFor(status.Tone);
        _statusDot.Fill = WindowTheme.Solid(colour);
        _statusTitle.Text = status.Title;
        _status.Background = WindowTheme.Solid(colour, 0.10);
        _status.BorderBrush = WindowTheme.Solid(colour, 0.22);
        var detail = StatusText.Connection(_model.Status).Text;
        _status.ToolTip = detail;
        AutomationProperties.SetName(_status, WindowText.Status(status.Title));
        AutomationProperties.SetHelpText(_status, detail);
    }

    public void FocusList() => _list.Focus();
}
