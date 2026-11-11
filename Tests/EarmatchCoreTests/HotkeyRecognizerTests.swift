import Testing
import EarmatchCore

@Suite struct HotkeyRecognizerTests {
    typealias Modifiers = HotkeyRecognizer.Modifiers

    static let rightOption: UInt16 = 61
    static let leftOption: UInt16 = 58
    static let leftShift: UInt16 = 56
    static let command: UInt16 = 55
    static let leftOptionDevice = Modifiers(rawValue: 0x20)
    static let leftShiftDevice = Modifiers(rawValue: 0x02)
    static let leftCommandDevice = Modifiers(rawValue: 0x08)
    static let noise = Modifiers(rawValue: 0x100)

    static let rightOptionDown: Modifiers = [.option, .rightOptionDevice, noise]

    func peeking() -> HotkeyRecognizer {
        var recognizer = HotkeyRecognizer()
        _ = recognizer.handle(.flagsChanged(keyCode: Self.rightOption, modifiers: Self.rightOptionDown))
        return recognizer
    }

    func keyDown(_ characters: String?, _ modifiers: Modifiers, isRepeat: Bool = false) -> HotkeyRecognizer.Event {
        .keyDown(characters: characters, modifiers: modifiers, isRepeat: isRepeat)
    }

    @Test func holdingRightOptionAloneStartsAPeek() {
        var recognizer = HotkeyRecognizer()

        let outcome = recognizer.handle(.flagsChanged(keyCode: Self.rightOption, modifiers: Self.rightOptionDown))

        #expect(outcome == HotkeyRecognizer.Outcome(peek: true, togglesEnabled: false))
        #expect(recognizer.isPeeking)
    }

    @Test func releasingRightOptionEndsThePeek() {
        var recognizer = peeking()

        let outcome = recognizer.handle(.flagsChanged(keyCode: Self.rightOption, modifiers: Self.noise))

        #expect(outcome == HotkeyRecognizer.Outcome(peek: false, togglesEnabled: false))
        #expect(!recognizer.isPeeking)
    }

    @Test func leftOptionNeverPeeks() {
        var recognizer = HotkeyRecognizer()

        let outcome = recognizer.handle(.flagsChanged(keyCode: Self.leftOption, modifiers: [.option, Self.leftOptionDevice, Self.noise]))

        #expect(outcome == .none)
        #expect(!recognizer.isPeeking)
    }

    @Test func rightOptionPressedWithAnotherModifierHeldIsAShortcutNotAPeek() {
        var recognizer = HotkeyRecognizer()

        let outcome = recognizer.handle(.flagsChanged(
            keyCode: Self.rightOption, modifiers: [.command, .option, .rightOptionDevice, Self.leftCommandDevice, Self.noise]))

        #expect(outcome == .none)
        #expect(!recognizer.isPeeking)
    }

    @Test func anotherModifierJoiningInCancelsThePeek() {
        var recognizer = peeking()

        let outcome = recognizer.handle(.flagsChanged(
            keyCode: Self.leftShift, modifiers: [.shift, .option, .rightOptionDevice, Self.leftShiftDevice, Self.noise]))

        #expect(outcome == HotkeyRecognizer.Outcome(peek: false, togglesEnabled: false))
        #expect(!recognizer.isPeeking)
    }

    @Test func leftOptionJoiningInKeepsThePeek() {
        var recognizer = peeking()

        let outcome = recognizer.handle(.flagsChanged(
            keyCode: Self.leftOption, modifiers: [.option, .rightOptionDevice, Self.leftOptionDevice, Self.noise]))

        #expect(outcome == .none)
        #expect(recognizer.isPeeking)
    }

    @Test func capsLockFunctionAndKeypadFlagsDoNotStopAPeek() {
        var recognizer = HotkeyRecognizer()

        let outcome = recognizer.handle(.flagsChanged(
            keyCode: Self.rightOption, modifiers: [.capsLock, .function, .numericPad, .option, .rightOptionDevice, Self.noise]))

        #expect(outcome.peek == true)
    }

    @Test func anyKeyPressedDuringAPeekCancelsIt() {
        var recognizer = peeking()

        let outcome = recognizer.handle(keyDown("å", [.option, .rightOptionDevice]))

        #expect(outcome == HotkeyRecognizer.Outcome(peek: false, togglesEnabled: false))
        #expect(!recognizer.isPeeking)
    }

    @Test func aKeyWithoutAPeekChangesNothing() {
        var recognizer = HotkeyRecognizer()

        #expect(recognizer.handle(keyDown("a", [])) == .none)
    }

    @Test func resetEndsAPeekOnce() {
        var recognizer = peeking()

        #expect(recognizer.reset() == HotkeyRecognizer.Outcome(peek: false, togglesEnabled: false))
        #expect(recognizer.reset() == .none)
    }

    @Test func controlOptionCommandETogglesEarmatch() {
        var recognizer = HotkeyRecognizer()

        let outcome = recognizer.handle(keyDown("e", [.control, .option, .command]))

        #expect(outcome == HotkeyRecognizer.Outcome(peek: nil, togglesEnabled: true))
    }

    @Test func theShortcutIgnoresCapsLockAndLetterCase() {
        var recognizer = HotkeyRecognizer()

        #expect(recognizer.handle(keyDown("E", [.capsLock, .control, .option, .command])).togglesEnabled)
    }

    @Test func theShortcutNeedsExactlyControlOptionCommand() {
        var recognizer = HotkeyRecognizer()

        #expect(!recognizer.handle(keyDown("e", [.shift, .control, .option, .command])).togglesEnabled)
        #expect(!recognizer.handle(keyDown("e", [.option, .command])).togglesEnabled)
        #expect(!recognizer.handle(keyDown("e", [.control, .option])).togglesEnabled)
    }

    @Test func otherKeysAndKeyRepeatsDoNotToggle() {
        var recognizer = HotkeyRecognizer()

        #expect(!recognizer.handle(keyDown("r", [.control, .option, .command])).togglesEnabled)
        #expect(!recognizer.handle(keyDown(nil, [.control, .option, .command])).togglesEnabled)
        #expect(!recognizer.handle(keyDown("e", [.control, .option, .command], isRepeat: true)).togglesEnabled)
    }

    @Test func theShortcutPressedDuringAPeekEndsThePeekAndToggles() {
        var recognizer = peeking()

        let outcome = recognizer.handle(keyDown("e", [.control, .option, .command, .rightOptionDevice]))

        #expect(outcome == HotkeyRecognizer.Outcome(peek: false, togglesEnabled: true))
    }
}
