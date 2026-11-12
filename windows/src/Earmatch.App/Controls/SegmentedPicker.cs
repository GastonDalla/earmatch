using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Controls;

/// The segmented picker of the macOS window: a dark well with one lime pill on the selected option. Labels that fit
/// share the row equally (SegmentWidths); Left and Right step through the options.
public sealed class SegmentedPicker<T> : FrameworkElement, ISegmentedOptions
{
    private const double Inset = Spacing.Tight;
    private const double Gap = Spacing.Tight;
    private const double SegmentHeight = 30;
    private const double LabelPadding = 6;

    private static readonly DependencyProperty PillXProperty = DependencyProperty.Register("PillX", typeof(double), typeof(SegmentedPicker<T>),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty PillWidthProperty = DependencyProperty.Register("PillWidth", typeof(double), typeof(SegmentedPicker<T>),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly IReadOnlyList<(string Label, T Value)> _options;
    private readonly Func<T, string> _explanation;
    private double[] _widths = [];
    private T _selected;

    public SegmentedPicker(string title, IReadOnlyList<(string Label, T Value)> options, Func<T, string> explanation, T selected)
    {
        _options = options;
        _explanation = explanation;
        _selected = selected;
        Height = SegmentHeight + 2 * Inset;
        FocusRing.Apply(this, 10);
        AutomationProperties.SetName(this, title);
        ToolTipService.SetInitialShowDelay(this, 600);
        UpdateHelp();
    }

    public event Action<T>? SelectionChanged;

    public T Selected
    {
        get => _selected;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_selected, value)) return;
            _selected = value;
            MovePill(animated: IsLoaded);
            UpdateHelp();
        }
    }

    private int SelectedIndex
    {
        get
        {
            for (var index = 0; index < _options.Count; index++)
            {
                if (EqualityComparer<T>.Default.Equals(_options[index].Value, _selected)) return index;
            }
            return -1;
        }
    }

    private void UpdateHelp() => AutomationProperties.SetHelpText(this, _explanation(_selected));

    private FormattedText Label(int index, Brush brush, double size = 13) =>
        Ui.Formatted(_options[index].Label, TypeStyle.BodyStrong.Family, size, FontWeights.SemiBold, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double IdealWidth(int index) => Label(index, WindowTheme.PrimaryText).WidthIncludingTrailingWhitespace + 2 * LabelPadding;

    protected override Size MeasureOverride(Size available)
    {
        var ideal = Enumerable.Range(0, _options.Count).Sum(IdealWidth) + Gap * Math.Max(_options.Count - 1, 0) + 2 * Inset;
        return new Size(double.IsInfinity(available.Width) ? ideal : available.Width, SegmentHeight + 2 * Inset);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var ideal = Enumerable.Range(0, _options.Count).Select(IdealWidth).ToList();
        var widths = SegmentWidths.Distribute(ideal, final.Width - 2 * Inset - Gap * Math.Max(_options.Count - 1, 0));
        if (!widths.SequenceEqual(_widths))
        {
            _widths = widths;
            MovePill(animated: false);
        }
        return final;
    }

    private double SegmentX(int index)
    {
        var x = Inset;
        for (var i = 0; i < index && i < _widths.Length; i++) x += _widths[i] + Gap;
        return x;
    }

    private void MovePill(bool animated)
    {
        var index = SelectedIndex;
        if (index < 0 || index >= _widths.Length)
        {
            SetValue(PillWidthProperty, 0.0);
            return;
        }
        var x = SegmentX(index);
        var width = _widths[index];
        if (animated && !WindowTheme.ReduceMotion && !double.IsNaN((double)GetValue(PillXProperty)))
        {
            var duration = TimeSpan.FromSeconds(0.22);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(PillXProperty, new DoubleAnimation(x, duration) { EasingFunction = easing });
            BeginAnimation(PillWidthProperty, new DoubleAnimation(width, duration) { EasingFunction = easing });
        }
        else
        {
            BeginAnimation(PillXProperty, null);
            BeginAnimation(PillWidthProperty, null);
            SetValue(PillXProperty, x);
            SetValue(PillWidthProperty, width);
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        context.DrawRoundedRectangle(WindowTheme.Well, new Pen(WindowTheme.Hairline, 1), new Rect(0.5, 0.5, bounds.Width - 1, bounds.Height - 1), 9.5, 9.5);
        var pillX = (double)GetValue(PillXProperty);
        var pillWidth = (double)GetValue(PillWidthProperty);
        if (pillWidth > 0 && !double.IsNaN(pillX))
        {
            context.DrawRoundedRectangle(WindowTheme.Accent, null, new Rect(pillX, Inset, pillWidth, SegmentHeight), 7, 7);
        }
        var selected = SelectedIndex;
        for (var index = 0; index < _options.Count && index < _widths.Length; index++)
        {
            var brush = index == selected ? WindowTheme.OnAccent : WindowTheme.SecondaryText;
            var room = _widths[index] - 2 * LabelPadding;
            var label = Label(index, brush);
            if (label.WidthIncludingTrailingWhitespace > room && room > 0)
            {
                var size = Math.Max(13 * 0.8, 13 * room / label.WidthIncludingTrailingWhitespace);
                label = Label(index, brush, size);
                label.MaxTextWidth = Math.Max(room, 1);
                label.Trimming = TextTrimming.CharacterEllipsis;
                label.MaxLineCount = 1;
            }
            var centre = SegmentX(index) + _widths[index] / 2;
            Ui.DrawText(context, label, centre, Inset + SegmentHeight / 2, TextAnchor.Center);
        }
    }

    private int IndexAt(Point point)
    {
        for (var index = 0; index < _widths.Length; index++)
        {
            var x = SegmentX(index);
            if (point.X >= x - Gap / 2 && point.X < x + _widths[index] + Gap / 2) return index;
        }
        return -1;
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _options.Count) return;
        var value = _options[index].Value;
        if (EqualityComparer<T>.Default.Equals(value, _selected)) return;
        Selected = value;
        if (UIElementAutomationPeer.FromElement(this) is SegmentedPickerAutomationPeer peer) peer.RaiseSelected(index);
        SelectionChanged?.Invoke(value);
    }

    int ISegmentedOptions.OptionCount => _options.Count;

    int ISegmentedOptions.SelectedOptionIndex => SelectedIndex;

    string ISegmentedOptions.OptionLabel(int index) => _options[index].Label;

    string ISegmentedOptions.OptionHelp(int index) => _explanation(_options[index].Value);

    Rect ISegmentedOptions.OptionBounds(int index) =>
        index < _widths.Length ? new Rect(SegmentX(index), Inset, _widths[index], SegmentHeight) : Rect.Empty;

    void ISegmentedOptions.SelectOption(int index) => Select(index);

    protected override AutomationPeer OnCreateAutomationPeer() => new SegmentedPickerAutomationPeer(this);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Select(IndexAt(e.GetPosition(this)));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexAt(e.GetPosition(this));
        var help = index >= 0 ? _explanation(_options[index].Value) : null;
        if (!Equals(ToolTip, help)) ToolTip = help;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Left or Key.Right)
        {
            Select(Math.Clamp(SelectedIndex + (e.Key == Key.Left ? -1 : 1), 0, _options.Count - 1));
            e.Handled = true;
        }
    }
}
