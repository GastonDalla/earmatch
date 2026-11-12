using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class PhaseMeterModelTests
{
    static readonly PhaseMeterDeckInput[] Decks =
    [
        new(1, 0.2, new BarPosition(24, 1)),
        new(2, 3.4, new BarPosition(6, 4)),
    ];

    [Fact]
    public void ADeckThatIsNotMasterShowsTheMasterRowAboveItsOwn()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: 2, PhaseMeterType.Box);

        Assert.Equal(PhaseMeterType.Box, display.Style);
        Assert.Equal(2, display.MasterPlayer);
        Assert.Equal([PhaseMeterRole.Master, PhaseMeterRole.Deck], display.Rows.Select(row => row.Role));
        Assert.Equal([2, 1], display.Rows.Select(row => row.DeckNumber));
        Assert.Equal(["06.4", "24.1"], display.Rows.Select(row => row.Counter));
        Assert.Equal([3, 0], display.Rows.Select(row => row.BeatInBar));
        Assert.Equal("Bars", PhaseMeterDisplay.CounterUnit);
    }

    [Fact]
    public void BoxesLightTheMastersBeatAndFillThisDecksBeatRedWhenItDiffers()
    {
        var rows = PhaseMeterModel.Display(1, Decks, master: 2, PhaseMeterType.Box).Rows;

        Assert.Equal([PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.MasterBeat], rows[0].Boxes);
        Assert.Equal([PhaseMeterBox.OutOfPhase, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle], rows[1].Boxes);
        Assert.False(rows[1].IsOnMastersBeat);
        Assert.Null(rows[0].IsOnMastersBeat);
    }

    [Fact]
    public void ThisDecksBoxIsBlueWhenItIsOnTheMastersBeat()
    {
        PhaseMeterDeckInput[] inPhase = [Decks[0] with { PhaseInBar = 3.1 }, Decks[1]];
        var rows = PhaseMeterModel.Display(1, inPhase, master: 2, PhaseMeterType.Box).Rows;

        Assert.Equal([PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.InPhase], rows[1].Boxes);
        Assert.True(rows[1].IsOnMastersBeat);
    }

    [Fact]
    public void TheSyncMasterShowsOnlyItsOwnRowWithoutALabel()
    {
        var display = PhaseMeterModel.Display(2, Decks, master: 2, PhaseMeterType.Box);

        Assert.Null(display.MasterPlayer);
        Assert.Null(display.Label);
        var row = Assert.Single(display.Rows);
        Assert.Equal(PhaseMeterRole.Deck, row.Role);
        Assert.Equal([PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.InPhase], row.Boxes);
        Assert.Equal("06.4", row.Counter);
    }

    [Fact]
    public void WithoutASyncMasterEveryDeckShowsOnlyItsOwnRow()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: null, PhaseMeterType.Line);

        Assert.Null(display.MasterPlayer);
        Assert.Equal([1], display.Rows.Select(row => row.DeckNumber));
        Assert.Null(display.Rows[0].IsOnMastersBeat);
    }

    [Fact]
    public void ThisDecksBoxIsNeutralWhileTheMastersGridIsUnread()
    {
        PhaseMeterDeckInput[] decks = [new(1, 1.2, null), new(2, null, null)];
        var rows = PhaseMeterModel.Display(1, decks, master: 2, PhaseMeterType.Box).Rows;

        Assert.Equal([PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle], rows[1].Boxes);
        Assert.Null(rows[1].IsOnMastersBeat);
        Assert.Equal(1, rows[1].BeatInBar);
        Assert.Equal([PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle], rows[0].Boxes);
    }

    [Fact]
    public void ADeckWithoutAReadingShowsAnIdleRow()
    {
        var row = PhaseMeterModel.Display(1, [new PhaseMeterDeckInput(1, null, null)], master: null, PhaseMeterType.Box).Rows[0];

        Assert.Null(row.BeatInBar);
        Assert.Empty(row.Ticks);
        Assert.Equal([PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle, PhaseMeterBox.Idle], row.Boxes);
        Assert.Equal("--.-", row.Counter);
        Assert.False(row.IsVacant);
    }

    [Fact]
    public void TheBoxMeterIsLabelledMasterPlayerWithTheNumberInABoxAndCountsOnTheRight()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: 2, PhaseMeterType.Box);
        var label = Assert.IsType<PhaseMeterLabel>(display.Label);

        Assert.Equal(["MASTER", "PLAYER"], label.Lines);
        Assert.Equal(2, label.Player);
        Assert.Equal(PhaseMeterCounterSide.Right, display.CounterSide);
        Assert.All(display.Rows, row => Assert.Empty(row.Ticks));
    }

    [Fact]
    public void LineTicksSitAtTheGridsRealOffsetsWithinOneBarOfNow()
    {
        var display = PhaseMeterModel.Display(1, [new PhaseMeterDeckInput(1, 1.25, null)], master: null, PhaseMeterType.Line);
        var row = display.Rows[0];

        Assert.Equal(4, display.Span);
        Assert.Equal([-3.25, -2.25, -1.25, -0.25, 0.75, 1.75, 2.75, 3.75], row.Ticks.Select(tick => tick.Offset));
        Assert.Equal([-0.8125, -0.5625, -0.3125, -0.0625, 0.1875, 0.4375, 0.6875, 0.9375], row.Ticks.Select(tick => tick.Position));
        Assert.Equal([-1.25, 2.75], row.Ticks.Where(tick => tick.IsDownbeat).Select(tick => tick.Offset));
        Assert.Equal(0.25, row.Fraction);
        Assert.Empty(row.Boxes);
    }

    [Fact]
    public void LineTicksHangFromTheMasterRulerAndStandOnThisDecksRuler()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: 2, PhaseMeterType.Line);

        Assert.Equal([PhaseMeterTickDirection.Down, PhaseMeterTickDirection.Up], display.Rows.Select(row => row.TickDirection));
        Assert.Equal(["MASTER", "PLAYER"], display.Label?.Lines);
        Assert.Equal(2, display.Label?.Player);
        Assert.Equal(PhaseMeterCounterSide.Right, display.CounterSide);
    }

    [Fact]
    public void TheCDJ3000MeterShowsAboutOnePointTwoBarsEachSide()
    {
        var display = PhaseMeterModel.Display(1, [new PhaseMeterDeckInput(1, 1.25, null)], master: null, PhaseMeterType.Cdj3000PhaseMeter);
        var row = display.Rows[^1];

        Assert.Equal(4.8, display.Span);
        Assert.Equal([-4.25, -3.25, -2.25, -1.25, -0.25, 0.75, 1.75, 2.75, 3.75, 4.75], row.Ticks.Select(tick => tick.Offset));
        Assert.All(row.Ticks, tick => Assert.True(Math.Abs(tick.Position) <= 1));
        Assert.Equal([-1.25, 2.75], row.Ticks.Where(tick => tick.IsDownbeat).Select(tick => tick.Offset));
    }

    [Fact]
    public void TheCDJ3000MeterCountsOnTheLeftWithoutAMasterPlayerLabel()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: 2, PhaseMeterType.Cdj3000PhaseMeter);

        Assert.Equal(PhaseMeterCounterSide.Left, display.CounterSide);
        Assert.Null(display.Label);
        Assert.Equal(2, display.MasterPlayer);
        Assert.Equal([PhaseMeterRole.Master, PhaseMeterRole.Deck], display.Rows.Select(row => row.Role));
        Assert.Equal(["06.4", "24.1"], display.Rows.Select(row => row.Counter));
        Assert.Equal([PhaseMeterTickDirection.Down, PhaseMeterTickDirection.Up], display.Rows.Select(row => row.TickDirection));
        Assert.False(display.Rows[0].IsVacant);
        Assert.NotEmpty(display.Rows[0].Ticks);
    }

    [Theory]
    [InlineData(PhaseMeterType.Cdj3000PhaseMeter)]
    [InlineData(PhaseMeterType.Cdj3000Waveform)]
    public void OnTheCDJ3000TheMasterRulerIsEmptyWhenThisDeckIsMaster(PhaseMeterType style)
    {
        var display = PhaseMeterModel.Display(2, Decks, master: 2, style);

        Assert.Equal([PhaseMeterRole.Master, PhaseMeterRole.Deck], display.Rows.Select(row => row.Role));
        Assert.True(display.Rows[0].IsVacant);
        Assert.Empty(display.Rows[0].Ticks);
        Assert.Null(display.Rows[0].BeatInBar);
        Assert.Equal("--.-", display.Rows[0].Counter);
        Assert.Equal("06.4", display.Rows[1].Counter);
        Assert.NotEmpty(display.Rows[1].Ticks);
        Assert.Null(display.MasterPlayer);
        Assert.False(display.ShowsMasterWaveform);
    }

    [Fact]
    public void OnTheCDJ3000TheMasterRulerIsEmptyWithoutASyncMaster()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: null, PhaseMeterType.Cdj3000PhaseMeter);

        Assert.Equal(2, display.Rows.Count);
        Assert.True(display.Rows[0].IsVacant);
        Assert.Equal("--.-", display.Rows[0].Counter);
        Assert.Equal("24.1", display.Rows[1].Counter);
    }

    [Fact]
    public void TheCDJ3000WaveformShowsTheMastersWaveformAndItsNumberInAnOrangeBox()
    {
        var display = PhaseMeterModel.Display(1, Decks, master: 2, PhaseMeterType.Cdj3000Waveform);
        var label = Assert.IsType<PhaseMeterLabel>(display.Label);

        Assert.True(display.ShowsMasterWaveform);
        Assert.Empty(label.Lines);
        Assert.Equal(2, label.Player);
        Assert.Equal(PhaseMeterCounterSide.Left, display.CounterSide);
        Assert.Equal(["06.4", "24.1"], display.Rows.Select(row => row.Counter));
    }

    [Theory]
    [InlineData(PhaseMeterType.Box)]
    [InlineData(PhaseMeterType.Line)]
    [InlineData(PhaseMeterType.Cdj3000PhaseMeter)]
    public void OtherStylesNeverShowTheMastersWaveform(PhaseMeterType style)
    {
        Assert.False(PhaseMeterModel.Display(1, Decks, master: 2, style).ShowsMasterWaveform);
    }

    [Fact]
    public void DisplaysOfTheSameReadingAreEqual()
    {
        Assert.Equal(PhaseMeterModel.Display(1, Decks, 2, PhaseMeterType.Line), PhaseMeterModel.Display(1, Decks, 2, PhaseMeterType.Line));
        Assert.NotEqual(PhaseMeterModel.Display(1, Decks, 2, PhaseMeterType.Line), PhaseMeterModel.Display(1, Decks, 2, PhaseMeterType.Box));
    }

    [Fact]
    public void TheBarPositionReadsLikeRekordboxsBarText()
    {
        Assert.Equal("06.3", new BarPosition(6, 3).Label);
        Assert.Equal("24.1", new BarPosition(24, 1).Label);
        Assert.Equal("101.2", new BarPosition(101, 2).Label);
    }
}
