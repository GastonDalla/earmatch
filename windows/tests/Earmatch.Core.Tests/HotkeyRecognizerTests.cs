using Earmatch.Core;
using Key = Earmatch.Core.HotkeyRecognizer.VirtualKey;

namespace Earmatch.Core.Tests;

public class HotkeyRecognizerTests
{
    const int A = 0x41;
    const int R = 0x52;

    static HotkeyRecognizer.Event Down(int key, int scanCode = 0) => HotkeyRecognizer.Event.KeyDown(key, scanCode);
    static HotkeyRecognizer.Event Up(int key, int scanCode = 0) => HotkeyRecognizer.Event.KeyUp(key, scanCode);

    static readonly HotkeyRecognizer.Outcome PeekStarts = new(Peek: true, TogglesEnabled: false);
    static readonly HotkeyRecognizer.Outcome PeekEnds = new(Peek: false, TogglesEnabled: false);
    static readonly HotkeyRecognizer.Outcome Toggles = new(Peek: null, TogglesEnabled: true, Swallows: true, SendsMaskKey: true);

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

        var outcomes = Press(recognizer, Down(Key.LeftControl, HotkeyRecognizer.AltGrFakeLeftControlScanCode), Down(Key.RightAlt));

        Assert.Equal([HotkeyRecognizer.Outcome.None, PeekStarts], outcomes);
        Assert.Equal([PeekEnds, HotkeyRecognizer.Outcome.None],
            Press(recognizer, Up(Key.RightAlt), Up(Key.LeftControl, HotkeyRecognizer.AltGrFakeLeftControlScanCode)));
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

    [Fact]
    public void TheShortcutsEIsSwallowedSoWindowsDoesNotOpenExplorer()
    {
        var recognizer = new HotkeyRecognizer();

        var outcomes = Press(recognizer, Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E), Down(Key.E), Up(Key.E));

        Assert.Equal([false, false, false, true, true, true], outcomes.Select(outcome => outcome.Swallows));
        Assert.Equal(Toggles, outcomes[3]);
    }

    [Fact]
    public void ASwallowedEStaysSwallowedWhenItIsReleasedAfterTheModifiers()
    {
        var recognizer = new HotkeyRecognizer();
        Press(recognizer, Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E));

        var outcomes = Press(recognizer, Up(Key.LeftWindows), Up(Key.LeftAlt), Up(Key.LeftControl), Up(Key.E), Down(Key.E), Up(Key.E));

        Assert.Equal([false, false, false, true, false, false], outcomes.Select(outcome => outcome.Swallows));
    }

    [Fact]
    public void EveryOtherKeyReachesTheAppsUntouched()
    {
        HotkeyRecognizer.Event[][] presses =
        [
            [Down(Key.RightAlt), Up(Key.RightAlt)],
            [Down(Key.LeftControl, HotkeyRecognizer.AltGrFakeLeftControlScanCode), Down(Key.RightAlt), Up(Key.LeftControl, HotkeyRecognizer.AltGrFakeLeftControlScanCode), Up(Key.RightAlt)],
            [Down(Key.LeftWindows), Down(Key.E), Up(Key.E), Up(Key.LeftWindows)],
            [Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.E), Up(Key.E)],
            [Down(Key.LeftShift), Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E), Up(Key.E)],
            [Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(R), Up(R)],
            [Down(Key.E), Up(Key.E), Down(A), Up(A)],
        ];

        foreach (var press in presses)
        {
            Assert.All(Press(new HotkeyRecognizer(), press), outcome => Assert.False(outcome.Swallows));
        }
    }

    [Fact]
    public void ResetForgetsASwallowedE()
    {
        var recognizer = new HotkeyRecognizer();
        Press(recognizer, Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows), Down(Key.E));

        recognizer.Reset();

        Assert.False(recognizer.Handle(Up(Key.E)).Swallows);
    }

    [Fact]
    public void TheToggleMasksTheModifiersRelease()
    {
        var recognizer = new HotkeyRecognizer();
        Press(recognizer, Down(Key.LeftControl), Down(Key.LeftAlt), Down(Key.LeftWindows));

        Assert.True(recognizer.Handle(Down(Key.E)).SendsMaskKey);
        Assert.False(recognizer.Handle(Down(Key.E)).SendsMaskKey);
        Assert.False(recognizer.Handle(Up(Key.E)).SendsMaskKey);
    }

    [Fact]
    public void TheMaskKeyIsIgnored()
    {
        var recognizer = Peeking();

        Assert.Equal(HotkeyRecognizer.Outcome.None, recognizer.Handle(Down(HotkeyRecognizer.MaskKey)));
        Assert.Equal(HotkeyRecognizer.Outcome.None, recognizer.Handle(Up(HotkeyRecognizer.MaskKey)));
        Assert.True(recognizer.IsPeeking);
    }

    [Fact]
    public void OnlyTheToggleSendsTheMaskKey()
    {
        var recognizer = new HotkeyRecognizer();

        Assert.All(Press(recognizer, Down(Key.LeftWindows), Down(Key.E), Up(Key.E), Up(Key.LeftWindows), Down(Key.RightAlt), Up(Key.RightAlt)),
            outcome => Assert.False(outcome.SendsMaskKey));
    }
}
