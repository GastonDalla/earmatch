using System.Runtime.InteropServices;

namespace Earmatch.App;

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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private unsafe struct DwmTimingInfo
    {
        public const int PackedSizeInDwmapiH = 292;
        private const int SizeUpToQpcVBlank = sizeof(uint) + 2 * sizeof(uint) + sizeof(ulong) + 2 * sizeof(uint) + sizeof(ulong);

        public uint Size;
        public UnsignedRatio RateRefresh;
        public ulong QpcRefreshPeriod;
        public UnsignedRatio RateCompose;
        public ulong QpcVBlank;
        public fixed byte FieldsAfterQpcVBlankNotRead[PackedSizeInDwmapiH - SizeUpToQpcVBlank];
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
    private static partial int DwmGetCompositionTimingInfo(IntPtr window, ref DwmTimingInfo info);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsShown(IntPtr window);

    public static bool ExtendedFrameBounds(IntPtr window, out WindowRect rect) =>
        DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out rect, Marshal.SizeOf<WindowRect>()) >= 0;

    public static bool ClientBounds(IntPtr window, out WindowRect rect)
    {
        rect = default;
        if (!GetClientRect(window, out var client)) return false;
        var origin = new Point32();
        if (!ClientToScreen(window, ref origin)) return false;
        rect = new WindowRect { Left = origin.X, Top = origin.Y, Right = origin.X + client.Right, Bottom = origin.Y + client.Bottom };
        return true;
    }

    public static bool CompositionTiming(out long vblank, out long period)
    {
        var info = new DwmTimingInfo { Size = DwmTimingInfo.PackedSizeInDwmapiH };
        var ok = DwmGetCompositionTimingInfo(IntPtr.Zero, ref info) >= 0 && info.QpcRefreshPeriod > 0;
        vblank = ok ? (long)info.QpcVBlank : 0;
        period = ok ? (long)info.QpcRefreshPeriod : 0;
        return ok;
    }

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
