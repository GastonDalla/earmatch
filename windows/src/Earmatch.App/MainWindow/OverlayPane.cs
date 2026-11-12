using System.Windows;
using System.Windows.Controls;
using Earmatch.App.Controls;
using Earmatch.App.Drawing;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The Overlay pane (OverlayPane.swift): BPM precision, waveform and phase meter cards with animated previews.
public sealed class OverlayPane : PaneView
{
    private readonly Grid _columns;
    private readonly AccentSwitch _limit;
    private readonly SegmentedPicker<BPMLimit> _bpmLimit;
    private readonly TextBlock _bpmExplanation = Parts.Explanation("");
    private readonly FrameworkElement _bpmOptions;
    private readonly AccentSwitch _blind;
    private readonly CardStatusRow _bpmStatus;
    private readonly BPMSampleView _bpmSample = new();

    private readonly PreviewPeekControl _waveformPeek = new();
    private readonly SegmentedPicker<WaveformMode> _waveform;
    private readonly TextBlock _waveformExplanation = Parts.Explanation("");
    private readonly WaveformSketch _waveformSketch = new();
    private readonly CardStatusRow _waveformStatus = new(new StatusLine(StatusTone.Off, ""));
    private bool _isPeekingWaveform;

    private readonly PreviewPeekControl _meterPeek = new();
    private readonly AccentSwitch _meterSwitch;
    private readonly SegmentedPicker<PhaseMeterType> _meterType;
    private readonly TextBlock _meterExplanation = Parts.Explanation("");
    private readonly FrameworkElement _meterOptions;
    private readonly SegmentedPicker<CounterStyle> _counter;
    private readonly TextBlock _counterExplanation = Parts.Explanation("");
    private readonly FrameworkElement _counterOptions;
    private readonly PhaseMeterCanvas _meterCanvas = new();
    private readonly CardStatusRow _meterStatus = new(new StatusLine(StatusTone.Off, ""));
    private bool _isPeekingMeter;

    public OverlayPane(MainWindowModel model) : base(model, MainWindowPane.Overlay.Title)
    {
        var settings = model.Settings;

        var (limitRow, limit) = Parts.SwitchRow(WindowText.LimitBPM, WindowText.LimitBPMSubtitle);
        _limit = limit;
        _limit.Toggled += on => Model.Update((ref OverlaySettings s) => s.IsBPMLimitEnabled = on);
        _bpmLimit = new SegmentedPicker<BPMLimit>(WindowText.BpmPrecision, BPMLimit.AllCases.Select(value => (value.DisplayName, value)).ToList(),
            value => value.Explanation, settings.BPMLimit);
        _bpmLimit.SelectionChanged += value => Model.Update((ref OverlaySettings s) => s.BPMLimit = value);
        _bpmOptions = Ui.VStack(6, _bpmLimit, _bpmExplanation);
        var (blindRow, blind) = Parts.SwitchRow(OptionText.BlindModeTitle, OptionText.BlindModeSubtitle);
        _blind = blind;
        _blind.Toggled += on => Model.Update((ref OverlaySettings s) => s.IsBlindModeEnabled = on);
        _bpmStatus = new CardStatusRow(new StatusLine(StatusTone.Off, ""), _bpmSample);
        var bpmCard = PaneCard.Make(WindowText.BpmPrecisionSection, null, limitRow, _bpmOptions, blindRow, _bpmStatus);

        _waveform = new SegmentedPicker<WaveformMode>(WindowText.Waveform, WaveformMode.AllCases.Select(value => (value.DisplayName, value)).ToList(),
            value => value.Explanation, settings.WaveformMode);
        _waveform.SelectionChanged += value => Model.Update((ref OverlaySettings s) => s.WaveformMode = value);
        _waveformPeek.PeekChanged += peeking =>
        {
            _isPeekingWaveform = peeking;
            Refresh();
        };
        var waveformCard = PaneCard.Make(WindowText.WaveformSection, _waveformPeek,
            Ui.VStack(6, _waveform, _waveformExplanation), Parts.PreviewScreen(54, _waveformSketch), _waveformStatus);

        var (meterRow, meterSwitch) = Parts.SwitchRow(WindowText.PhaseMeter, WindowText.PhaseMeterSubtitle);
        _meterSwitch = meterSwitch;
        _meterSwitch.Toggled += on => Model.Update((ref OverlaySettings s) => s.IsPhaseMeterEnabled = on);
        _meterType = new SegmentedPicker<PhaseMeterType>(WindowText.PhaseMeterType,
            PhaseMeterType.AllCases.Select(value => (value.DisplayName, value)).ToList(), value => value.Explanation, settings.PhaseMeterType);
        _meterType.SelectionChanged += value => Model.Update((ref OverlaySettings s) => s.PhaseMeterType = value);
        _meterOptions = Ui.VStack(6, _meterType, _meterExplanation);
        var counterTitle = Ui.Text(OptionText.CounterTitle, TypeStyle.BodyStrong, WindowTheme.SecondaryText);
        counterTitle.Margin = new Thickness(Spacing.Tight, 0, 0, 0);
        _counter = new SegmentedPicker<CounterStyle>(OptionText.CounterTitle, CounterStyle.AllCases.Select(value => (value.DisplayName, value)).ToList(),
            WindowText.CounterHelp, settings.CounterStyle);
        _counter.SelectionChanged += value => Model.Update((ref OverlaySettings s) => s.CounterStyle = value);
        _counterOptions = Ui.VStack(6, counterTitle, _counter, _counterExplanation);
        _meterPeek.PeekChanged += peeking =>
        {
            _isPeekingMeter = peeking;
            Refresh();
        };
        var meterCard = PaneCard.Make(WindowText.PhaseMeterSection, _meterPeek,
            meterRow, _meterOptions, _counterOptions, Parts.PreviewScreen(64, _meterCanvas), _meterStatus);

        _columns = Columns(Column(bpmCard, waveformCard), Column(meterCard));
        SetContent(_columns);
        Refresh();
        Tick(model.Now);
    }

