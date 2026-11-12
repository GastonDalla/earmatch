using Earmatch.Core;
using Key = Earmatch.Core.HotkeyRecognizer.VirtualKey;

namespace Earmatch.Core.Tests;

/// The macOS HotkeyRecognizerTests, adapted to the key-down and key-up events of a Windows low-level keyboard hook:
/// Right Alt stands for Right Option, Windows for Command.
public class HotkeyRecognizerTests
{
    const int A = 0x41;
    const int R = 0x52;

    static HotkeyRecognizer.Event Down(int key, int scanCode = 0) => HotkeyRecognizer.Event.KeyDown(key, scanCode);
    static HotkeyRecognizer.Event Up(int key, int scanCode = 0) => HotkeyRecognizer.Event.KeyUp(key, scanCode);

    static readonly HotkeyRecognizer.Outcome PeekStarts = new(Peek: true, TogglesEnabled: false);
    static readonly HotkeyRecognizer.Outcome PeekEnds = new(Peek: false, TogglesEnabled: false);
    static readonly HotkeyRecognizer.Outcome Toggles = new(Peek: null, TogglesEnabled: true);

    static HotkeyRecognizer Peeking()
    {
        var recognizer = new HotkeyRecognizer();
        recognizer.Handle(Down(Key.RightAlt));
        return recognizer;
    }

    static List<HotkeyRecognizer.Outcome> Press(HotkeyRecognizer recognizer, params HotkeyRecognizer.Event[] events) =>
        events.Select(recognizer.Handle).ToList();

    [Fact]
    public void HoldingRightAltAloneStartsAPeek()
    {
        var recognizer = new HotkeyRecognizer();

        Assert.Equal(PeekStarts, recognizer.Handle(Down(Key.RightAlt)));
        Assert.True(recognizer.IsPeeking);
    }

    [Fact]
    public void ReleasingRightAltEndsThePeek()
    {
        var recognizer = Peeking();

        Assert.Equal(PeekEnds, recognizer.Handle(Up(Key.RightAlt)));
        Assert.False(recognizer.IsPeeking);
    }

    [Fact]
    public void RightAltRepeatingWhileHeldKeepsThePeek()
    {
        var recognizer = Peeking();

        Assert.Equal(HotkeyRecognizer.Outcome.None, recognizer.Handle(Down(Key.RightAlt)));
        Assert.True(recognizer.IsPeeking);
    }

    [Fact]
    public void LeftAltNeverPeeks()
    {
        var recognizer = new HotkeyRecognizer();

        Assert.Equal(HotkeyRecognizer.Outcome.None, recognizer.Handle(Down(Key.LeftAlt)));
        Assert.False(recognizer.IsPeeking);
    }

    [Fact]
    public void RightAltPressedWithAnotherModifierHeldIsAShortcutNotAPeek()
    {
        var recognizer = new HotkeyRecognizer();

        var outcomes = Press(recognizer, Down(Key.LeftWindows), Down(Key.RightAlt));

        Assert.Equal(HotkeyRecognizer.Outcome.None, outcomes[^1]);
        Assert.False(recognizer.IsPeeking);
    }

    [Fact]
    public void AnotherModifierJoiningInCancelsThePeek()
    {
        var recognizer = Peeking();

        Assert.Equal(PeekEnds, recognizer.Handle(Down(Key.LeftShift)));
        Assert.False(recognizer.IsPeeking);
    }

    [Fact]
    public void LeftAltJoiningInKeepsThePeek()
    {
        var recognizer = Peeking();

        Assert.Equal(HotkeyRecognizer.Outcome.None, recognizer.Handle(Down(Key.LeftAlt)));
        Assert.True(recognizer.IsPeeking);
    }

    [Fact]
    public void AltGrsLeftControlDoesNotStopAPeek()
    {
        var recognizer = new HotkeyRecognizer();

        var outcomes = Press(recognizer, Down(Key.LeftControl, HotkeyRecognizer.AltGrControlScanCode), Down(Key.RightAlt));

        Assert.Equal([HotkeyRecognizer.Outcome.None, PeekStarts], outcomes);
        Assert.Equal([PeekEnds, HotkeyRecognizer.Outcome.None],
            Press(recognizer, Up(Key.RightAlt), Up(Key.LeftControl, HotkeyRecognizer.AltGrControlScanCode)));
    }

