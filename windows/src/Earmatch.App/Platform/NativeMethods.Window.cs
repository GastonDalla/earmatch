using System.Runtime.InteropServices;
using Earmatch.Core;

namespace Earmatch.App;

internal static partial class NativeMethods
{
    private const uint MonitorInfoPrimary = 1;
    private const int MonitorDpiEffective = 0;
    public const uint SwpNoZOrder = 0x0004;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip,
        delegate* unmanaged<IntPtr, IntPtr, Rect32*, IntPtr, int> callback, IntPtr data);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint processId);

    public static (IReadOnlyList<DisplayMonitor> Monitors, DisplayMonitor? Primary) DisplayMonitors()
    {
        var found = new List<(DisplayMonitor Monitor, bool IsPrimary)>();
        var handle = GCHandle.Alloc(found);
        try
        {
            unsafe
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, &OnMonitor, GCHandle.ToIntPtr(handle));
            }
        }
        finally
        {
            handle.Free();
        }
        DisplayMonitor? primary = null;
        foreach (var (monitor, isPrimary) in found)
        {
            if (isPrimary) primary = monitor;
        }
        return (found.ConvertAll(entry => entry.Monitor), primary ?? (found.Count > 0 ? found[0].Monitor : null));
    }

    [UnmanagedCallersOnly]
    private static unsafe int OnMonitor(IntPtr monitor, IntPtr hdc, Rect32* clip, IntPtr data)
    {
        if (GCHandle.FromIntPtr(data).Target is not List<(DisplayMonitor, bool)> found) return 0;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return 1;
        var dpi = GetDpiForMonitor(monitor, MonitorDpiEffective, out var dpiX, out _) == 0 && dpiX > 0 ? dpiX : 96u;
        found.Add((new DisplayMonitor(Screen(info.Monitor), Screen(info.Work), dpi), (info.Flags & MonitorInfoPrimary) != 0));
        return 1;
    }

    private static ScreenRect Screen(Rect32 rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    public static bool AllowForeground(int processId) => AllowSetForegroundWindow((uint)processId);
}
