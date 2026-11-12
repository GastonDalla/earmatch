using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Earmatch.App.Controls;
using Earmatch.App.Drawing;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The Live pane (LivePane.swift): the on/off switch, the booth presets, the live decks mirror and the hotkeys.
public sealed class LivePane : PaneView
{
    private readonly ConnectionChip _connection = new();
    private readonly AccentSwitch _enable;
    private readonly CardStatusRow _enableStatus;
    private readonly SegmentedPicker<BoothPreset?> _booth;
    private readonly TextBlock _boothExplanation;
    private readonly Border _boothCard;
    private readonly LiveDecksPanel _decks;

    public LivePane(MainWindowModel model) : base(model, MainWindowPane.Live.Title)
    {
        SetAccessory(_connection);

        var (enableRow, enable) = Parts.SwitchRow(WindowText.Enable, WindowText.EnableHelp, WindowText.EnableHint);
        _enable = enable;
        _enable.Toggled += on => Model.Update((ref OverlaySettings settings) => settings.IsEnabled = on);
        _enableStatus = new CardStatusRow(new StatusLine(StatusTone.Off, ""));
        var enableCard = PaneCard.Filling(null, null, enableRow, _enableStatus);
        enableCard.ToolTip = WindowText.EnableHelp;

        _booth = new SegmentedPicker<BoothPreset?>(OptionText.BoothTitle,
            BoothPreset.AllCases.Select(preset => (preset.DisplayName, (BoothPreset?)preset)).ToList(),
            preset => preset?.Explanation ?? BoothPreset.CustomExplanation, BoothPreset.Selected(model.Settings));
        _booth.SelectionChanged += preset =>
        {
            if (preset is { } chosen) Model.Update((ref OverlaySettings settings) => chosen.Apply(ref settings));
        };
        _boothExplanation = Parts.Explanation("");
        var boothSubtitle = Ui.Text(OptionText.BoothSubtitle, TypeStyle.Caption, WindowTheme.SectionText, wraps: true);
        boothSubtitle.Margin = new Thickness(Spacing.Tight, 0, 0, 0);
        _boothCard = PaneCard.Filling(OptionText.BoothSectionTitle, null,
            Ui.VStack(10, Ui.VStack(6, _booth, _boothExplanation), boothSubtitle), null);

        var cards = Columns(enableCard, _boothCard);
        _decks = new LiveDecksPanel(model);

        var hints = Ui.HStack(Spacing.Large, Parts.HotkeyHint(WindowText.RightAltKey, WindowText.PeekHint),
            Parts.HotkeyHint(WindowText.ToggleKeys, WindowText.ToggleHint));
        AutomationProperties.SetName(hints, WindowText.HotkeysSpoken);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 12 });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(cards);
        SetRow(_decks, 2);
        content.Children.Add(_decks);
        SetRow(hints, 4);
        content.Children.Add(hints);
        SetContent(content);
        Refresh();
    }

    public override void Refresh()
    {
        var settings = Model.Settings;
        _connection.Line = StatusText.Connection(Model.Status);
        _enable.IsOn = settings.IsEnabled;
        var status = Model.WindowStatus;
        _enableStatus.Line = new StatusLine(status.Tone, status.Title);
        var selected = BoothPreset.Selected(settings);
        _booth.Selected = selected;
        _boothExplanation.Text = selected?.Explanation ?? BoothPreset.CustomExplanation;
        _boothCard.Opacity = settings.IsEnabled ? 1 : 0.55;
        _decks.Refresh();
    }
}

/// The pill at the top right of the Live pane with rekordbox's connection.
public sealed class ConnectionChip : Border
{
    private readonly System.Windows.Shapes.Ellipse _dot = new() { Width = 8, Height = 8 };
    private readonly TextBlock _text = Ui.Text("", TypeStyle.Body, WindowTheme.White(0.92 * 0.88));

    public ConnectionChip()
    {
        CornerRadius = new CornerRadius(10);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(12 - 1, 5 - 1, 12 - 1, 5 - 1);
        Child = Ui.HStack(Spacing.Small, _dot, _text);
    }

    public StatusLine Line
    {
        set
        {
            var colour = WindowTheme.ColorFor(value.Tone);
            _dot.Fill = WindowTheme.Solid(colour);
            _text.Text = value.Text;
            Background = WindowTheme.Solid(colour, 0.10);
            BorderBrush = WindowTheme.Solid(colour, 0.22);
            ToolTip = value.Text;
            AutomationProperties.SetName(this, value.Text);
        }
    }
}

/// The live decks mirror (LiveDecksPanel.swift): each deck's title and phase meter while rekordbox is mirrored.
public sealed class LiveDecksPanel : Border
{
    private readonly MainWindowModel _model;
    private readonly ContentControl _body = new() { Focusable = false };
    private readonly CardStatusRow _status = new(new StatusLine(StatusTone.Waiting, ""));
    private readonly Dictionary<int, (PhaseMeterCanvas Canvas, Border Screen)> _meters = [];
    private readonly Dictionary<int, LiveDeckSnapshot> _latest = [];
    private string _layoutKey = "";

