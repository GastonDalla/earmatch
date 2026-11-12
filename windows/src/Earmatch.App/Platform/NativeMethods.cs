using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Earmatch.App;

internal static partial class NativeMethods
{
    public const int WmNcHitTest = 0x0084;
    public const int WmNcMouseMove = 0x00A0;
    public const int WmNcLButtonDown = 0x00A1;
    public const int WmNcLButtonUp = 0x00A2;
    public const int WmNcLButtonDblClk = 0x00A3;
    public const int WmNcMouseLeave = 0x02A2;

    public const int HtTop = 12;
    public const int HtTopRight = 14;
    public const int HtMaxButton = 9;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwcpRound = 2;
    private const uint TmeLeave = 0x00000002;
    private const uint TmeNonClient = 0x00000010;
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect32 Monitor;
        public Rect32 Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackMouseEventInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Window;
        public uint HoverTime;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(int processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TrackMouseEvent(ref TrackMouseEventInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(IntPtr hwnd, out Rect32 rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr hwnd, ref Point32 point);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    public static void StyleFrame(Window window, Color border)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = 1;
        DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var round = DwmwcpRound;
        DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref round, sizeof(int));
        var colour = ColorRef(border);
        DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref colour, sizeof(int));
        DwmSetWindowAttribute(handle, DwmwaBorderColor, ref colour, sizeof(int));
    }

    public static void TrackNonClientLeave(IntPtr hwnd)
    {
        var info = new TrackMouseEventInfo { Size = Marshal.SizeOf<TrackMouseEventInfo>(), Flags = TmeLeave | TmeNonClient, Window = hwnd };
        TrackMouseEvent(ref info);
    }

    public static Point ScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new Point(unchecked((short)value), unchecked((short)(value >> 16)));
    }

    public static Thickness MaximizedOverhang(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetClientRect(handle, out var client)) return new Thickness(0);
        var origin = new Point32();
        if (!ClientToScreen(handle, ref origin)) return new Thickness(0);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info)) return new Thickness(0);
        var work = info.Work;
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Thickness(
            Math.Max(0, work.Left - origin.X) / dpi.DpiScaleX,
            Math.Max(0, work.Top - origin.Y) / dpi.DpiScaleY,
            Math.Max(0, origin.X + client.Right - work.Right) / dpi.DpiScaleX,
            Math.Max(0, origin.Y + client.Bottom - work.Bottom) / dpi.DpiScaleY);
    }

    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
}
