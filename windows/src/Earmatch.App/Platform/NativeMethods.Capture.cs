using System.Runtime.InteropServices;

namespace Earmatch.App;

/// The Win32 calls of the window capture, the refresh clock and the hotkeys' mask key.
internal static partial class NativeMethods
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    public const uint LwaAlpha = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct UnsignedRatio
    {
        public uint Numerator;
        public uint Denominator;
    }

    /// DWM_TIMING_INFO (dwmapi.h, packed to 1 byte, 292 bytes): the fields read here, then the rest as bytes.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct CompositionTimingInfo
    {
        public uint Size;
        public UnsignedRatio RateRefresh;
        public ulong QpcRefreshPeriod;
        public UnsignedRatio RateCompose;
        public ulong QpcVBlank;
        public fixed byte Rest[256];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    /// INPUT with its union sized for the largest member (MOUSEINPUT), as SendInput checks cbSize.
    [StructLayout(LayoutKind.Explicit)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyboardInput Keyboard;
        [FieldOffset(8)] private MouseInputPadding _mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputPadding
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(IntPtr window, int attribute, out WindowRect value, int size);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetCompositionTimingInfo(IntPtr window, ref CompositionTimingInfo info);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsShown(IntPtr window);

    /// The window's visible bounds without its invisible resize borders (DWMWA_EXTENDED_FRAME_BOUNDS), in physical
    /// pixels: what Windows.Graphics.Capture captures of a window.
    public static bool ExtendedFrameBounds(IntPtr window, out WindowRect rect) =>
        DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out rect, Marshal.SizeOf<WindowRect>()) >= 0;

    /// The client area in physical screen pixels.
    public static bool ClientBounds(IntPtr window, out WindowRect rect)
    {
        rect = default;
        if (!GetClientRect(window, out var client)) return false;
        var origin = new Point32();
        if (!ClientToScreen(window, ref origin)) return false;
        rect = new WindowRect { Left = origin.X, Top = origin.Y, Right = origin.X + client.Right, Bottom = origin.Y + client.Bottom };
        return true;
    }

    /// The desktop composition's last vertical blank and refresh period, in performance counter ticks.
    public static bool CompositionTiming(out long vblank, out long period)
    {
        var info = new CompositionTimingInfo { Size = 292 };
        var ok = DwmGetCompositionTimingInfo(IntPtr.Zero, ref info) >= 0 && info.QpcRefreshPeriod > 0;
        vblank = ok ? (long)info.QpcVBlank : 0;
        period = ok ? (long)info.QpcRefreshPeriod : 0;
        return ok;
    }

    /// Types a key (down and up) as if from the keyboard; false when Windows refused it (another app's UIPI level).
    public static bool TypeKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { VirtualKey = virtualKey } },
            new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = KeyEventKeyUp } },
        };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }
}
