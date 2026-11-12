using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Drawing;
using Earmatch.App.Panes;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Practice;

/// The Practice pane (PracticePane.swift): the mix trainer switch, the transition being timed, the last mix, its replay
/// and the progress day by day.
public sealed class PracticePane : PaneView
{
    private readonly Grid _columns;
    private readonly AccentSwitch _trainer;
    private readonly ContentControl _session = new() { Focusable = false };
    private readonly ContentControl _history = new() { Focusable = false, VerticalContentAlignment = VerticalAlignment.Stretch };
    private string _key = "";

    public PracticePane(MainWindowModel model) : base(model, MainWindowPane.Practice.Title)
    {
        var (trainerRow, trainer) = Parts.SwitchRow(OptionText.MixTrainerTitle, OptionText.MixTrainerSubtitle);
        _trainer = trainer;
        _trainer.Toggled += on => Model.Update((ref OverlaySettings settings) => settings.IsMixTrainerEnabled = on);
        _columns = Columns(Column(PaneCard.Make(null, null, trainerRow), _session), _history);
        SetContent(_columns);
        Refresh();
    }

    public override void Refresh()
    {
        _trainer.IsOn = Model.Settings.IsMixTrainerEnabled;
        _columns.Opacity = Model.Settings.IsEnabled ? 1 : 0.55;
        var overview = Model.PracticeOverview;
        var key = $"{overview.LastMix?.Id}:{overview.MixCount}:{Model.PracticeLive}";
        if (key == _key) return;
        _key = key;
        _session.Content = SessionCard(overview);
        _history.Content = History(overview);
    }

