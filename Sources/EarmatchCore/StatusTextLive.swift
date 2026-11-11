import Foundation

extension StatusText {
    public static func liveDecks(_ context: StatusContext, snapshot: LiveDecksSnapshot?) -> StatusLine {
        if let blocked = blocked(context, featureOff: nil) { return blocked }
        guard context.isScreenRecordingGranted else {
            return StatusLine(tone: .waiting, text: L10n.text("Allow Screen Recording to mirror the decks"))
        }
        if let problem = captureProblem(context) { return problem }
        guard let snapshot, !snapshot.decks.isEmpty else {
            return StatusLine(tone: .waiting, text: L10n.text("Reading the decks…"))
        }
        let fps = snapshot.framesPerSecond, latency = snapshot.latencyMilliseconds
        let text = switch (fps > 0, latency > 0) {
        case (true, true): L10n.format("Live · %.0f fps · %.1f ms", fps, latency)
        case (true, false): L10n.format("Live · %.0f fps", fps)
        case (false, true): L10n.format("Live · %.1f ms", latency)
        case (false, false): L10n.text("Live")
        }
        return StatusLine(tone: .live, text: text)
    }

    public static func isMirroringDecks(_ context: StatusContext, snapshot: LiveDecksSnapshot?) -> Bool {
        context.settings.isEnabled && context.activation.isActive && context.isScreenRecordingGranted
            && captureProblem(context) == nil && !(snapshot?.decks.isEmpty ?? true)
    }
}
