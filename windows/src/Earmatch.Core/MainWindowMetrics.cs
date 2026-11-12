namespace Earmatch.Core;

public readonly record struct WindowSize(double Width, double Height);

public static class MainWindowMetrics
{
    public static WindowSize DefaultSize { get; } = new(1040, 700);
    public static WindowSize MinimumSize { get; } = new(940, 640);
    public const double SidebarWidth = 200;
}
