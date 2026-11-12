using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class StatusTextLayoutTests
{
    static StatusContext Context(string? layout, OverlaySettings? settings = null, OverlayActivation? activation = null,
        string? version = "7.2.19.0342") =>
        new(activation ?? OverlayActivation.Active, settings ?? new OverlaySettings(), rekordboxVersion: version, captureProblem: null, layoutName: layout);

    [Fact]
    public void TheHeaderNamesTheLayout()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "rekordbox 7.2.19.0342 connected · 4Deck Vertical"), StatusText.Connection(Context("4Deck Vertical")));
        Assert.Equal(new StatusLine(StatusTone.Live, "rekordbox connected · 2Deck Vertical"), StatusText.Connection(Context("2Deck Vertical", version: null)));
        Assert.Equal(new StatusLine(StatusTone.Live, "rekordbox 7.2.19.0342 connected · Browse"),
            StatusText.Connection(Context("Browse", activation: OverlayActivation.WrongLayout("Browse"))));
    }

    [Fact]
    public void HorizontalLayoutsSplitTheirDecks()
    {
        var split = new OverlaySettings(waveformMode: WaveformMode.Split);
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 2Deck Horizontal: decks split, one playhead each"),
            StatusText.Waveform(Context("2Deck Horizontal", split)));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 4Deck Horizontal: decks split, one playhead each"),
            StatusText.Waveform(Context("4Deck Horizontal", split)));
    }

    [Fact]
    public void VerticalLayoutsKeepEachDeckInItsOwnColumn()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 2Deck Vertical: each deck keeps its own column"),
            StatusText.Waveform(Context("2Deck Vertical", new OverlaySettings(waveformMode: WaveformMode.Split))));
    }

    [Fact]
    public void HideBothHidesEveryWaveform()
    {
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 4Deck Vertical: waveforms hidden"),
            StatusText.Waveform(Context("4Deck Vertical", new OverlaySettings(waveformMode: WaveformMode.HideBoth))));
    }

    [Fact]
    public void TheMeterLineNamesTheLayout()
    {
        var line = new OverlaySettings(waveformMode: WaveformMode.Split, phaseMeterType: PhaseMeterType.Line);
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 4Deck Vertical: Line (CDJ-2000NXS2 Type 2)"), StatusText.PhaseMeter(Context("4Deck Vertical", line)));
        var wave = new OverlaySettings(waveformMode: WaveformMode.Split, phaseMeterType: PhaseMeterType.Cdj3000Waveform);
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 4Deck Horizontal: CDJ-3000 Waveform"), StatusText.PhaseMeter(Context("4Deck Horizontal", wave)));
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 2Deck Vertical: CDJ-3000 Waveform · master waveform in horizontal Split only"),
            StatusText.PhaseMeter(Context("2Deck Vertical", wave)));
        var normal = new OverlaySettings(waveformMode: WaveformMode.Normal, phaseMeterType: PhaseMeterType.Cdj3000Waveform);
        Assert.Equal(new StatusLine(StatusTone.Live, "Live in 2Deck Horizontal: CDJ-3000 Waveform · master waveform in horizontal Split only"),
            StatusText.PhaseMeter(Context("2Deck Horizontal", normal)));
    }

    [Fact]
    public void EveryLayoutLineGoesThroughTheLookup()
    {
        var lines = Localized.Marked(() =>
        {
            var result = new List<StatusLine>();
            foreach (var name in DeckArrangement.AllCases.Select(arrangement => arrangement.RawValue))
            {
                foreach (var mode in WaveformMode.AllCases)
                {
                    foreach (var type in PhaseMeterType.AllCases)
                    {
                        var context = Context(name, new OverlaySettings(waveformMode: mode, phaseMeterType: type));
                        result.AddRange([StatusText.Connection(context), StatusText.Waveform(context), StatusText.PhaseMeter(context)]);
                    }
                }
            }
            return result;
        });
        var unmarked = lines.Where(line => !line.Text.StartsWith('⟦')).Select(line => line.Text).ToList();
        Assert.True(unmarked.Count == 0, string.Join(" | ", unmarked));
    }
}
