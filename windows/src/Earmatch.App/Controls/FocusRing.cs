using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earmatch.App.Theme;

namespace Earmatch.App.Controls;

public static class FocusRing
{
    private static readonly Dictionary<double, Style> Styles = [];

    public static Style For(double cornerRadius)
    {
        if (Styles.TryGetValue(cornerRadius, out var style)) return style;
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BorderBrushProperty, WindowTheme.Accent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius + 3));
        border.SetValue(FrameworkElement.MarginProperty, new Thickness(-3));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        style = new Style(typeof(Control));
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = border }));
        style.Seal();
        Styles[cornerRadius] = style;
        return style;
    }

    public static void Apply(FrameworkElement element, double cornerRadius)
    {
        element.Focusable = true;
        element.FocusVisualStyle = For(cornerRadius);
    }
}
