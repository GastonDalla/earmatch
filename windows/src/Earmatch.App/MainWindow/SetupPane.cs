using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Earmatch.App.Controls;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The Setup pane (SetupPane.swift): language, hotkeys and the About card, with Check for Updates… in installed copies.
/// Windows needs no permissions, so the macOS permissions card is gone.
public sealed class SetupPane : PaneView
{
    private readonly SegmentedPicker<AppLanguageChoice> _language;
    private readonly TextBlock _languageExplanation = Parts.Explanation("");
    private ActionButton? _updates;
    private TextBlock? _updatesLabel;

    public SetupPane(MainWindowModel model) : base(model, MainWindowPane.Setup.Title)
    {
        _language = new SegmentedPicker<AppLanguageChoice>(WindowText.Language,
            AppLanguageChoice.AllCases.Select(choice => (choice.Label, choice)).ToList(), choice => choice.Explanation, model.Language);
        _language.SelectionChanged += choice => Dispatcher.BeginInvoke(() => Model.SelectLanguage(choice), DispatcherPriority.Background);
        var languageCard = PaneCard.Make(WindowText.LanguageSection, null, Ui.VStack(6, _language, _languageExplanation));

        var about = AboutCard();
        about.VerticalAlignment = VerticalAlignment.Stretch;
        SetContent(Columns(Column(languageCard, HotkeysCard()), about));
        Refresh();
    }

    public void FocusLanguage() => System.Windows.Input.Keyboard.Focus(_language);

    public override void Refresh()
    {
        _language.Selected = Model.Language;
        _languageExplanation.Text = Model.Language.Explanation;
        RefreshUpdates();
    }

    /// The update button follows the menus' item: hidden in development builds, Update Available… when an update
    /// waits, and dimmed while it downloads.
    private void RefreshUpdates()
    {
        if (_updates is null || _updatesLabel is null) return;
        var updates = Model.Updates;
        _updates.Visibility = updates.ShowsMenuItem ? Visibility.Visible : Visibility.Collapsed;
        _updates.IsEnabled = updates.IsMenuItemEnabled;
        _updates.Opacity = updates.IsMenuItemEnabled ? 1 : 0.5;
        _updatesLabel.Text = updates.MenuTitle;
        _updates.ToolTip = updates.MenuTitle;
        System.Windows.Automation.AutomationProperties.SetName(_updates, updates.MenuTitle);
    }

    private static Border HotkeysCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Spacing.Medium) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        (string Key, string Text)[] rows =
        [
            (WindowText.RightAltKey, WindowText.PeekHint),
            (WindowText.ToggleKeys, WindowText.ToggleHint),
            (WindowText.SectionKeys, WindowText.SwitchSectionsHint),
        ];
        for (var index = 0; index < rows.Length; index++)
        {
            if (index > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var key = Parts.KeyCap(rows[index].Key);
            key.HorizontalAlignment = HorizontalAlignment.Right;
            key.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(key, index * 2);
            grid.Children.Add(key);
            var text = Ui.Text(rows[index].Text, TypeStyle.Caption, WindowTheme.SecondaryText, wraps: true);
            Grid.SetRow(text, index * 2);
            Grid.SetColumn(text, 2);
            grid.Children.Add(text);
        }
        return PaneCard.Make(WindowText.HotkeysSection, null, grid);
    }

    private Border AboutCard()
    {
        var icon = new Image { Source = AppIcon.Large, Width = 72, Height = 72, HorizontalAlignment = HorizontalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var name = Ui.Text("Earmatch", TypeStyle.Title, WindowTheme.PrimaryText);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.Margin = new Thickness(0, Spacing.Small, 0, 0);
        var version = Ui.Text(Model.VersionLabel, TypeStyle.Status, WindowTheme.SecondaryText);
        version.HorizontalAlignment = HorizontalAlignment.Center;
        version.Margin = new Thickness(0, Spacing.Tight, 0, 0);

        TextBlock? copyLabel = null;
        Icon? copyIcon = null;
        DispatcherTimer? reset = null;
        var copy = ActionButton.WithIcon(ActionKind.Pill, WindowText.CopyDiagnostics, IconKind.Clipboard, () =>
        {
            var copied = Model.CopyDiagnostics();
            if (copyLabel is null || copyIcon is null) return;
            copyLabel.Text = copied ? WindowText.Copied : WindowText.CopyFailed;
            copyIcon.Kind = copied ? IconKind.Checkmark : IconKind.Warning;
            reset?.Stop();
            reset = new DispatcherTimer { Interval = TimeSpan.FromSeconds(copied ? 1.6 : 3) };
            reset.Tick += (_, _) =>
            {
                reset.Stop();
                copyLabel.Text = WindowText.CopyDiagnostics;
                copyIcon.Kind = IconKind.Clipboard;
            };
            reset.Start();
        }, WindowText.CopyDiagnosticsHelp);
        copyLabel = FindFirst<TextBlock>(copy);
        copyIcon = FindFirst<Icon>(copy);
        _updates = ActionButton.WithIcon(ActionKind.Pill, Model.Updates.MenuTitle, IconKind.ArrowDownCircle, () => Model.CheckForUpdates());
        _updatesLabel = FindFirst<TextBlock>(_updates);
        var quit = ActionButton.Plain(ActionKind.Pill, WindowText.Quit, () => Model.Quit());
        var buttons = Ui.VStack(Spacing.Small, _updates, copy, quit);
        buttons.HorizontalAlignment = HorizontalAlignment.Center;
        buttons.Margin = new Thickness(0, Spacing.Large, 0, 0);

        var top = Ui.VStack(0, icon, name, version, buttons);
        var credit = CreditLines();
        var layout = new Grid { Margin = new Thickness(0, Spacing.Small, 0, Spacing.Small) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = Spacing.Large });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(top);
        Grid.SetRow(credit, 2);
        layout.Children.Add(credit);
        var card = PaneCard.Wrap(layout);
        card.VerticalAlignment = VerticalAlignment.Stretch;
        return card;
    }

    /// The credit link and the independence line.
    public static FrameworkElement CreditLines()
    {
        var (before, after) = Credit.Parts;
        var line = Ui.Text("", TypeStyle.Body, WindowTheme.SecondaryText);
        line.Inlines.Add(new Run(before));
        line.Inlines.Add(new Run(Credit.Author) { FontWeight = FontWeights.SemiBold, Foreground = WindowTheme.Accent });
        line.Inlines.Add(new Run(after));
        var arrow = new Icon(IconKind.ArrowUpRight, 10, WindowTheme.Solid(WindowTheme.AccentColor, 0.8), 2.2) { VerticalAlignment = VerticalAlignment.Center };
        var link = new ActionButton(ActionKind.Link, Credit.Help, OpenProfile, Ui.HStack(5, line, arrow));
        var independence = Ui.Text(WindowText.Independence, new TypeStyle(11.5, FontWeights.Normal), WindowTheme.SectionText, wraps: true);
        independence.TextAlignment = TextAlignment.Center;
        independence.HorizontalAlignment = HorizontalAlignment.Center;
        var stack = Ui.VStack(Spacing.Tight, link, independence);
        return stack;
    }

    public static void OpenProfile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Credit.Url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No browser to open; the link stays in the tooltip.
        }
    }

    private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) return match;
        IEnumerable<DependencyObject> children = root switch
        {
            Border { Child: { } child } => [child],
            Panel panel => panel.Children.Cast<DependencyObject>(),
            _ => [],
        };
        foreach (var child in children)
        {
            if (FindFirst<T>(child) is { } found) return found;
        }
        return null;
    }
}
