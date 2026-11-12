using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Earmatch.App;

internal static partial class NativeMethods
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(int processId);

    /// Gives the window a dark title bar in the sidebar's colour (Windows 11; Windows 10 keeps its own colours).
    public static void UseDarkTitleBar(Window window, Color caption, Color text)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = 1;
        DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var captionColour = ColorRef(caption);
        DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColour, sizeof(int));
        DwmSetWindowAttribute(handle, DwmwaBorderColor, ref captionColour, sizeof(int));
        var textColour = ColorRef(text);
        DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColour, sizeof(int));
    }

    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
}
