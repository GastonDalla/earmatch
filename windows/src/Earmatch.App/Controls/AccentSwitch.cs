using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Earmatch.App.Theme;

namespace Earmatch.App.Controls;

public sealed class AccentSwitch : FrameworkElement
{
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(AccentSwitch),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Color OffFill = WindowTheme.WithOpacity(Colors.White, 0.16);
    private static readonly Brush KnobShadow = WindowTheme.Solid(Colors.Black, 0.22);
    private bool _isOn;

    public AccentSwitch(string title)
    {
        Width = 46;
        Height = 26;
        Cursor = Cursors.Hand;
        FocusRing.Apply(this, 12);
        AutomationProperties.SetName(this, title);
    }

    public event Action<bool>? Toggled;

    private double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsOn
    {
        get => _isOn;
        set => SetOn(value, animated: IsLoaded);
    }

    private void SetOn(bool value, bool animated)
    {
        if (_isOn == value && animated) return;
        var wasOn = _isOn;
        _isOn = value;
        if (wasOn != value && UIElementAutomationPeer.FromElement(this) is AccentSwitchAutomationPeer peer) peer.RaiseToggled(wasOn, value);
        var target = value ? 1.0 : 0.0;
        if (animated && !WindowTheme.ReduceMotion)
        {
            BeginAnimation(ProgressProperty, new DoubleAnimation(target, TimeSpan.FromSeconds(0.18)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        else
        {
            BeginAnimation(ProgressProperty, null);
            Progress = target;
        }
    }

    private void Toggle()
    {
        SetOn(!_isOn, animated: true);
        Toggled?.Invoke(_isOn);
    }

    internal void ToggleByUser() => Toggle();

    protected override AutomationPeer OnCreateAutomationPeer() => new AccentSwitchAutomationPeer(this);

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var point = e.GetPosition(this);
        if (point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight) Toggle();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter && !e.IsRepeat)
        {
            Toggle();
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        var progress = Progress;
        var fill = Blend(OffFill, WindowTheme.AccentColor, progress);
        context.DrawRoundedRectangle(new SolidColorBrush(fill), progress < 1 ? new Pen(WindowTheme.White(0.08 * (1 - progress)), 1) : null,
            new Rect(0.5, 0.5, 45, 25), 12.5, 12.5);
        var knobX = 3 + progress * 20 + 10;
        context.DrawEllipse(KnobShadow, null, new Point(knobX, 13.8), 10.6, 10.6);
        context.DrawEllipse(WindowTheme.WhiteBrush, null, new Point(knobX, 13), 10, 10);
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (byte)(from.A + (to.A - from.A) * amount), (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount), (byte)(from.B + (to.B - from.B) * amount));
}
