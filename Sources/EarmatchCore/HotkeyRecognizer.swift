public struct HotkeyRecognizer: Sendable {
    public struct Modifiers: OptionSet, Sendable {
        public let rawValue: UInt

        public init(rawValue: UInt) {
            self.rawValue = rawValue
        }

        public static let capsLock = Modifiers(rawValue: 1 << 16)
        public static let shift = Modifiers(rawValue: 1 << 17)
        public static let control = Modifiers(rawValue: 1 << 18)
        public static let option = Modifiers(rawValue: 1 << 19)
        public static let command = Modifiers(rawValue: 1 << 20)
        public static let numericPad = Modifiers(rawValue: 1 << 21)
        public static let function = Modifiers(rawValue: 1 << 23)
        public static let rightOptionDevice = Modifiers(rawValue: 0x40)

        static let deviceIndependent = Modifiers(rawValue: 0xFFFF_0000)
    }

    public enum Event: Sendable {
        case flagsChanged(keyCode: UInt16, modifiers: Modifiers)
        case keyDown(characters: String?, modifiers: Modifiers, isRepeat: Bool)
    }

    public struct Outcome: Equatable, Sendable {
        public var peek: Bool?
        public var togglesEnabled: Bool

        public init(peek: Bool?, togglesEnabled: Bool) {
            self.peek = peek
            self.togglesEnabled = togglesEnabled
        }

        public static let none = Outcome(peek: nil, togglesEnabled: false)
    }

    public static let rightOptionKeyCode: UInt16 = 61
    static let ignored: Modifiers = [.capsLock, .function, .numericPad]
    static let toggleModifiers: Modifiers = [.control, .option, .command]

    public private(set) var isPeeking = false

    public init() {}

    public mutating func handle(_ event: Event) -> Outcome {
        switch event {
        case let .flagsChanged(keyCode, modifiers):
            let others = Self.held(modifiers).subtracting(.option)
            if keyCode == Self.rightOptionKeyCode {
                return setPeeking(modifiers.contains(.rightOptionDevice) && others.isEmpty)
            }
            return others.isEmpty ? .none : setPeeking(false)
        case let .keyDown(characters, modifiers, isRepeat):
            var outcome = setPeeking(false)
            outcome.togglesEnabled = !isRepeat && Self.held(modifiers) == Self.toggleModifiers
                && characters?.lowercased() == "e"
            return outcome
        }
    }

    public mutating func reset() -> Outcome {
        setPeeking(false)
    }

    private mutating func setPeeking(_ peeking: Bool) -> Outcome {
        guard peeking != isPeeking else { return .none }
        isPeeking = peeking
        return Outcome(peek: peeking, togglesEnabled: false)
    }

    private static func held(_ modifiers: Modifiers) -> Modifiers {
        modifiers.intersection(.deviceIndependent).subtracting(ignored)
    }
}
