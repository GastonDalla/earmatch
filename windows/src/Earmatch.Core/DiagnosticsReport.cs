namespace Earmatch.Core;

/// The plain-text report Copy Diagnostics puts on the clipboard, always in English. Windows has no Accessibility or
/// Screen Recording grants, so the macOS permission lines are gone.
public sealed record DiagnosticsReport(
    string AppVersion,
    string Windows,
    string Architecture,
    string? RekordboxVersion,
    OverlayActivation Activation,
    OverlaySettings Settings,
    string Extra)
{
    public string Text => L10n.English(() =>
    {
        static string OnOff(bool on) => on ? "on" : "off";
        var settings = Settings;
        var lines = new List<string>
        {
            "Earmatch diagnostics",
            $"Earmatch: {AppVersion}",
            $"Windows: {Windows}",
            $"PC: {Architecture}",
            $"rekordbox: {RekordboxVersion ?? "not running"}",
            $"Status: {Activation.Message}",
            $"Enabled: {OnOff(settings.IsEnabled)}",
            $"Limit BPM display: {OnOff(settings.IsBPMLimitEnabled)}, {settings.BPMLimit.DisplayName}",
            $"Waveform: {settings.WaveformMode.DisplayName}",
            $"Phase meter: {OnOff(settings.IsPhaseMeterEnabled)}, {settings.PhaseMeterType.Title}",
            $"Counter: {settings.CounterStyle.Title}",
            $"Full blind mode: {OnOff(settings.IsBlindModeEnabled)}",
            $"Booth: {BoothPreset.Selected(settings)?.DisplayName ?? "custom"}",
            $"Mix trainer: {OnOff(settings.IsMixTrainerEnabled)}",
        };
        var extra = Core.Text.TrimWhitespacesAndNewlines(Extra);
        if (extra.Length > 0) lines.AddRange(["", extra]);
        return string.Join("\n", lines) + "\n";
    });
}
