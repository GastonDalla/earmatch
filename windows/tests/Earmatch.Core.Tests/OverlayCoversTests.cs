using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

/// Where Earmatch for Windows covers rekordbox without reading its pixels (OverlayController.swift's masks and covers):
/// the jog BPM under the BPM limit, the pitch and header BPM in blind mode, and both waveforms in Hide Both.
public class OverlayCoversTests
{
    const string TwoDeckHorizontal = "uia-7.2.19-perf-2deck-horizontal.json";
    const string TwoDeckVertical = "uia-7.2.19-perf-2deck-vertical.json";

    static OverlaySettings Settings(bool limit = true, BPMLimit bpmLimit = BPMLimit.OneDecimal, bool blind = false,
        WaveformMode waveform = WaveformMode.Split) =>
        new(isBPMLimitEnabled: limit, bpmLimit: bpmLimit, isBlindModeEnabled: blind, waveformMode: waveform);

    static List<OverlayCover> Covers(string fixture, OverlaySettings settings) =>
        OverlayCovers.Plan(Layout(fixture), settings, Scale).ToList();

    [Theory]
    [InlineData(BPMLimit.OneDecimal)]
    [InlineData(BPMLimit.NoDecimals)]
    [InlineData(BPMLimit.CoverFully)]
    public void TheBPMLimitCoversEachJogBPMFullyOneLayoutUnitWider(BPMLimit limit)
    {
        var covers = Covers(TwoDeckHorizontal, Settings(bpmLimit: limit));

        Assert.Equal([
            new OverlayCover(OverlayCoverKind.JogBPM, 1, Rect(756, 712, 112, 50)),
            new OverlayCover(OverlayCoverKind.JogBPM, 2, Rect(1200, 712, 112, 50)),
        ], covers);
    }

    [Fact]
    public void BlindModeAlsoCoversThePitchAndTheHeaderBPM()
    {
        var layout = Layout(TwoDeckHorizontal);

        var covers = Covers(TwoDeckHorizontal, Settings(blind: true));

        var pitch1 = layout.Decks[0].PitchFrame!.Value;
        var pitch2 = layout.Decks[1].PitchFrame!.Value;
        Assert.Equal([
            new OverlayCover(OverlayCoverKind.JogBPM, 1, Rect(756, 712, 112, 50)),
            new OverlayCover(OverlayCoverKind.Pitch, 1, new ScreenRect(pitch1.X - 2, pitch1.Y - 2, pitch1.Width + 4, pitch1.Height + 4)),
            new OverlayCover(OverlayCoverKind.HeaderBPM, 1, Rect(356, 488, 92, 32)),
            new OverlayCover(OverlayCoverKind.JogBPM, 2, Rect(1200, 712, 112, 50)),
            new OverlayCover(OverlayCoverKind.Pitch, 2, new ScreenRect(pitch2.X - 2, pitch2.Y - 2, pitch2.Width + 4, pitch2.Height + 4)),
            new OverlayCover(OverlayCoverKind.HeaderBPM, 2, Rect(1382, 488, 92, 32)),
        ], covers);
    }

    [Fact]
    public void BlindModeWithoutTheLimitLeavesTheJogBPMInFull()
    {
        var covers = Covers(TwoDeckHorizontal, Settings(limit: false, blind: true));

        Assert.DoesNotContain(covers, cover => cover.Kind == OverlayCoverKind.JogBPM);
        Assert.Equal(4, covers.Count);
    }

    [Fact]
    public void WithTheLimitAndBlindModeOffNothingIsCovered() => Assert.Empty(Covers(TwoDeckHorizontal, Settings(limit: false)));

    [Fact]
    public void AnEmptyDeckHasNoHeaderBPMToCover()
    {
        var layout = Layout(TwoDeckHorizontal);
        layout.Decks[1] = layout.Decks[1] with { HeaderBPMFrame = null };

        var covers = OverlayCovers.Plan(layout, Settings(limit: false, blind: true), Scale);

        Assert.Equal([1], covers.Where(cover => cover.Kind == OverlayCoverKind.HeaderBPM).Select(cover => cover.Deck));
    }

    [Fact]
    public void HideBothBlacksOutTheWholeStackedWaveformOfAHorizontalLayout()
    {
        var covers = Covers(TwoDeckHorizontal, Settings(limit: false, waveform: WaveformMode.HideBoth));

        Assert.Equal([new OverlayCover(OverlayCoverKind.Waveform, 0, Rect(0, 146, 2048, 276))], covers);
    }

    [Fact]
    public void HideBothBlacksOutEachDecksColumnOfAVerticalLayout()
    {
        var layout = Layout(TwoDeckVertical);

        var covers = Covers(TwoDeckVertical, Settings(limit: false, waveform: WaveformMode.HideBoth));

        Assert.Equal(layout.Decks.Select(deck => new OverlayCover(OverlayCoverKind.Waveform, deck.Number, deck.WaveformStrip!.Value)), covers);
    }

    [Theory]
    [InlineData(WaveformMode.Normal)]
    [InlineData(WaveformMode.Split)]
    public void NormalAndSplitLeaveTheWaveformsToThePixelOverlays(WaveformMode mode) =>
        Assert.Empty(Covers(TwoDeckHorizontal, Settings(limit: false, waveform: mode)));

    [Fact]
    public void ThereIsNothingToCoverOutsidePerformanceLayouts()
    {
        var browse = Layout("uia-7.2.19-perf-browse.json");

        Assert.DoesNotContain(OverlayCovers.Plan(browse, Settings(blind: true, waveform: WaveformMode.HideBoth), Scale),
            cover => cover.Kind == OverlayCoverKind.Waveform);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    public void TheOverlaysShowWhileEarmatchIsOnRekordboxIsActiveAndNobodyPeeks(bool enabled, bool active, bool peeking, bool shows) =>
        Assert.Equal(shows, OverlayCovers.Shows(new OverlaySettings(isEnabled: enabled),
            active ? OverlayActivation.Active : OverlayActivation.RekordboxNotFrontmost, peeking));
}
