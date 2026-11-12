namespace Earmatch.Core;

public enum OverlayActivationKind
{
    RekordboxNotRunning,
    MainWindowNotFound,
    RekordboxNotFrontmost,
    WrongMode,
    WrongLayout,
    DecksNotFound,
    Active,
}

/// Whether the overlays can run on rekordbox, and what the user should do when they cannot. Windows needs no
/// Accessibility or Screen Recording permission, so the macOS accessibilityMissing state has no counterpart.
public sealed record OverlayActivation
{
    private OverlayActivation(OverlayActivationKind kind, RekordboxMode mode = RekordboxMode.Unknown, string? layoutName = null)
    {
        Kind = kind;
        Mode = mode;
        LayoutName = layoutName;
    }

    public OverlayActivationKind Kind { get; }

    /// The mode rekordbox is in, for WrongMode.
    public RekordboxMode Mode { get; }

    /// The layout rekordbox shows, for WrongLayout.
    public string? LayoutName { get; }

    public static OverlayActivation RekordboxNotRunning { get; } = new(OverlayActivationKind.RekordboxNotRunning);
    public static OverlayActivation MainWindowNotFound { get; } = new(OverlayActivationKind.MainWindowNotFound);
    public static OverlayActivation RekordboxNotFrontmost { get; } = new(OverlayActivationKind.RekordboxNotFrontmost);
    public static OverlayActivation DecksNotFound { get; } = new(OverlayActivationKind.DecksNotFound);
    public static OverlayActivation Active { get; } = new(OverlayActivationKind.Active);

    public static OverlayActivation WrongMode(RekordboxMode mode) => new(OverlayActivationKind.WrongMode, mode: mode);

    public static OverlayActivation WrongLayout(string? layoutName) => new(OverlayActivationKind.WrongLayout, layoutName: layoutName);

    public static OverlayActivation Evaluate(bool isRunning, RekordboxLayout? layout, bool isFrontmost)
    {
        if (!isRunning) return RekordboxNotRunning;
        if (layout is null) return MainWindowNotFound;
        if (!isFrontmost) return RekordboxNotFrontmost;
        if (layout.Mode != RekordboxMode.Performance) return WrongMode(layout.Mode);
        if (layout.Arrangement is null) return WrongLayout(layout.LayoutName);
        if (!layout.IsSupportedPerformanceLayout) return DecksNotFound;
        return Active;
    }

    public bool IsActive => Kind == OverlayActivationKind.Active;

    public string Message => Kind switch
    {
        OverlayActivationKind.RekordboxNotRunning => L10n.Text("Waiting for rekordbox 7"),
        OverlayActivationKind.MainWindowNotFound => L10n.Text("Waiting for the rekordbox window"),
        OverlayActivationKind.RekordboxNotFrontmost => L10n.Text("Paused until rekordbox is in front"),
        OverlayActivationKind.WrongMode => L10n.Text("Switch rekordbox to PERFORMANCE mode"),
        OverlayActivationKind.WrongLayout when LayoutName == "Browse" => L10n.Text("Browse view is open; switch back to a 2Deck or 4Deck layout"),
        OverlayActivationKind.WrongLayout => L10n.Text("Switch the rekordbox layout to a 2Deck or 4Deck layout"),
        OverlayActivationKind.DecksNotFound => L10n.Text("Waiting for the decks and the waveform"),
        _ => L10n.Text("Active on rekordbox"),
    };
}
