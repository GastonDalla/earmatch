public enum UpdatePrompt {
    public static let launchWindow: Double = 120

    public static func showsAlert(immediateFocus: Bool, secondsSinceLaunch: Double, rekordboxIsFrontmost: Bool) -> Bool {
        immediateFocus && secondsSinceLaunch < launchWindow && !rekordboxIsFrontmost
    }
}