    private Border SessionCard(PracticeOverview overview)
    {
        var title = Ui.Text(PracticeStrings.Title, TypeStyle.Headline, WindowTheme.PrimaryText);
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level2);
        var heading = Ui.VStack(2, title, Ui.Text(PracticeStrings.Subtitle, TypeStyle.Caption, WindowTheme.SecondaryText));
        UIElement live = Model.PracticeLive is { } status ? LiveMix(status) : Waiting();
        var line = Model.PracticeLive is { } mixing && Model.IsMixing
            ? new StatusLine(StatusTone.Live, PracticeText.StatusMixing(mixing.OutgoingDeck, mixing.IncomingDeck))
            : new StatusLine(StatusTone.Waiting, PracticeText.StatusWaiting(overview.MixCount));
        return overview.LastMix is { } mix
            ? PaneCard.Make(null, null, heading, live, Ui.Divider(), Summary(mix), new CardStatusRow(line))
            : PaneCard.Make(null, null, heading, live, new CardStatusRow(line));
    }

    private static FrameworkElement LiveMix(PracticeLiveStatus live)
    {
        var pair = Ui.Text(PracticeText.DeckPair(live.OutgoingDeck, live.IncomingDeck), TypeStyle.BodyStrong, WindowTheme.PrimaryText);
        var elapsed = Ui.Text(PracticeText.Duration(live.ElapsedSeconds), TypeStyle.Status, WindowTheme.SecondaryText);
        var top = Ui.Row(pair, elapsed);

        var colour = live.IsMatched ? WindowTheme.AccentColor : WindowTheme.WaitingColor;
        var matchText = live.SecondsToMatch is { } seconds ? PracticeText.MatchedAfter(seconds) : PracticeText.Matching(live.TempoDifferenceBPM);
        var tempo = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = WindowTheme.Solid(colour, 0.10),
            BorderBrush = WindowTheme.Solid(colour, 0.22),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10 - 1, 4 - 1, 10 - 1, 4 - 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = Ui.HStack(Spacing.Small,
                new Icon(live.IsMatched ? IconKind.CheckCircleFill : IconKind.Metronome, 15, WindowTheme.Solid(colour), 1.8) { VerticalAlignment = VerticalAlignment.Center },
                Ui.Text(matchText, TypeStyle.Status, WindowTheme.White(0.92 * 0.88))),
        };

        var meter = new PracticeErrorMeter { Height = 34 };
        meter.Show(live.PhaseErrorMilliseconds, isDimmed: !live.IsMatched);
        var errorBrush = live.PhaseErrorMilliseconds is { } error && live.IsMatched ? PracticeErrorMeter.BrushFor(error) : WindowTheme.SecondaryText;
        var value = Ui.Text(live.PhaseErrorMilliseconds is { } shown ? PracticeText.Milliseconds(shown, signed: true) : PracticeText.NoValue,
            new TypeStyle(17, FontWeights.SemiBold, IsMono: true), errorBrush);
        value.Width = 76;
        value.TextAlignment = TextAlignment.Right;
        var meterRow = new Grid();
        meterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        meterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        meterRow.Children.Add(meter);
        value.Margin = new Thickness(Spacing.Medium, 0, 0, 0);
        Grid.SetColumn(value, 1);
        meterRow.Children.Add(value);

        var caption = !live.IsLongEnough ? PracticeStrings.WaitingForTempo
            : live.PhaseErrorMilliseconds is null && live.IsMatched ? PracticeStrings.NoPhase
            : PracticeText.AverageSinceMatch(live.MeanPhaseErrorMilliseconds);
        var view = Ui.VStack(Spacing.Small, top, tempo, meterRow, Ui.Text(caption, TypeStyle.Caption, WindowTheme.SecondaryText));
        AutomationProperties.SetName(view, $"{PracticeText.DeckPair(live.OutgoingDeck, live.IncomingDeck)}, {matchText}");
        return view;
    }

    private static FrameworkElement Waiting()
    {
        var icon = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Background = WindowTheme.Well,
            Child = new Icon(IconKind.Metronome, 20, WindowTheme.SectionText, 1.7) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        var texts = Ui.VStack(2, Ui.Text(PracticeStrings.WaitingTitle, TypeStyle.BodyStrong, WindowTheme.PrimaryText),
            Ui.Text(PracticeStrings.WaitingBody, TypeStyle.Caption, WindowTheme.SecondaryText, wraps: true));
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        icon.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(icon);
        texts.Margin = new Thickness(Spacing.Medium, 0, 0, 0);
        Grid.SetColumn(texts, 1);
        row.Children.Add(texts);
        return row;
    }

    private FrameworkElement Summary(PracticeMix mix)
    {
        TextBlock Detail() => Ui.Text(PracticeText.LastMixDetail(mix, Model.Now, TimeZoneInfo.Local), TypeStyle.Status, WindowTheme.SecondaryText);
        var heading = new FirstThatFits();
        heading.Children.Add(Ui.Row(Label(PracticeStrings.LastMix), Detail()));
        heading.Children.Add(Ui.VStack(Spacing.Tight, Label(PracticeStrings.LastMix), Detail()));

        static Brush ErrorBrush(double? milliseconds) =>
            milliseconds is { } value && value <= PracticeErrorMeter.InPhase ? WindowTheme.Accent : WindowTheme.PrimaryText;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Spacing.Small) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Spacing.Small) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Place(grid, Metric(PracticeStrings.TempoMatch, mix.SecondsToMatch is { } match ? PracticeText.Duration(match) : PracticeStrings.NotMatched,
            mix.SecondsToMatch is null ? WindowTheme.Waiting : WindowTheme.PrimaryText), 0, 0);
        Place(grid, Metric(PracticeStrings.Drift, mix.TempoDriftBPM is { } drift ? PracticeText.Bpm(drift) : PracticeText.NoValue, WindowTheme.PrimaryText), 0, 2);
        Place(grid, Metric(PracticeStrings.AverageError, mix.MeanPhaseErrorMilliseconds is { } mean ? PracticeText.Milliseconds(mean) : PracticeText.NoValue,
            ErrorBrush(mix.MeanPhaseErrorMilliseconds)), 2, 0);
        Place(grid, Metric(PracticeStrings.WorstError, mix.WorstPhaseErrorMilliseconds is { } worst ? PracticeText.Milliseconds(worst) : PracticeText.NoValue,
            ErrorBrush(mix.WorstPhaseErrorMilliseconds)), 2, 2);
        return Ui.VStack(Spacing.Small, heading, grid);
    }

    private static void Place(Grid grid, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static Border Metric(string title, string value, Brush colour) => Ui.Surface(
        Ui.VStack(2, Ui.Text(value, new TypeStyle(17, FontWeights.SemiBold), colour), Ui.Text(title, TypeStyle.Caption, WindowTheme.SecondaryText)),
        10, WindowTheme.Well, WindowTheme.Hairline, new Thickness(12 - 1, 10 - 1, 12 - 1, 10 - 1));

    public static FrameworkElement Label(string text)
    {
        var label = new TrackedText(text, TypeStyle.Label, 2.2, WindowTheme.SectionText) { VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(label, text);
        return label;
    }

    private UIElement History(PracticeOverview overview)
    {
        if (overview.LastMix is not { } mix) return EmptyHistory();
        var cards = new List<UIElement>();
        if (overview.LastTrace is { } trace && trace.MixID == mix.Id && trace.Samples.Count > 0) cards.Add(PaneCard.Make(null, null, new PracticeReplay(trace, 76)));
        if (overview.Days.Count > 0) cards.Add(PaneCard.Make(null, null, Progress(overview.Days)));
        return Column([.. cards]);
    }

    private FrameworkElement Progress(IReadOnlyList<PracticeDay> days)
    {
        var measured = days.Select(day => day.MedianPhaseErrorMilliseconds).OfType<double>().ToList();
        UIElement? trend = null;
        if (measured.Count > 1)
        {
            var (first, last) = (measured[0], measured[^1]);
            trend = Ui.Text(PracticeText.Trend(first, last), TypeStyle.Status,
                Math.Round(last, MidpointRounding.AwayFromZero) < Math.Round(first, MidpointRounding.AwayFromZero) ? WindowTheme.Accent : WindowTheme.SecondaryText);
        }
        var chart = new PracticeProgressView { Height = 76, Values = days.Select(day => day.MedianPhaseErrorMilliseconds).ToList() };
        var footer = Footer(PracticeText.ShortDay(days[0].Day, Model.Now, TimeZoneInfo.Local), PracticeStrings.MedianPerDay,
            PracticeText.ShortDay(days[^1].Day, Model.Now, TimeZoneInfo.Local));
        var well = Ui.Surface(Ui.VStack(6, chart, footer), 10, WindowTheme.Well, WindowTheme.Hairline, new Thickness(12 - 1));
        var view = Ui.VStack(Spacing.Small, Ui.Row(Label(PracticeStrings.Progress), trend), well);
        AutomationProperties.SetName(view, PracticeStrings.MedianPerDay);
        return view;
    }

    /// The three captions under a chart: left, centred and right.
    public static Grid Footer(string left, string centre, string right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var style = new TypeStyle(10.5, FontWeights.Normal);
        var leftText = Ui.Text(left, style, WindowTheme.SectionText);
        var centreText = Ui.Text(centre, style, WindowTheme.SectionText);
        centreText.HorizontalAlignment = HorizontalAlignment.Center;
        centreText.Margin = new Thickness(Spacing.Small, 0, Spacing.Small, 0);
        var rightText = Ui.Text(right, style, WindowTheme.SectionText);
        grid.Children.Add(leftText);
        Grid.SetColumn(centreText, 1);
        grid.Children.Add(centreText);
        Grid.SetColumn(rightText, 2);
        grid.Children.Add(rightText);
        return grid;
    }

    private static Border EmptyHistory()
    {
        var body = Ui.Text(PracticeStrings.EmptyBody, TypeStyle.Caption, WindowTheme.SecondaryText, wraps: true);
        body.TextAlignment = TextAlignment.Center;
        body.MaxWidth = 280;
        var title = Ui.Text(PracticeStrings.EmptyTitle, TypeStyle.Headline, WindowTheme.PrimaryText);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var icon = new Icon(IconKind.ChartUptrend, 28, WindowTheme.SectionText, 2) { HorizontalAlignment = HorizontalAlignment.Center };
        var stack = Ui.VStack(Spacing.Small, icon, title, body);
        stack.VerticalAlignment = VerticalAlignment.Center;
        stack.HorizontalAlignment = HorizontalAlignment.Center;
        var card = Ui.Surface(stack, 14, WindowTheme.Card, WindowTheme.Hairline, new Thickness(Spacing.Large - 1));
        card.VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(card, $"{PracticeStrings.EmptyTitle}. {PracticeStrings.EmptyBody}");
        return card;
    }
}
