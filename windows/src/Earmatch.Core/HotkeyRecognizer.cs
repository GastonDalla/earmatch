namespace Earmatch.Core;

public sealed class HotkeyRecognizer
{
    public static class VirtualKey
    {
        public const int Shift = WinUser.VK_SHIFT;
        public const int Control = WinUser.VK_CONTROL;
        public const int Alt = WinUser.VK_MENU;
        public const int CapsLock = WinUser.VK_CAPITAL;
        public const int E = 'E';
        public const int LeftWindows = WinUser.VK_LWIN;
        public const int RightWindows = WinUser.VK_RWIN;
        public const int NumLock = WinUser.VK_NUMLOCK;
        public const int ScrollLock = WinUser.VK_SCROLL;
        public const int LeftShift = WinUser.VK_LSHIFT;
        public const int RightShift = WinUser.VK_RSHIFT;
        public const int LeftControl = WinUser.VK_LCONTROL;
        public const int RightControl = WinUser.VK_RCONTROL;
        public const int LeftAlt = WinUser.VK_LMENU;
        public const int RightAlt = WinUser.VK_RMENU;
        public const int Unassigned = WinUser.UnassignedVirtualKeyE8;

        private static class WinUser
        {
            public const int VK_SHIFT = 0x10;
            public const int VK_CONTROL = 0x11;
            public const int VK_MENU = 0x12;
            public const int VK_CAPITAL = 0x14;
            public const int VK_LWIN = 0x5B;
            public const int VK_RWIN = 0x5C;
            public const int VK_NUMLOCK = 0x90;
            public const int VK_SCROLL = 0x91;
            public const int VK_LSHIFT = 0xA0;
            public const int VK_RSHIFT = 0xA1;
            public const int VK_LCONTROL = 0xA2;
            public const int VK_RCONTROL = 0xA3;
            public const int VK_LMENU = 0xA4;
            public const int VK_RMENU = 0xA5;
            public const int UnassignedVirtualKeyE8 = 0xE8;
        }
    }

    public const int AltGrFakeLeftControlScanCode = 0x21D;

    public const int MaskKey = VirtualKey.Unassigned;

    public readonly record struct Event(int VirtualKey, bool IsDown, int ScanCode = 0)
    {
        public static Event KeyDown(int virtualKey, int scanCode = 0) => new(virtualKey, true, scanCode);
        public static Event KeyUp(int virtualKey, int scanCode = 0) => new(virtualKey, false, scanCode);
    }

    public readonly record struct Outcome(bool? Peek, bool TogglesEnabled, bool Swallows = false, bool SendsMaskKey = false)
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
    private bool _isSwallowingE;

    public bool IsPeeking { get; private set; }

    public Outcome Handle(Event keyEvent)
    {
        var key = keyEvent.VirtualKey;
        if (key == MaskKey || IsLockKey(key) || (key == VirtualKey.LeftControl && keyEvent.ScanCode == AltGrFakeLeftControlScanCode)) return Outcome.None;
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
        if (!keyEvent.IsDown)
        {
            var swallows = key == VirtualKey.E && _isSwallowingE;
            if (swallows) _isSwallowingE = false;
            return Outcome.None with { Swallows = swallows };
        }
        var outcome = SetPeeking(false);
        if (key != VirtualKey.E || Held() != ToggleModifiers) return outcome;
        _isSwallowingE = true;
        return outcome with { TogglesEnabled = !isRepeat, Swallows = true, SendsMaskKey = !isRepeat };
    }

    public Outcome Reset()
    {
        _held.Clear();
        _isSwallowingE = false;
        return SetPeeking(false);
    }

    private Outcome SetPeeking(bool peeking)
    {
        if (peeking == IsPeeking) return Outcome.None;
        IsPeeking = peeking;
        return new Outcome(peeking, false);
    }

    private Modifiers Held() => _held.Aggregate(Modifiers.None, (held, key) => held | ModifierOf(key));

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
