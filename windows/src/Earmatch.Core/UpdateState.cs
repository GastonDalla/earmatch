using System.Globalization;

namespace Earmatch.Core;

public enum UpdatePhase
{
    /// No updates: a development build, or a feed that cannot be used.
    Off,
    Idle,
    Checking,
    UpToDate,

    /// A newer version was found and the user has not seen it yet: the menus say "Update Available…".
    Available,

    /// The user saw the newer version and chose Later.
    Postponed,
    Downloading,
    Restarting,
    Failed,
}

/// The updater's state for the tray menu, the Setup pane and Copy Diagnostics (AppUpdater.swift's menu title and
/// pending reminder on macOS).
public sealed record UpdateState
{
    private UpdateState(UpdatePhase phase) => Phase = phase;

    public UpdatePhase Phase { get; private init; }

    /// The release channel the installed copy follows (win-x64 or win-arm64).
    public string? Channel { get; private init; }

    /// Where the updates come from, as the diagnostics name it.
    public string? Feed { get; private init; }

    /// The newer version that was found.
    public string? Version { get; private init; }

    /// Why updates are off, or what went wrong.
    public string? Problem { get; private init; }

    public DateTimeOffset? LastCheck { get; private init; }

    public static UpdateState Off(string reason) => new(UpdatePhase.Off) { Problem = reason };

    public static UpdateState Ready(string channel, string feed) => new(UpdatePhase.Idle) { Channel = channel, Feed = feed };

    public UpdateState Checking() => this with { Phase = UpdatePhase.Checking };

    public UpdateState Checked(DateTimeOffset at, string? newVersion) => this with
    {
        Phase = newVersion is null ? UpdatePhase.UpToDate : UpdatePhase.Available,
        Version = newVersion,
        Problem = null,
        LastCheck = at,
    };

    public UpdateState CheckFailed(DateTimeOffset at, string problem) =>
        this with { Phase = UpdatePhase.Failed, Version = null, Problem = problem, LastCheck = at };

    public UpdateState Postponed() => this with { Phase = UpdatePhase.Postponed };

    public UpdateState Downloading() => this with { Phase = UpdatePhase.Downloading, Problem = null };

    public UpdateState Restarting() => this with { Phase = UpdatePhase.Restarting };

    /// The download failed: the version stays named in the diagnostics, and the next check finds it again.
    public UpdateState DownloadFailed(string problem) => this with { Phase = UpdatePhase.Failed, Problem = problem };

    /// Whether a newer version waits to be installed.
    public bool HasUpdate => Phase is UpdatePhase.Available or UpdatePhase.Postponed;

    /// Development builds show no update item, as macOS hides it without a public key.
    public bool ShowsMenuItem => Phase != UpdatePhase.Off;

    public bool IsMenuItemEnabled => Phase is not (UpdatePhase.Off or UpdatePhase.Downloading or UpdatePhase.Restarting);

    public string MenuTitle => Phase switch
    {
        UpdatePhase.Available => L10n.Text("Update Available…"),
        UpdatePhase.Downloading or UpdatePhase.Restarting => L10n.Text("Downloading Update…"),
        _ => L10n.Text("Check for Updates…"),
    };

    /// The diagnostics report's line, always in English.
    public string DiagnosticsLine
    {
        get
        {
            if (Phase == UpdatePhase.Off) return $"Updates: off ({Problem})";
            var checkedAt = LastCheck is { } at ? at.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture) : "never";
            var status = Phase switch
            {
                UpdatePhase.Idle => "not checked yet",
                UpdatePhase.Checking => "checking now",
                UpdatePhase.UpToDate => $"up to date (checked {checkedAt})",
                UpdatePhase.Available => $"{Version} available (checked {checkedAt})",
                UpdatePhase.Postponed => $"{Version} available, put off (checked {checkedAt})",
                UpdatePhase.Downloading => $"downloading {Version}",
                UpdatePhase.Restarting => $"restarting to install {Version}",
                _ when Version is not null => $"downloading {Version} failed: {Problem}",
                _ => $"last check failed ({checkedAt}): {Problem}",
            };
            return $"Updates: {Channel} channel from {Feed}; {status}";
        }
    }
}
