namespace Earmatch.Core;

public sealed record LiveDeckSnapshot(int Number, string? Title, bool IsMaster, PhaseMeterDisplay? Meter, double? PhaseInBar = null,
    double BeatsPerSecond = 0)
{
    public bool IsPlaying => BeatsPerSecond > 0.05;
}

public sealed record LiveDecksSnapshot(ValueList<LiveDeckSnapshot> Decks, double FramesPerSecond, double LatencyMilliseconds)
{
    public static LiveDecksSnapshot Empty { get; } = new([], 0, 0);
}

public static partial class StatusText
{
    public static StatusLine LiveDecks(StatusContext context, LiveDecksSnapshot? snapshot)
    {
        if (Blocked(context, null) is { } blocked) return blocked;
        if (CaptureProblem(context) is { } problem) return problem;
        if (snapshot is null || snapshot.Decks.Count == 0) return new StatusLine(StatusTone.Waiting, L10n.Text("Reading the decks…"));
        var fps = snapshot.FramesPerSecond;
        var latency = snapshot.LatencyMilliseconds;
        var text = (fps > 0, latency > 0) switch
        {
            (true, true) => L10n.Format("Live · %.0f fps · %.1f ms", fps, latency),
            (true, false) => L10n.Format("Live · %.0f fps", fps),
            (false, true) => L10n.Format("Live · %.1f ms", latency),
            _ => L10n.Text("Live"),
        };
        return new StatusLine(StatusTone.Live, text);
    }

    public static bool IsMirroringDecks(StatusContext context, LiveDecksSnapshot? snapshot) =>
        context.Settings.IsEnabled && context.Activation.IsActive && CaptureProblem(context) is null && snapshot is { Decks.Count: > 0 };
}
