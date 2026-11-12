using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earmatch.App.Theme;

namespace Earmatch.App.Controls;

public static class Ui
{
    public static TextBlock Text(string text, TypeStyle style, Brush foreground, bool wraps = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = style.Family,
            FontSize = style.Size,
            FontWeight = style.Weight,
            Foreground = foreground,
            TextWrapping = wraps ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wraps ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            LineHeight = AppFonts.LineHeight(style.Size),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextOptions.SetTextFormattingMode(block, TextFormattingMode.Ideal);
        return block;
    }

    public static Stack VStack(double spacing, params UIElement?[] children) => Fill(new Stack { Spacing = spacing }, children);

    public static Stack HStack(double spacing, params UIElement?[] children) =>
        Fill(new Stack { Orientation = Orientation.Horizontal, Spacing = spacing }, children);

    private static Stack Fill(Stack stack, UIElement?[] children)
    {
        foreach (var child in children)
        {
            if (child is not null) stack.Children.Add(child);
        }
        return stack;
    }

    public static Grid Row(UIElement leading, UIElement? trailing, double minimumGap = Spacing.Small, VerticalAlignment alignment = VerticalAlignment.Center)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (leading is FrameworkElement element) element.VerticalAlignment = alignment;
        grid.Children.Add(leading);
        if (trailing is not null)
        {
            if (trailing is FrameworkElement trailingElement)
            {
                trailingElement.VerticalAlignment = alignment;
                trailingElement.Margin = new Thickness(minimumGap, 0, 0, 0);
            }
            Grid.SetColumn(trailing, 1);
            grid.Children.Add(trailing);
        }
        return grid;
    }

    public static Border Surface(UIElement? child, double cornerRadius, Brush fill, Brush stroke, Thickness padding = default, double lineWidth = 1) => new()
    {
        Child = child,
        CornerRadius = new CornerRadius(cornerRadius),
        Background = fill,
        BorderBrush = stroke,
        BorderThickness = new Thickness(lineWidth),
        Padding = padding,
    };

    public static FrameworkElement Dot(double size, Brush fill) => new System.Windows.Shapes.Ellipse { Width = size, Height = size, Fill = fill };

    public static FrameworkElement Divider() => new Border { Height = 1, Background = WindowTheme.Hairline };

    public static FormattedText Formatted(string text, TypeStyle style, Brush brush, double pixelsPerDip = 1) =>
        Formatted(text, style.Family, style.Size, style.Weight, brush, pixelsPerDip);

    public static FormattedText Formatted(string text, FontFamily family, double size, FontWeight weight, Brush brush, double pixelsPerDip = 1) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal),
            size, brush, pixelsPerDip);

    public static void DrawText(DrawingContext context, FormattedText text, double x, double y, TextAnchor anchor)
    {
        var left = anchor switch
        {
            TextAnchor.Leading => x,
            TextAnchor.Center => x - text.WidthIncludingTrailingWhitespace / 2,
            _ => x - text.WidthIncludingTrailingWhitespace,
        };
        context.DrawText(text, new Point(left, y - text.Height / 2));
    }
}

public enum TextAnchor
{
    Leading,
    Center,
    Trailing,
}

public sealed class TrackedText : FrameworkElement
{
    private readonly List<FormattedText> _glyphs = [];
    private string _text = "";
    private Brush _foreground = WindowTheme.PrimaryText;

    public TrackedText(string text, TypeStyle style, double tracking, Brush foreground)
    {
        TextStyle = style;
        Tracking = tracking;
        _foreground = foreground;
        Text = text;
        SnapsToDevicePixels = true;
    }

    public TypeStyle TextStyle { get; }
    public double Tracking { get; }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Rebuild();
        }
    }

    public Brush Foreground
    {
        get => _foreground;
        set
        {
            _foreground = value;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _glyphs.Clear();
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var character in Elements(StringInfo.GetTextElementEnumerator(_text)))
        {
            _glyphs.Add(Ui.Formatted(character, TextStyle, _foreground, dpi));
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static IEnumerable<string> Elements(TextElementEnumerator elements)
    {
        while (elements.MoveNext()) yield return elements.GetTextElement();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = _glyphs.Sum(glyph => glyph.WidthIncludingTrailingWhitespace + Tracking);
        return new Size(Math.Max(0, width - (_glyphs.Count > 0 ? Tracking : 0)), AppFonts.LineHeight(TextStyle.Size));
    }

    protected override void OnRender(DrawingContext context)
    {
        double x = 0;
        var height = AppFonts.LineHeight(TextStyle.Size);
        foreach (var glyph in _glyphs)
        {
            context.DrawText(glyph, new Point(x, (height - glyph.Height) / 2));
            x += glyph.WidthIncludingTrailingWhitespace + Tracking;
        }
    }
}
