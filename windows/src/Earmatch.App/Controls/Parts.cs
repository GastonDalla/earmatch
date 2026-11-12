using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Controls;

/// A card of the macOS window: rounded 14, the card fill and a hairline, an optional spaced-out title with an
/// accessory on its right, and its content 10 apart.
public static class PaneCard
{
    public static Border Make(string? title, UIElement? accessory, params UIElement?[] content)
    {
        var stack = new Stack { Spacing = 10 };
        if (title is not null) stack.Children.Add(TitleRow(title, accessory));
        foreach (var child in content)
        {
            if (child is not null) stack.Children.Add(child);
        }
        return Wrap(stack);
    }

    /// A card that fills its column's height, with content at the top and a footer at the bottom.
    public static Border Filling(string? title, UIElement? accessory, UIElement body, UIElement? footer)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (title is not null)
        {
            var row = TitleRow(title, accessory);
            row.Margin = new Thickness(0, 0, 0, 10);
            grid.Children.Add(row);
        }
        if (body is FrameworkElement element) element.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetRow(body, 1);
        grid.Children.Add(body);
        if (footer is not null)
        {
            if (footer is FrameworkElement footerElement) footerElement.Margin = new Thickness(0, 10, 0, 0);
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);
        }
        var card = Wrap(grid);
        card.VerticalAlignment = VerticalAlignment.Stretch;
        return card;
    }

    public static Border Wrap(UIElement content) => new()
    {
        Child = content,
        CornerRadius = new CornerRadius(14),
        Background = WindowTheme.Card,
        BorderBrush = WindowTheme.Hairline,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(Spacing.Medium - 1, 12 - 1, Spacing.Medium - 1, 12 - 1),
        VerticalAlignment = VerticalAlignment.Top,
    };

    private static Grid TitleRow(string title, UIElement? accessory)
    {
        var row = Ui.Row(CardTitle(title), accessory);
        row.MinHeight = 18;
        return row;
    }

    public static FrameworkElement CardTitle(string text)
    {
        var title = new TrackedText(text, TypeStyle.Section, 2, WindowTheme.SectionText) { VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(title, text);
        return title;
    }
}

/// The status line at the bottom of a card: a tone dot and monospaced text, with an optional trailing view.
public sealed class CardStatusRow : Grid
{
    private readonly System.Windows.Shapes.Ellipse _dot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _text = Ui.Text("", TypeStyle.Status, WindowTheme.SecondaryText, wraps: true);

    public CardStatusRow(StatusLine line, UIElement? trailing = null)
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _dot.Margin = new Thickness(0, AppFonts.LineHeight(TypeStyle.Status.Size) / 2 - 2.5, Spacing.Small, 0);
        _text.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_dot);
        SetColumn(_text, 1);
        Children.Add(_text);
        if (trailing is FrameworkElement element)
        {
            element.Margin = new Thickness(Spacing.Small, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Top;
            SetColumn(element, 2);
            Children.Add(element);
        }
        Line = line;
    }

    public StatusLine Line
    {
        set
        {
            _dot.Fill = WindowTheme.BrushFor(value.Tone);
            _text.Text = value.Text;
            AutomationProperties.SetName(this, value.Text);
        }
    }
}

public static class Parts
{
    public static TextBlock Explanation(string text)
    {
        var block = Ui.Text(text, TypeStyle.Caption, WindowTheme.SecondaryText, wraps: true);
        block.Margin = new Thickness(Spacing.Tight, 0, 0, 0);
        return block;
    }

    /// The room SwiftUI's HStack(spacing: 16) { texts; Spacer(minLength: 8); switch } leaves between the texts and the
    /// switch: two gaps and the spacer, which is where the macOS subtitles wrap.
    private const double SwitchRowGap = Spacing.Medium * 2 + Spacing.Small;

    /// A title, an optional subtitle, and the switch on the right.
    public static (Grid Row, AccentSwitch Switch) SwitchRow(string title, string? subtitle, string? hint = null)
    {
        var texts = Ui.VStack(2, Ui.Text(title, TypeStyle.Headline, WindowTheme.PrimaryText),
            subtitle is null ? null : Ui.Text(subtitle, TypeStyle.Caption, WindowTheme.SecondaryText, wraps: true));
        var toggle = new AccentSwitch(title);
        AutomationProperties.SetHelpText(toggle, hint ?? subtitle ?? "");
        var row = Ui.Row(texts, toggle, SwitchRowGap);
        return (row, toggle);
    }

