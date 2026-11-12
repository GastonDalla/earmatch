using System.Globalization;

namespace Earmatch.Core;

public readonly record struct DisplayMonitor(ScreenRect Bounds, ScreenRect WorkArea, double Dpi)
{
    public double Scale => Dpi / 96;
}

public sealed record WindowPlacement(ScreenRect Frame, bool IsMaximized)
{
    public const string StorageKey = "mainWindowFrames";
    private const string MaximizedFlag = "maximized";

    public static string SetupKey(IEnumerable<DisplayMonitor> monitors) => string.Join(" | ", monitors
        .OrderBy(monitor => monitor.Bounds.X)
        .ThenBy(monitor => monitor.Bounds.Y)
        .Select(monitor => string.Create(CultureInfo.InvariantCulture,
            $"{monitor.Bounds.X},{monitor.Bounds.Y},{monitor.Bounds.Width}x{monitor.Bounds.Height}@{monitor.Dpi}")));

    public string StoredValue
    {
        get
        {
            var frame = string.Create(CultureInfo.InvariantCulture, $"{Frame.X} {Frame.Y} {Frame.Width} {Frame.Height}");
            return IsMaximized ? $"{frame} {MaximizedFlag}" : frame;
        }
    }

    public static WindowPlacement? FromStoredValue(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        var parts = stored.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (4 or 5)) return null;
        var numbers = new double[4];
        for (var index = 0; index < 4; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index]) || !double.IsFinite(numbers[index]))
                return null;
        }
        if (numbers[2] <= 0 || numbers[3] <= 0) return null;
        var isMaximized = parts.Length == 5 && parts[4] == MaximizedFlag;
        if (parts.Length == 5 && !isMaximized) return null;
        return new WindowPlacement(new ScreenRect(numbers[0], numbers[1], numbers[2], numbers[3]), isMaximized);
    }

    public static DisplayMonitor? MonitorFor(ScreenRect frame, IReadOnlyList<DisplayMonitor> monitors)
    {
        DisplayMonitor? best = null;
        var bestArea = 0.0;
        foreach (var monitor in monitors)
        {
            var area = Overlap(frame, monitor.Bounds);
            if (area <= bestArea) continue;
            bestArea = area;
            best = monitor;
        }
        if (best is not null) return best;
        var nearest = double.PositiveInfinity;
        foreach (var monitor in monitors)
        {
            var distance = Distance(frame.Center, monitor.Bounds);
            if (distance >= nearest) continue;
            nearest = distance;
            best = monitor;
        }
        return best;
    }

    public static ScreenRect Fit(ScreenRect frame, ScreenRect workArea, ScreenSize minimum)
    {
        var width = Math.Max(minimum.Width, Math.Min(frame.Width, workArea.Width));
        var height = Math.Max(minimum.Height, Math.Min(frame.Height, workArea.Height));
        var x = Math.Max(workArea.X, Math.Min(frame.X, workArea.MaxX - width));
        var y = Math.Max(workArea.Y, Math.Min(frame.Y, workArea.MaxY - height));
        return new ScreenRect(Math.Round(x), Math.Round(y), Math.Round(width), Math.Round(height));
    }

    public static ScreenRect Centered(WindowSize size, DisplayMonitor monitor, WindowSize minimum)
    {
        var work = monitor.WorkArea;
        var scale = monitor.Scale;
        var fitted = Fit(new ScreenRect(work.X, work.Y, size.Width * scale, size.Height * scale), work,
            new ScreenSize(minimum.Width * scale, minimum.Height * scale));
        return fitted with
        {
            X = Math.Round(Math.Max(work.X, work.X + (work.Width - fitted.Width) / 2)),
            Y = Math.Round(Math.Max(work.Y, work.Y + (work.Height - fitted.Height) / 2)),
        };
    }

    public static WindowPlacement Restored(WindowPlacement? stored, IReadOnlyList<DisplayMonitor> monitors, DisplayMonitor primary,
        WindowSize defaultSize, WindowSize minimum)
    {
        if (stored is null || MonitorFor(stored.Frame, monitors) is not { } monitor)
            return new WindowPlacement(Centered(defaultSize, primary, minimum), false);
        var scale = monitor.Scale;
        var frame = Fit(stored.Frame, monitor.WorkArea, new ScreenSize(minimum.Width * scale, minimum.Height * scale));
        return stored with { Frame = frame };
    }

    private static double Overlap(ScreenRect a, ScreenRect b)
    {
        var width = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        var height = Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY);
        return width > 0 && height > 0 ? width * height : 0;
    }

    private static double Distance(ScreenPoint point, ScreenRect rect)
    {
        var dx = Math.Max(Math.Max(rect.MinX - point.X, 0), point.X - rect.MaxX);
        var dy = Math.Max(Math.Max(rect.MinY - point.Y, 0), point.Y - rect.MaxY);
        return dx * dx + dy * dy;
    }
}