    public LiveDecksPanel(MainWindowModel model)
    {
        _model = model;
        var card = PaneCard.Make(WindowText.LiveDecks, null, _body, _status);
        Child = card;
    }

    public void Refresh()
    {
        var line = StatusText.LiveDecks(_model.Status, _model.Snapshot);
        _status.Line = line;
        var decks = _model.Decks;
        var showsMeter = _model.Settings.IsPhaseMeterEnabled;
        var isConnected = line.Tone == StatusTone.Live;
        var key = isConnected ? string.Join(",", decks.Select(deck => $"{deck.Number}:{deck.Title}:{deck.IsMaster}:{deck.Meter is null}")) + showsMeter : "empty";
        if (key != _layoutKey)
        {
            _layoutKey = key;
            _meters.Clear();
            _body.Content = isConnected ? DeckGrid(decks, showsMeter) : EmptyDecks();
        }
        foreach (var deck in decks)
        {
            _latest[deck.Number] = deck;
            if (_meters.TryGetValue(deck.Number, out var meter)) meter.Canvas.Display = deck.Meter;
        }
    }

    private Grid DeckGrid(IReadOnlyList<LiveDeckSnapshot> decks, bool showsMeter)
    {
        var columns = decks.Count > 2 ? 2 : 1;
        var grid = new Grid();
        for (var column = 0; column < columns; column++)
        {
            if (column > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Spacing.Medium) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var rows = (decks.Count + columns - 1) / columns;
        for (var row = 0; row < rows; row++)
        {
            if (row > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (var index = 0; index < decks.Count; index++)
        {
            var view = DeckRow(decks[index], showsMeter);
            Grid.SetRow(view, index / columns * 2);
            Grid.SetColumn(view, index % columns * 2);
            grid.Children.Add(view);
        }
        return grid;
    }

    private FrameworkElement DeckRow(LiveDeckSnapshot deck, bool showsMeter)
    {
        var title = Ui.Text(deck.Title ?? WindowText.NoTrack, TypeStyle.BodyStrong, deck.Title is null ? WindowTheme.SecondaryText : WindowTheme.PrimaryText);
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(DeckBadge(deck.Number, isDimmed: false));
        title.Margin = new Thickness(Spacing.Small, 0, Spacing.Small, 0);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        if (deck.IsMaster)
        {
            var chip = SyncMasterChip();
            Grid.SetColumn(chip, 2);
            header.Children.Add(chip);
        }
        var canvas = new PhaseMeterCanvas { Display = deck.Meter };
        UIElement inside = !showsMeter ? Caption(WindowText.MeterOff) : deck.Meter is null ? Caption(WindowText.ReadingGrid) : canvas;
        var screen = new Border { Height = 60, Background = WindowTheme.Black, CornerRadius = new CornerRadius(6), Child = inside };
        if (showsMeter) _meters[deck.Number] = (canvas, screen);
        var number = deck.Number;
        return new SpokenGroup(Ui.VStack(Spacing.Small, header, screen),
            () => MeterSpeech.DeckRow(_latest.GetValueOrDefault(number, deck), _model.Settings.IsPhaseMeterEnabled));
    }

    private static TextBlock Caption(string text)
    {
        var caption = Ui.Text(text, TypeStyle.Status, WindowTheme.SectionText);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        return caption;
    }

    private static FrameworkElement EmptyDecks()
    {
        var badges = Ui.HStack(Spacing.Tight, DeckBadge(1, isDimmed: true), DeckBadge(2, isDimmed: true));
        var hint = Ui.Text(WindowText.EmptyDecksHint, TypeStyle.Caption, WindowTheme.SecondaryText, wraps: true);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(badges);
        hint.Margin = new Thickness(Spacing.Medium, 0, 0, 0);
        Grid.SetColumn(hint, 1);
        row.Children.Add(hint);
        return row;
    }

    public static Border DeckBadge(int number, bool isDimmed)
    {
        var text = Ui.Text(number.ToString(System.Globalization.CultureInfo.InvariantCulture), new TypeStyle(12, FontWeights.Bold),
            isDimmed ? WindowTheme.SectionText : WindowTheme.PrimaryText);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        var badge = Ui.Surface(text, 6, WindowTheme.Well, WindowTheme.White(isDimmed ? 0.06 : 0.14));
        badge.Width = 22;
        badge.Height = 22;
        return badge;
    }

    private static Border SyncMasterChip() => new()
    {
        CornerRadius = new CornerRadius(6),
        Background = WindowTheme.Solid(WindowTheme.MasterColor, 0.14),
        BorderBrush = WindowTheme.Solid(WindowTheme.MasterColor, 0.35),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(Spacing.Small - 1, 3 - 1, Spacing.Small - 1, 3 - 1),
        Child = new TrackedText(WindowText.SyncMasterChip, new TypeStyle(10, FontWeights.Bold), 1.2, WindowTheme.Master),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
