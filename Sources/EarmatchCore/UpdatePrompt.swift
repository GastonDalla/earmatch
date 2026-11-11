public enum UpdatePrompt {
    public static let launchWindow: Double = 120

    public static func asksNow(secondsSinceLaunch: Double, rekordboxIsFrontmost: Bool) -> Bool {
        secondsSinceLaunch < launchWindow && !rekordboxIsFrontmost
    }
}
