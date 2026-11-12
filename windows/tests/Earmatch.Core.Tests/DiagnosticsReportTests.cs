using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class DiagnosticsReportTests
{
    static readonly DiagnosticsReport Report = new(
        AppVersion: "0.1.2", Windows: "Windows 11 (10.0.26100)", Architecture: "ARM64", RekordboxVersion: null,
        Activation: OverlayActivation.RekordboxNotFrontmost,
        Settings: new OverlaySettings(isBPMLimitEnabled: true, bpmLimit: BPMLimit.NoDecimals, waveformMode: WaveformMode.HideBoth,
            isPhaseMeterEnabled: false, phaseMeterType: PhaseMeterType.Box),
        Extra: "");

    [Fact]
    public void TheReportListsVersionsStatusAndEverySettingInEnglish()
    {
        var text = L10n.WithOverride(LocalizationCatalog.Load("es"), () => Report.Text);
        Assert.Equal(string.Join("\n",
            "Earmatch diagnostics",
            "Earmatch: 0.1.2",
            "Windows: Windows 11 (10.0.26100)",
            "PC: ARM64",
            "rekordbox: not running",
            "Status: Paused until rekordbox is in front",
            "Enabled: on",
            "Limit BPM display: on, No decimals",
            "Waveform: Hide Both",
            "Phase meter: off, Box (CDJ-2000NXS / XDJ-1000)",
            "Counter: Bar position (rekordbox)",
            "Full blind mode: off",
            "Booth: custom",
            "Mix trainer: on") + "\n", text);
    }

    [Fact]
    public void ExtraLinesFollowAfterABlankLine()
    {
        var text = (Report with { RekordboxVersion = "7.2.19", Extra = "  capture: running \n" }).Text;
        Assert.Contains("rekordbox: 7.2.19\n", text);
        Assert.EndsWith("Mix trainer: on\n\ncapture: running\n", text);
    }

    [Fact]
    public void TheUpdateStateFollowsTheRekordboxLine()
    {
        var text = (Report with { Updates = "Updates: off (development build)" }).Text;
        Assert.Contains("rekordbox: not running\nUpdates: off (development build)\nStatus: ", text);
        Assert.DoesNotContain("Updates:", Report.Text);
    }

    [Fact]
    public void AMatchingBoothIsNamed()
    {
        var settings = new OverlaySettings();
        BoothPreset.Cdj3000.Apply(ref settings);
        Assert.Contains("Booth: CDJ-3000\n", (Report with { Settings = settings }).Text);
    }
}