    public static Border KeyCap(string text) => Ui.Surface(Ui.Text(text, new TypeStyle(11.5, FontWeights.SemiBold), WindowTheme.PrimaryText),
        5, WindowTheme.Well, WindowTheme.White(0.14), new Thickness(7 - 1, 3 - 1, 7 - 1, 3 - 1));

    public static FrameworkElement HotkeyHint(string key, string text)
    {
        var hint = Ui.HStack(Spacing.Small, KeyCap(key), Ui.Text(text, TypeStyle.Caption, WindowTheme.SecondaryText));
        AutomationProperties.SetName(hint, $"{key}: {text}");
        return hint;
    }

    public static Border LiveChip()
    {
        var chip = new Border
        {
            Background = WindowTheme.Solid(WindowTheme.AccentColor, 0.12),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 2, 7, 2),
            Child = Ui.HStack(5, Ui.Dot(5, WindowTheme.Accent), new TrackedText(WindowText.LiveChip, TypeStyle.Label, 1.6, WindowTheme.Accent)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(chip, WindowText.LiveSpoken);
        return chip;
    }

    /// The black screen the previews draw in.
    public static Border PreviewScreen(double height, UIElement content) => new()
    {
        Height = height,
        Background = WindowTheme.Black,
        CornerRadius = new CornerRadius(8),
        BorderBrush = WindowTheme.Hairline,
        BorderThickness = new Thickness(1),
        Child = new Border { Child = content, ClipToBounds = true, Margin = new Thickness(-1) },
    };
}

/// The PREVIEW title (or the LIVE chip while rekordbox is mirrored) and the eye that, held down, shows rekordbox
/// unchanged.
public sealed class PreviewPeekControl : Border
{
    private readonly TextBlock _unchanged = Ui.Text(WindowText.Unchanged, TypeStyle.Label, WindowTheme.SecondaryText);
    private readonly TrackedText _title = new(WindowText.PreviewTitle, TypeStyle.Label, 2.2, WindowTheme.SectionText);
    private readonly Border _chip = Parts.LiveChip();
    private readonly Icon _eye = new(IconKind.Eye, 16, WindowTheme.SecondaryText);
    private readonly Border _eyeBox;
    private bool _isPeeking;

    public PreviewPeekControl()
    {
        _eyeBox = new Border { Width = 24, Height = 18, Background = Brushes.Transparent, Child = _eye, Cursor = Cursors.Hand, ToolTip = WindowText.PeekHelp };
        _eye.HorizontalAlignment = HorizontalAlignment.Center;
        _eye.VerticalAlignment = VerticalAlignment.Center;
        FocusRing.Apply(_eyeBox, 5);
        AutomationProperties.SetName(_eyeBox, WindowText.Peek);
        _eyeBox.MouseLeftButtonDown += (_, e) =>
        {
            _eyeBox.CaptureMouse();
            SetPeeking(true);
            e.Handled = true;
        };
        _eyeBox.MouseLeftButtonUp += (_, e) =>
        {
            _eyeBox.ReleaseMouseCapture();
            SetPeeking(false);
            e.Handled = true;
        };
        _eyeBox.LostMouseCapture += (_, _) => SetPeeking(false);
        _eyeBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Space) return;
            SetPeeking(true);
            e.Handled = true;
        };
        _eyeBox.KeyUp += (_, e) =>
        {
            if (e.Key != Key.Space) return;
            SetPeeking(false);
            e.Handled = true;
        };
        _eyeBox.LostKeyboardFocus += (_, _) => SetPeeking(false);
        _unchanged.Visibility = Visibility.Collapsed;
        var badge = new Grid { VerticalAlignment = VerticalAlignment.Center };
        _title.HorizontalAlignment = HorizontalAlignment.Right;
        _title.VerticalAlignment = VerticalAlignment.Center;
        badge.Children.Add(_title);
        badge.Children.Add(_chip);
        Child = Ui.HStack(Spacing.Small, _unchanged, badge, _eyeBox);
        VerticalAlignment = VerticalAlignment.Center;
    }

    public event Action<bool>? PeekChanged;

    public bool IsLive
    {
        set
        {
            _title.Opacity = value ? 0 : 1;
            _chip.Opacity = value ? 1 : 0;
        }
    }

    private void SetPeeking(bool peeking)
    {
        if (peeking == _isPeeking) return;
        _isPeeking = peeking;
        _unchanged.Visibility = peeking ? Visibility.Visible : Visibility.Collapsed;
        _eye.Kind = peeking ? IconKind.EyeSlash : IconKind.Eye;
        _eye.Brush = peeking ? WindowTheme.Accent : WindowTheme.SecondaryText;
        AutomationProperties.SetName(_eyeBox, peeking ? WindowText.Peeking : WindowText.Peek);
        PeekChanged?.Invoke(peeking);
    }
}

