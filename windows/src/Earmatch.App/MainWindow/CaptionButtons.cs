using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Earmatch.App.Controls;
using Earmatch.App.Theme;

namespace Earmatch.App.Panes;

public enum CaptionButtonKind
{
    Minimize,
    Maximize,
    Restore,
    Close,
}

/// A Windows 11 caption button: 46×32, a Segoe Fluent Icons glyph (Segoe MDL2 Assets on Windows 10) on a background
/// that lights up on hover and press, red under Close. Like the system's caption buttons it never takes the keyboard
/// focus (Alt+Space, Win+arrows and Alt+F4 work as in any window); screen readers see a named button they can invoke.
public sealed class CaptionButton : Button
{
    public const double ButtonWidth = 46;
    public const double ButtonHeight = 32;
    private const double GlyphSize = 10;

    // The WinUI title bar's dark theme: SubtleFillColorSecondary/Tertiary under the glyph, TextFillColorPrimary,
    // Secondary and Disabled for the glyph, #C42B1C under Close.
    private static readonly Color CloseRed = Color.FromRgb(0xC4, 0x2B, 0x1C);
    private static readonly Brush HoverFill = WindowTheme.Solid(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
    private static readonly Brush PressedFill = WindowTheme.Solid(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
    private static readonly Brush CloseHoverFill = WindowTheme.Solid(CloseRed);
    private static readonly Brush ClosePressedFill = WindowTheme.Solid(CloseRed, 0.9);
    private static readonly Brush Glyph = WindowTheme.WhiteBrush;
    private static readonly Brush PressedGlyph = WindowTheme.Solid(Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF));
    private static readonly Brush InactiveGlyph = WindowTheme.Solid(Color.FromArgb(0x5D, 0xFF, 0xFF, 0xFF));
    private static readonly Brush ClosePressedGlyph = WindowTheme.White(0.7);

    /// No visual tree: the button draws itself in OnRender.
    private static readonly ControlTemplate Drawn = new(typeof(CaptionButton));

    private CaptionButtonKind _kind;
    private bool _isWindowActive = true;
    private bool _isHot;
    private bool _isHeld;

    public CaptionButton(CaptionButtonKind kind)
    {
        Template = Drawn;
        Width = ButtonWidth;
        Height = ButtonHeight;
        Focusable = false;
        IsTabStop = false;
        WindowChrome.SetIsHitTestVisibleInChrome(this, true);
        Kind = kind;
    }

    public CaptionButtonKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Relabel();
            InvalidateVisual();
        }
    }

    public bool IsWindowActive
    {
        set
        {
            if (_isWindowActive == value) return;
            _isWindowActive = value;
            InvalidateVisual();
        }
    }

    /// The window sets these for the maximize button, whose mouse input arrives as non-client messages that WPF does
    /// not turn into IsMouseOver and IsPressed.
    public void ShowNonClientState(bool isHot, bool isHeld)
    {
        if (_isHot == isHot && _isHeld == isHeld) return;
        _isHot = isHot;
        _isHeld = isHeld;
        InvalidateVisual();
    }

    /// The name and tooltip in the window's current language.
    public void Relabel()
    {
        var name = _kind switch
        {
            CaptionButtonKind.Minimize => WindowText.Minimize,
            CaptionButtonKind.Maximize => WindowText.Maximize,
            CaptionButtonKind.Restore => WindowText.Restore,
            _ => WindowText.Close,
        };
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetAcceleratorKey(this, _kind == CaptionButtonKind.Close ? "Alt+F4" : "");
        ToolTip = name;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateVisual();
    }

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var isHot = IsMouseOver || _isHot;
        var isPressed = isHot && (IsPressed || _isHeld);
        var isClose = _kind == CaptionButtonKind.Close;
        var fill = (isPressed, isHot, isClose) switch
        {
            (true, _, true) => ClosePressedFill,
            (true, _, false) => PressedFill,
            (false, true, true) => CloseHoverFill,
            (false, true, false) => HoverFill,
            _ => Brushes.Transparent,
        };
        // Transparent still takes the mouse, so the whole 46×32 box is the button.
        context.DrawRectangle(fill, null, bounds);

        var ink = (isPressed, isHot, isClose) switch
        {
            (true, _, true) => ClosePressedGlyph,
            (_, true, true) => Glyph,
            (true, _, false) => PressedGlyph,
            _ when !_isWindowActive => InactiveGlyph,
            _ => Glyph,
        };
        var dpi = VisualTreeHelper.GetDpi(this);
        var glyph = Ui.Formatted(GlyphFor(_kind), AppFonts.Icons, GlyphSize, FontWeights.Normal, ink, dpi.PixelsPerDip);
        // Centre the glyph's ink, not its line box, and snap it to whole pixels so its 1-pixel strokes stay sharp.
        var inkBounds = glyph.BuildGeometry(new Point(0, 0)).Bounds;
        if (inkBounds.IsEmpty) return;
        var x = Snap((ActualWidth - inkBounds.Width) / 2 - inkBounds.X, dpi.DpiScaleX);
        var y = Snap((ActualHeight - inkBounds.Height) / 2 - inkBounds.Y, dpi.DpiScaleY);
        context.DrawText(glyph, new Point(x, y));
    }

    private static double Snap(double value, double scale) => Math.Round(value * scale) / scale;

    private static string GlyphFor(CaptionButtonKind kind) => kind switch
    {
        CaptionButtonKind.Minimize => "",
        CaptionButtonKind.Maximize => "",
        CaptionButtonKind.Restore => "",
        _ => "",
    };
}

/// Minimize, maximize (restore while maximized) and close at the top right of the window, where Windows puts them.
public sealed class CaptionButtons : StackPanel
{
    public const double StripWidth = 3 * CaptionButton.ButtonWidth;

    public CaptionButtons()
    {
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Children.Add(Minimize);
        Children.Add(Maximize);
        Children.Add(Close);
    }

    public CaptionButton Minimize { get; } = new(CaptionButtonKind.Minimize);
    public CaptionButton Maximize { get; } = new(CaptionButtonKind.Maximize);
    public CaptionButton Close { get; } = new(CaptionButtonKind.Close);

    public bool IsMaximized
    {
        set => Maximize.Kind = value ? CaptionButtonKind.Restore : CaptionButtonKind.Maximize;
    }

    public bool IsWindowActive
    {
        set
        {
            foreach (var button in new[] { Minimize, Maximize, Close }) button.IsWindowActive = value;
        }
    }

    public void Relabel()
    {
        foreach (var button in new[] { Minimize, Maximize, Close }) button.Relabel();
    }
}
