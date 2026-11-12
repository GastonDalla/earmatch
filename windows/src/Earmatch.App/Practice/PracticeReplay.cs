using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Drawing;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Practice;

public sealed class PracticeReplay : Border
{
    private readonly PracticeTrace _trace;
    private readonly PracticeErrorMeter _meter = new() { Height = 30 };
    private readonly TextBlock _value = Ui.Text("", new TypeStyle(15, FontWeights.SemiBold, IsMono: true), WindowTheme.SecondaryText);
    private readonly PracticeTraceView _traceView;
    private readonly TextBlock _caption;
    private readonly ActionButton _button;
    private readonly TextBlock _buttonLabel;
    private readonly Icon _buttonIcon;
    private DateTimeOffset? _playbackStart;
    private double? _cursor;
    private double? _error;

    public PracticeReplay(PracticeTrace trace, double traceHeight)
    {
        _trace = trace;
        _traceView = new PracticeTraceView { Height = traceHeight };
        _button = ActionButton.WithIcon(ActionKind.Pill, PracticeStrings.Play, IconKind.PlayFill, Toggle, PracticeStrings.PlayHint);
        _buttonLabel = (TextBlock)((Stack)_button.Child).Children[1];
        _buttonIcon = (Icon)((Stack)_button.Child).Children[0];
        _button.HorizontalAlignment = HorizontalAlignment.Right;

        _value.Width = 70;
        _value.TextAlignment = TextAlignment.Right;
        var meterRow = new Grid();
        meterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        meterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        meterRow.Children.Add(_meter);
        _value.Margin = new Thickness(Spacing.Medium, 0, 0, 0);
        Grid.SetColumn(_value, 1);
        meterRow.Children.Add(_value);

        var footer = PracticePane.Footer(PracticeText.Duration(0), "", PracticeText.Duration(trace.DurationSeconds));
        _caption = (TextBlock)footer.Children[1];
        var well = Ui.Surface(Ui.VStack(Spacing.Small, meterRow, _traceView, footer), 10, WindowTheme.Well, WindowTheme.Hairline, new Thickness(12 - 1));
        var spoken = new SpokenGroup(well, () => PracticeSpeech.Replay(_trace))
        {
            DescribeValue = () => _cursor is { } seconds ? PracticeSpeech.ReplayValue(seconds, _error) : "",
        };
        Child = Ui.VStack(Spacing.Small, Ui.Row(PracticePane.Label(PracticeStrings.Replay), _button), spoken);
        Show(DateTimeOffset.Now);
        Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    private double PlaybackSeconds => Math.Clamp(_trace.DurationSeconds / 6, 4, 12);

    private void Toggle()
    {
        _playbackStart = _playbackStart is null ? DateTimeOffset.Now : null;
        _buttonLabel.Text = _playbackStart is null ? PracticeStrings.Play : PracticeStrings.Stop;
        _buttonIcon.Kind = _playbackStart is null ? IconKind.PlayFill : IconKind.StopFill;
        _button.ToolTip = _playbackStart is null ? PracticeStrings.PlayHint : PracticeStrings.StopHint;
        Show(DateTimeOffset.Now);
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_playbackStart is null) return;
        var now = DateTimeOffset.Now;
        if ((now - _playbackStart.Value).TotalSeconds >= PlaybackSeconds)
        {
            Toggle();
            return;
        }
        Show(now);
    }

    private void Show(DateTimeOffset now)
    {
        double? cursor = _playbackStart is { } start
            ? Math.Clamp((now - start).TotalSeconds / PlaybackSeconds, 0, 1) * _trace.DurationSeconds
            : _trace.WorstSample?.Seconds;
        var error = cursor is { } seconds ? _trace.ErrorMilliseconds(seconds) : null;
        _cursor = cursor;
        _error = error;
        var isMatched = cursor is { } at && _trace.MatchedAtSeconds is { } matched && at >= matched;
        _meter.Show(error, isDimmed: !isMatched);
        _value.Text = error is { } shown ? PracticeText.Milliseconds(shown, signed: true) : PracticeText.NoValue;
        _value.Foreground = error is { } colour ? PracticeErrorMeter.BrushFor(colour) : WindowTheme.SecondaryText;
        _traceView.Show(_trace, cursor);
        _caption.Text = cursor is not { } position ? ""
            : _playbackStart is not null ? PracticeText.ReplayPosition(position, _trace.DurationSeconds) : PracticeText.WorstAt(position);
    }
}
