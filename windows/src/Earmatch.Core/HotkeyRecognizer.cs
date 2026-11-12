namespace Earmatch.Core;

/// Recognizes Earmatch's keys in the key-down and key-up events of a Windows low-level keyboard hook (which only
/// observes keys; it never swallows or sends any): hold Right Alt alone to peek under the overlays (any other key
/// pressed with it ends the peek; Left Alt and the lock keys do not), and Ctrl+Alt+Windows+E turns Earmatch on or off.
/// Right Alt stands for the macOS Right Option and Windows for Command. The recognizer tracks the held keys itself, so
/// key repeats are recognized; Reset forgets them when the hook may have missed a release.
public sealed class HotkeyRecognizer
{
    /// The Windows virtual-key codes the recognizer knows (winuser.h).
    public static class VirtualKey
    {
        public const int Shift = 0x10; // VK_SHIFT
        public const int Control = 0x11; // VK_CONTROL
        public const int Alt = 0x12; // VK_MENU
        public const int CapsLock = 0x14; // VK_CAPITAL
        public const int E = 0x45;
        public const int LeftWindows = 0x5B; // VK_LWIN
        public const int RightWindows = 0x5C; // VK_RWIN
        public const int NumLock = 0x90; // VK_NUMLOCK
        public const int ScrollLock = 0x91; // VK_SCROLL
        public const int LeftShift = 0xA0; // VK_LSHIFT
        public const int RightShift = 0xA1; // VK_RSHIFT
        public const int LeftControl = 0xA2; // VK_LCONTROL
        public const int RightControl = 0xA3; // VK_RCONTROL
        public const int LeftAlt = 0xA4; // VK_LMENU
        public const int RightAlt = 0xA5; // VK_RMENU
    }

    /// The scan code of the Left Ctrl that Windows sends along with Right Alt on keyboard layouts where Right Alt is
    /// AltGr (Spanish, German, French...). It is not a key the user pressed, so it is ignored.
    public const int AltGrControlScanCode = 0x21D;

    public readonly record struct Event(int VirtualKey, bool IsDown, int ScanCode = 0)
    {
        public static Event KeyDown(int virtualKey, int scanCode = 0) => new(virtualKey, true, scanCode);
        public static Event KeyUp(int virtualKey, int scanCode = 0) => new(virtualKey, false, scanCode);
    }

    /// Peek is the new peek state when it changed, null otherwise.
    public readonly record struct Outcome(bool? Peek, bool TogglesEnabled)
    {
        public static Outcome None => new(null, false);
    }

    [Flags]
    private enum Modifiers
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4,
        Windows = 8,
    }

    private const Modifiers ToggleModifiers = Modifiers.Control | Modifiers.Alt | Modifiers.Windows;

    private readonly HashSet<int> _held = [];

    public bool IsPeeking { get; private set; }

    public Outcome Handle(Event keyEvent)
    {
        var key = keyEvent.VirtualKey;
        if (IsLockKey(key) || (key == VirtualKey.LeftControl && keyEvent.ScanCode == AltGrControlScanCode)) return Outcome.None;
        var isRepeat = keyEvent.IsDown && !_held.Add(key);
        if (!keyEvent.IsDown) _held.Remove(key);
        if (key == VirtualKey.RightAlt)
        {
            return isRepeat ? Outcome.None : SetPeeking(keyEvent.IsDown && Others() == Modifiers.None);
        }
        if (ModifierOf(key) != Modifiers.None)
        {
            return isRepeat || Others() == Modifiers.None ? Outcome.None : SetPeeking(false);
        }
        if (!keyEvent.IsDown) return Outcome.None;
        var outcome = SetPeeking(false);
        return outcome with { TogglesEnabled = !isRepeat && Held() == ToggleModifiers && key == VirtualKey.E };
    }

    /// Ends a peek and forgets the held keys, for when the hook may have missed a release (focus or session changes).
    public Outcome Reset()
    {
        _held.Clear();
        return SetPeeking(false);
    }

    private Outcome SetPeeking(bool peeking)
    {
        if (peeking == IsPeeking) return Outcome.None;
        IsPeeking = peeking;
        return new Outcome(peeking, false);
    }

    private Modifiers Held() => _held.Aggregate(Modifiers.None, (held, key) => held | ModifierOf(key));

    /// The held modifiers other than Alt, which may join a peek.
    private Modifiers Others() => Held() & ~Modifiers.Alt;

    private static Modifiers ModifierOf(int key) => key switch
    {
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => Modifiers.Shift,
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => Modifiers.Control,
        VirtualKey.Alt or VirtualKey.LeftAlt or VirtualKey.RightAlt => Modifiers.Alt,
        VirtualKey.LeftWindows or VirtualKey.RightWindows => Modifiers.Windows,
        _ => Modifiers.None,
    };

    private static bool IsLockKey(int key) => key is VirtualKey.CapsLock or VirtualKey.NumLock or VirtualKey.ScrollLock;
}
