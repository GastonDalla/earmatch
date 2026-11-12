using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class CaptureDiagnosticsTests
{
    static readonly CaptureDiagnostics Capture = new(
        earmatchVersion: "0.1.0", windowsVersion: "10.0.26100", rekordboxVersion: "7.2.19",
        activation: "Active on rekordbox", layout: "4Deck Vertical", capture: "running",
        mappingProblem: null, waveformFound: true, framesPerSecond: 110.44, captureJitterMilliseconds: 0.75,
        analysisMilliseconds: 0.61, maxAnalysisMilliseconds: 2.14, earliestLatencyMilliseconds: 5.8, latencyMilliseconds: 14.06,
        maxLatencyMilliseconds: 18.2, refreshesPerSecond: 118, meterErrorMilliseconds: 2.1,
        decks:
        [
            new CaptureDiagnostics.Deck(number: 1, isLoaded: true, reading: "live", beatSpacing: 12.04, phaseInBar: 2.5, beatsPerSecond: 2.25,
                isMaster: false, headerBPM: 135, elapsedSeconds: 327.1),
            new CaptureDiagnostics.Deck(number: 3, isLoaded: false, reading: "-", beatSpacing: null, phaseInBar: null, beatsPerSecond: null,
                isMaster: true, headerBPM: null, elapsedSeconds: null),
        ]);

    [Fact]
    public void TheCaptureReportStaysEnglishWithInvariantNumbers()
    {
        var text = Localized.InSpanishCulture(() => Capture.Text);

        Assert.Equal(
            """
            Earmatch 0.1.0, Windows 10.0.26100, rekordbox 7.2.19
            Activation: Active on rekordbox
            Layout: 4Deck Vertical
            Capture: running
            Stacked waveform: found
            Frames: 110.4 fps (interval sd 0.75 ms), analysis 0.61 ms avg / 2.14 ms max
            Latency: capture to screen 5.8 to 14.1 ms avg, 18.2 ms max; meter drawn 118 times/s, predicted for the screen (2.1 ms from rekordbox)
            Deck 1: loaded, grid live, spacing 12.04 px/beat, phase 2.50, playing 135.0 BPM, master no, BPM 135.00, elapsed 327.1 s
            Deck 3: empty, grid -, spacing -, phase -, playing -, master yes, BPM -, elapsed -
            """.ReplaceLineEndings("\n"),
            text);
    }

    [Fact]
    public void AMappingProblemFollowsTheCaptureState()
    {
        var capture = Capture with
        {
            MappingProblem = "frame shows 800x600 px but the window is 2048x1356 px",
            Layout = null,
            MeterErrorMilliseconds = null,
            RekordboxVersion = null,
        };

        var lines = capture.Text.Split('\n');

        Assert.Contains("Capture: running (frame shows 800x600 px but the window is 2048x1356 px)", lines);
        Assert.Contains("Layout: -", lines);
        Assert.Contains("Earmatch 0.1.0, Windows 10.0.26100, rekordbox not running", lines);
        Assert.Contains("Latency: capture to screen 5.8 to 14.1 ms avg, 18.2 ms max; meter drawn 118 times/s, predicted for the screen", lines);
    }
}
