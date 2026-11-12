using System.Runtime.InteropServices;

namespace Earmatch.App;

/// The Win32 calls of the rekordbox monitor, the overlays and the hotkeys.
internal static partial class NativeMethods
{
    public const int WhKeyboardLl = 13;
    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;
    public const int WmSysKeyDown = 0x0104;
    public const int WmSysKeyUp = 0x0105;
    public const uint WmQuit = 0x0012;
    public const uint WmApp = 0x8000;
    public const int WmDpiChanged = 0x02E0;

    public const uint EventSystemForeground = 0x0003;
    public const uint EventSystemMinimizeStart = 0x0016;
    public const uint EventSystemMinimizeEnd = 0x0017;
    public const uint EventObjectShow = 0x8002;
    public const uint EventObjectHide = 0x8003;
    public const uint EventObjectLocationChange = 0x800B;
    public const uint WinEventOutOfContext = 0x0000;
    public const int ObjIdWindow = 0;

    public const int WsPopup = unchecked((int)0x80000000);
    public const int WsExTopmost = 0x00000008;
    public const int WsExTransparent = 0x00000020;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExLayered = 0x00080000;
    public const int WsExNoActivate = 0x08000000;

    public static readonly IntPtr HwndTopmost = new(-1);
    public const uint SwpNoActivate = 0x0010;
    public const uint SwpShowWindow = 0x0040;
    public const uint SwpNoOwnerZOrder = 0x0200;
    public const int SwHide = 0;

    private const uint GwOwner = 4;
    private const uint GaRoot = 2;
    private const uint PmNoRemove = 0x0000;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardHookInfo
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Message
    {
        public IntPtr Window;
        public uint Id;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    // Hooks take delegates, which the source-generated LibraryImport does not marshal.
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int hook, LowLevelKeyboardProc procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc procedure, uint processId,
        uint threadId, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc procedure, IntPtr parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(IntPtr hook);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(IntPtr hook);

    [LibraryImport("user32.dll")]
    public static partial IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? name);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out Message message, IntPtr window, uint filterMin, uint filterMax);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Message message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial IntPtr DispatchMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr window, out WindowRect rect);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindow(IntPtr window, uint command);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetAncestor(IntPtr window, uint flags);

    /// Whether a window is a top-level window rather than a child of another window.
    public static bool IsTopLevel(IntPtr window) => window != IntPtr.Zero && GetAncestor(window, GaRoot) == window;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW")]
    private static unsafe partial int GetWindowText(IntPtr window, char* text, int count);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(IntPtr window);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr icon);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(IntPtr process, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    /// Creates the calling thread's message queue, so messages posted to it before its loop starts are kept.
    public static void CreateMessageQueue() => PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);

    /// The visible top-level windows of a process that no other window owns, with their titles.
    public static List<(IntPtr Window, string Title)> TopLevelWindows(int processId)
    {
        var windows = new List<(IntPtr, string)>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner == processId && IsWindowVisible(window) && GetWindow(window, GwOwner) == IntPtr.Zero) windows.Add((window, Title(window)));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static unsafe string Title(IntPtr window)
    {
        var buffer = stackalloc char[256];
        var length = GetWindowText(window, buffer, 256);
        return length <= 0 ? "" : new string(buffer, 0, length);
    }

    /// The path of a process's executable; it works for elevated processes too, which only allow limited queries.
    public static unsafe string? ProcessImagePath(int processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            var buffer = stackalloc char[1024];
            uint size = 1024;
            return QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