    [Fact]
    public void LockKeysDoNotStopAPeek()
    {
        var recognizer = new HotkeyRecognizer();
        Press(recognizer, Down(Key.CapsLock), Up(Key.CapsLock));

        Assert.Equal(PeekStarts, recognizer.Handle(Down(Key.RightAlt)));
        Assert.Equal([HotkeyRecognizer.Outcome.None, HotkeyRecognizer.Outcome.None], Press(recognizer, Down(Key.CapsLock), Up(Key.CapsLock)));
        Assert.True(recognizer.IsPeeking);
    }

    [Fact]
    public void AnyKeyPressedDuringAPeekCancelsIt()
    {
        var recognizer = Peeking();

        Assert.Equal(PeekEnds, recognizer.Handle(Down(A)));
        Assert.False(recognizer.IsPeeking);
    }

    [Fact]
    public void AKeyWithoutAPeekChangesNothing()
    {
        var recognizer = new HotkeyRecognizer();

        Assert.Equal([HotkeyRecognizer.Outcome.None, HotkeyRecognizer.Outcome.None], Press(recognizer, Down(A), Up(A)));
    }

    [Fact]
    public void AReleasedModifierNoLongerBlocksAPeek()
    {
        var recognizer = new HotkeyRecognizer();
        Press(recognizer, Down(Key.LeftShift), Up(Key.LeftShift));

        Assert.Equal(PeekStarts, recognizer.Handle(Down(Key.RightAlt)));
    }

    [Fact]
    public void ResetEndsAPeekOnce()
    {
        var recognizer = Peeking();

        Assert.Equal(PeekEnds, recognizer.Reset());
        Assert.Equal(HotkeyRecognizer.Outcome.None, recognizer.Reset());
    }

    [Fact]
    public void ResetForgetsKeysWhoseReleaseWasMissed()
    {
        var recognizer = new HotkeyRecognizer();
        recognizer.Handle(Down(Key.LeftShift));
        recognizer.Reset();

        Assert.Equal(PeekStarts, recognizer.Handle(Down(Key.RightAlt)));
    }

    [Fact]
    public void ControlAltWindowsETogglesEarmatch()
    {
        var recognizer = new HotkeyRecognizer();

        var outcomes = Press(recognizer, Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E));

        Assert.Equal(Toggles, outcomes[^1]);
        Assert.All(outcomes.SkipLast(1), outcome => Assert.Equal(HotkeyRecognizer.Outcome.None, outcome));
    }

    [Fact]
    public void TheShortcutIgnoresCapsLockAndWhichSideTheModifiersAreOn()
    {
        var recognizer = new HotkeyRecognizer();
        Press(recognizer, Down(Key.CapsLock), Up(Key.CapsLock));

        var outcomes = Press(recognizer, Down(Key.RightControl), Down(Key.RightAlt), Down(Key.RightWindows), Down(Key.E));

        Assert.True(outcomes[^1].TogglesEnabled);
        Assert.False(recognizer.IsPeeking);
    }

    [Fact]
    public void TheShortcutNeedsExactlyControlAltWindows()
    {
        Assert.False(Press(new HotkeyRecognizer(), Down(Key.LeftShift), Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E))[^1].TogglesEnabled);
        Assert.False(Press(new HotkeyRecognizer(), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E))[^1].TogglesEnabled);
        Assert.False(Press(new HotkeyRecognizer(), Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.E))[^1].TogglesEnabled);
    }

    [Fact]
    public void OtherKeysAndKeyRepeatsDoNotToggle()
    {
        Assert.False(Press(new HotkeyRecognizer(), Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(R))[^1].TogglesEnabled);

        var recognizer = new HotkeyRecognizer();
        var outcomes = Press(recognizer, Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E), Down(Key.E));
        Assert.True(outcomes[^2].TogglesEnabled);
        Assert.False(outcomes[^1].TogglesEnabled);

        Assert.True(Press(recognizer, Up(Key.E), Down(Key.E))[^1].TogglesEnabled);
    }

    [Fact]
    public void TheShortcutStartedDuringAPeekEndsThePeekAndToggles()
    {
        var recognizer = Peeking();

        var outcomes = Press(recognizer, Down(Key.LeftControl), Down(Key.LeftWindows), Down(Key.E));

        Assert.Equal([PeekEnds, HotkeyRecognizer.Outcome.None, Toggles], outcomes);
        Assert.False(recognizer.IsPeeking);
    }
}
