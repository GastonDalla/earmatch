public struct MainWindowListCheck: Equatable, Sendable {
    public static let mainWindowTitle = "rekordbox"

    private var sawEmptyList = false

    public init() {}

    public mutating func isMainWindowGone(windowTitles: [String]?) -> Bool {
        guard let windowTitles else {
            sawEmptyList = false
            return false
        }
        guard !windowTitles.isEmpty else {
            defer { sawEmptyList = true }
            return sawEmptyList
        }
        sawEmptyList = false
        return !windowTitles.contains(Self.mainWindowTitle)
    }

    public mutating func mainWindowFound() {
        sawEmptyList = false
    }
}