public enum ActionKind
{
    Accent,
    Pill,
    Link,
}

/// The buttons of the macOS window: a lime accent button, a hairline pill, or an inline link.
public sealed class ActionButton : Border
{
    private readonly ActionKind _kind;
    private readonly Action _action;
    private bool _isPressed;

    public ActionKind Kind => _kind;

    public ActionButton(ActionKind kind, string accessibilityText, Action action, UIElement label)
    {
        _kind = kind;
        _action = action;
        Child = label;
        Cursor = Cursors.Hand;
        ToolTip = accessibilityText;
        AutomationProperties.SetName(this, accessibilityText);
        FocusRing.Apply(this, kind == ActionKind.Pill ? 14 : 7);
        switch (kind)
        {
            case ActionKind.Accent:
                Background = WindowTheme.Accent;
                CornerRadius = new CornerRadius(7);
                Padding = new Thickness(12, 5, 12, 5);
                break;
            case ActionKind.Pill:
                CornerRadius = new CornerRadius(12);
                BorderThickness = new Thickness(1);
                BorderBrush = WindowTheme.Hairline;
                Padding = new Thickness(Spacing.Medium - 1, 7 - 1, Spacing.Medium - 1, 7 - 1);
                break;
            default:
                Background = Brushes.Transparent;
                Padding = new Thickness(Spacing.Tight, 2, Spacing.Tight, 2);
                break;
        }
        Restyle();
        MouseEnter += (_, _) => Restyle();
        MouseLeave += (_, _) =>
        {
            _isPressed = false;
            Restyle();
        };
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    public static ActionButton WithIcon(ActionKind kind, string text, IconKind icon, Action action, string? accessibilityText = null)
    {
        var label = Ui.Text(text, TypeStyle.BodyStrong, WindowTheme.PrimaryText);
        var iconView = new Icon(icon, 16, WindowTheme.PrimaryText, 1.8) { VerticalAlignment = VerticalAlignment.Center };
        return new ActionButton(kind, accessibilityText ?? text, action, Ui.HStack(7, iconView, label));
    }

    public static ActionButton Plain(ActionKind kind, string text, Action action, string? accessibilityText = null) =>
        new(kind, accessibilityText ?? text, action, Ui.Text(text, TypeStyle.BodyStrong, kind == ActionKind.Accent ? WindowTheme.OnAccent : WindowTheme.PrimaryText));

    private void Restyle()
    {
        var hovered = IsMouseOver;
        if (_kind == ActionKind.Pill)
        {
            Background = hovered ? WindowTheme.White(0.07) : WindowTheme.Card;
            SetForeground(this, hovered ? WindowTheme.White(0.92 * 0.95) : WindowTheme.White(0.92 * 0.8));
        }
        Opacity = _isPressed ? (_kind == ActionKind.Accent ? 0.75 : 0.7) : 1;
        if (_kind == ActionKind.Link && Child is FrameworkElement element) SetUnderline(element, hovered);
    }

    private static void SetForeground(DependencyObject root, Brush brush)
    {
        switch (root)
        {
            case TextBlock text:
                text.Foreground = brush;
                return;
            case Icon icon:
                icon.Brush = brush;
                return;
        }
        if (root is Border { Child: { } content }) SetForeground(content, brush);
        if (root is Panel panel)
        {
            foreach (UIElement element in panel.Children) SetForeground(element, brush);
        }
    }

    private static void SetUnderline(DependencyObject root, bool underlined)
    {
        if (root is TextBlock text)
        {
            text.TextDecorations = underlined ? TextDecorations.Underline : null;
            return;
        }
        if (root is Panel panel)
        {
            foreach (UIElement child in panel.Children) SetUnderline(child, underlined);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isPressed = true;
        CaptureMouse();
        Restyle();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var wasPressed = _isPressed;
        _isPressed = false;
        ReleaseMouseCapture();
        Restyle();
        var point = e.GetPosition(this);
        if (wasPressed && point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight) _action();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter && !e.IsRepeat)
        {
            _action();
            e.Handled = true;
        }
    }

    internal void InvokeByUser() => _action();

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ActionButtonAutomationPeer(this);
}
