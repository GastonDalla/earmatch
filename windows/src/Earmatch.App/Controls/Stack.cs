using System.Windows;
using System.Windows.Controls;

namespace Earmatch.App.Controls;

/// A StackPanel with spacing between its visible children, like SwiftUI's VStack and HStack.
public sealed class Stack : Panel
{
    public Orientation Orientation { get; init; } = Orientation.Vertical;
    public double Spacing { get; init; }

    protected override Size MeasureOverride(Size available)
    {
        var vertical = Orientation == Orientation.Vertical;
        double main = 0, cross = 0;
        var visible = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(vertical ? new Size(available.Width, double.PositiveInfinity) : new Size(double.PositiveInfinity, available.Height));
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            main += vertical ? size.Height : size.Width;
            cross = Math.Max(cross, vertical ? size.Width : size.Height);
            visible++;
        }
        main += Spacing * Math.Max(visible - 1, 0);
        return vertical ? new Size(cross, main) : new Size(main, cross);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var vertical = Orientation == Orientation.Vertical;
        double offset = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            if (vertical)
            {
                child.Arrange(new Rect(0, offset, final.Width, size.Height));
                offset += size.Height + Spacing;
            }
            else
            {
                child.Arrange(new Rect(offset, 0, size.Width, final.Height));
                offset += size.Width + Spacing;
            }
        }
        return final;
    }
}

/// Shows its first child when it fits the width, otherwise its second, like SwiftUI's ViewThatFits.
public sealed class FirstThatFits : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        if (InternalChildren.Count == 0) return default;
        var first = InternalChildren[0];
        first.Measure(new Size(double.PositiveInfinity, available.Height));
        var fits = InternalChildren.Count == 1 || first.DesiredSize.Width <= available.Width;
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            InternalChildren[index].Visibility = index == (fits ? 0 : 1) ? Visibility.Visible : Visibility.Collapsed;
        }
        var shown = InternalChildren[fits ? 0 : 1];
        shown.Measure(available);
        return shown.DesiredSize;
    }

    protected override Size ArrangeOverride(Size final)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Visible) child.Arrange(new Rect(final));
        }
        return final;
    }
}