    public override void Refresh()
    {
        var settings = Model.Settings;
        var status = Model.Status;
        _columns.Opacity = settings.IsEnabled ? 1 : 0.55;

        _limit.IsOn = settings.IsBPMLimitEnabled;
        _bpmLimit.Selected = settings.BPMLimit;
        _bpmExplanation.Text = settings.BPMLimit.Explanation;
        _bpmOptions.Opacity = settings.IsBPMLimitEnabled ? 1 : 0.45;
        _blind.IsOn = settings.IsBlindModeEnabled;
        _bpmStatus.Line = StatusText.BPMPrecision(status);
        _bpmSample.Limit = settings.IsBPMLimitEnabled ? settings.BPMLimit : null;

        _waveform.Selected = settings.WaveformMode;
        _waveformExplanation.Text = settings.WaveformMode.Explanation;
        _waveformSketch.Mode = _isPeekingWaveform ? WaveformMode.Normal : settings.WaveformMode;
        _waveformStatus.Line = StatusText.Waveform(status);
        _waveformPeek.IsLive = Model.IsMirroringDecks && Model.WaveformPhases is not null;

        _meterSwitch.IsOn = settings.IsPhaseMeterEnabled;
        _meterType.Selected = settings.PhaseMeterType;
        _meterExplanation.Text = settings.PhaseMeterType.Explanation;
        _meterOptions.Opacity = settings.IsPhaseMeterEnabled ? 1 : 0.45;
        _counter.Selected = settings.CounterStyle;
        _counterExplanation.Text = settings.CounterStyle.Explanation;
        _counterOptions.Opacity = settings.IsPhaseMeterEnabled ? 1 : 0.45;
        _meterCanvas.Visibility = _isPeekingMeter ? Visibility.Hidden : Visibility.Visible;
        _meterCanvas.Opacity = settings.IsPhaseMeterEnabled ? 1 : 0.3;
        _meterStatus.Line = StatusText.PhaseMeter(status);
        _meterPeek.IsLive = LiveMeter is not null;
        Tick(Model.Now);
    }

    private PhaseMeterDisplay? LiveMeter => Model.IsMirroringDecks && Model.Settings.IsPhaseMeterEnabled ? Model.PreviewMeter : null;

    public override void Tick(DateTimeOffset now)
    {
        var clock = new PreviewClock(now);
        var livePhases = Model.IsMirroringDecks ? Model.WaveformPhases : null;
        _waveformSketch.Phases = livePhases ?? new DeckPhases(clock.DeckBeats + 0.31, clock.MasterBeats);
        _meterCanvas.Display = LiveMeter ?? clock.MeterDisplay(Model.Settings.PhaseMeterType);
        _bpmSample.Tick(now);
    }
}

/// The BPM the jog shows under the current limit, with the covered digits in a lime-bordered box.
public sealed class BPMSampleView : Border
{
    private readonly TextBlock _visible = Ui.Text("", new TypeStyle(15, FontWeights.SemiBold, IsMono: true), WindowTheme.PrimaryText);
    private readonly TextBlock _covered = Ui.Text("", new TypeStyle(15, FontWeights.SemiBold, IsMono: true), System.Windows.Media.Brushes.Transparent);
    private readonly Border _cover;
    private BPMLimit? _limit;
    private DateTimeOffset _lastTick;

    public BPMSampleView()
    {
        // The stroke is drawn over the box like SwiftUI's strokeBorder, so it takes no room.
        _covered.Margin = new Thickness(1, 0, 1, 0);
        var box = new Grid();
        box.Children.Add(new Border { Background = WindowTheme.Black, CornerRadius = new CornerRadius(3) });
        box.Children.Add(_covered);
        box.Children.Add(new Border { BorderBrush = WindowTheme.Accent, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(3) });
        _cover = new Border { Child = box };
        Child = Ui.HStack(1, _visible, _cover);
    }

    public BPMLimit? Limit
    {
        set
        {
            _limit = value;
            _lastTick = default;
        }
    }

    public void Tick(DateTimeOffset now)
    {
        if ((now - _lastTick).TotalSeconds < 0.25) return;
        _lastTick = now;
        var seconds = (now - new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds;
        var sample = new BPMSample(128 + 0.06 * Math.Sin(seconds * 0.9), _limit);
        _visible.Text = sample.Visible;
        _covered.Text = sample.Covered;
        _cover.Visibility = sample.Covered.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(this, sample.Visible.Length == 0 ? WindowText.SampleHidden : WindowText.Sample(sample.Visible));
    }
}
