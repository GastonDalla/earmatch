using System.Windows;
using System.Windows.Media;
using Earmatch.Core;

namespace Earmatch.App.Theme;

public static class WindowTheme
{
    public static bool ReduceMotion => !System.Windows.SystemParameters.ClientAreaAnimation;

    public static readonly Color AccentColor = Rgb(0.72, 0.95, 0.23);
    public static readonly Color WaitingColor = Rgb(0.96, 0.65, 0.14);
    public static readonly Color OffColor = Rgb(0.42, 0.42, 0.42);

    public static readonly Brush Accent = Solid(AccentColor);
    public static readonly Brush OnAccent = Solid(Rgb(0.07, 0.09, 0.05));
    public static readonly Brush Waiting = Solid(WaitingColor);
    public static readonly Brush Off = Solid(OffColor);
    public static readonly Brush Granted = Solid(Rgb(0.30, 0.85, 0.42));

    public static readonly Color BackgroundColor = Rgb(0.110, 0.110, 0.118);
    public static readonly Color SidebarColor = Rgb(0.086, 0.086, 0.094);
    public static readonly Brush Background = Solid(BackgroundColor);
    public static readonly Brush Sidebar = Solid(SidebarColor);
    public static readonly Brush Card = Solid(Rgb(0.145, 0.145, 0.157));
    public static readonly Brush Well = Solid(SidebarColor);
    public static readonly Brush Hairline = White(0.08);

    public static readonly Brush PrimaryText = White(0.92);
    public static readonly Brush SecondaryText = White(0.58);
    public static readonly Brush SectionText = White(0.42);

    public static readonly Color MasterColor = Color.FromRgb(240, 140, 40);
    public static readonly Color DeckColor = Color.FromRgb(60, 140, 220);
    public static readonly Color DownbeatColor = Color.FromRgb(230, 50, 40);
    public static readonly Brush Master = Solid(MasterColor);
    public static readonly Brush MasterDim = Solid(Color.FromRgb(130, 72, 22));
    public static readonly Brush Deck = Solid(DeckColor);
    public static readonly Brush DeckDim = Solid(Color.FromRgb(30, 68, 110));
    public static readonly Brush DeckTwo = Solid(Color.FromRgb(70, 200, 215));
    public static readonly Brush Downbeat = Solid(DownbeatColor);
    public static readonly Color CdjOrangeColor = Color.FromRgb(255, 115, 0);
    public static readonly Color CdjBlueColor = Color.FromRgb(0, 125, 255);
    public static readonly Brush CdjOrange = Solid(CdjOrangeColor);
    public static readonly Brush CdjBlue = Solid(CdjBlueColor);
    public static readonly Brush TickGrey = Solid(Color.FromRgb(205, 205, 205));
    public static readonly Brush Black = Solid(Colors.Black);
    public static readonly Brush WhiteBrush = Solid(Colors.White);

    public static Color ColorFor(StatusTone tone) => tone switch
    {
        StatusTone.Live => AccentColor,
        StatusTone.Waiting => WaitingColor,
        _ => OffColor,
    };

    public static Brush BrushFor(StatusTone tone) => tone switch
    {
        StatusTone.Live => Accent,
        StatusTone.Waiting => Waiting,
        _ => Off,
    };

    public static Color Rgb(double red, double green, double blue) =>
        Color.FromRgb(Channel(red), Channel(green), Channel(blue));

    public static Brush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static Brush Solid(Color color, double opacity) => Solid(WithOpacity(color, opacity));

    public static Brush White(double opacity) => Solid(Colors.White, opacity);

    public static Color WithOpacity(Color color, double opacity) => Color.FromArgb(Channel(opacity * color.A / 255), color.R, color.G, color.B);

    private static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}

public static class Spacing
{
    public const double Tight = 4;
    public const double Small = 8;
    public const double Medium = 16;
    public const double Large = 24;
    public const double ExtraLarge = 32;
}

public static class AppFonts
{
    public static readonly FontFamily Text = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static double LineHeight(double size) => Math.Round(size * 1.2, 1);
}

public readonly record struct TypeStyle(double Size, FontWeight Weight, bool IsMono = false)
{
    public FontFamily Family => IsMono ? AppFonts.Mono : Size >= 20 ? AppFonts.Display : AppFonts.Text;

    public static readonly TypeStyle Title = new(24, FontWeights.Bold);
    public static readonly TypeStyle PaneTitle = new(20, FontWeights.Bold);
    public static readonly TypeStyle Headline = new(15, FontWeights.SemiBold);
    public static readonly TypeStyle Body = new(13, FontWeights.Normal);
    public static readonly TypeStyle BodyStrong = new(13, FontWeights.SemiBold);
    public static readonly TypeStyle Caption = new(12, FontWeights.Normal);
    public static readonly TypeStyle Status = new(12, FontWeights.Normal, IsMono: true);
    public static readonly TypeStyle Section = new(11, FontWeights.Bold);
    public static readonly TypeStyle Label = new(10.5, FontWeights.SemiBold, IsMono: true);
    public static readonly TypeStyle Counter = new(12.5, FontWeights.Medium);
}
