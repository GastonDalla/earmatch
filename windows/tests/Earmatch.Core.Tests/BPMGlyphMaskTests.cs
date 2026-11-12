using Earmatch.Core;
using static Earmatch.Core.Tests.Fixture;

namespace Earmatch.Core.Tests;

public class BPMGlyphMaskTests
{
    sealed record JogFixture(string File, ScreenRect JogFrame)
    {
        public ScreenPoint Origin => new(JogFrame.MinX - 10, JogFrame.MinY - 7);

        public PixelImage Image() => PixelBuffer.Png(File).Image(Origin);
    }

    static readonly JogFixture Deck1 = new("jog-bpm-deck1-135.00.png", Rect(558, 427, 54, 23));
    static readonly JogFixture Deck2 = new("jog-bpm-deck2-124.00.png", Rect(890, 427, 54, 23));

    static JogFixture Named(string deck) => deck == "deck1" ? Deck1 : Deck2;

    [Fact]
    public void FindsTheIntegerDigitsThePointAndBothDecimals()
    {
        var glyphs = BPMGlyphMask.Glyphs(Deck1.JogFrame, Deck1.Image());

        Assert.NotNull(glyphs);
        Assert.Equal(3, glyphs.IntegerDigits.Count);
        Assert.Equal(2, glyphs.Decimals.Count);
        Assert.True(Math.Abs(glyphs.Point.MinX - 590) <= 0.5, $"{glyphs.Point.MinX}");
        Assert.True(Math.Abs(glyphs.Decimals[1].MaxX - 605.5) <= 0.5, $"{glyphs.Decimals[1].MaxX}");
    }

    [Theory]
    [InlineData("deck1")]
    [InlineData("deck2")]
    public void OneDecimalCoversOnlyTheLastDigit(string deck)
    {
        var fixture = Named(deck);
        var image = fixture.Image();

        var decimals = BPMGlyphMask.Glyphs(fixture.JogFrame, image)!.Decimals;
        var cover = BPMGlyphMask.Cover(BPMLimit.OneDecimal, fixture.JogFrame, image);

        Assert.True(cover.MinX > decimals[0].MaxX);
        Assert.True(cover.MinX <= decimals[1].MinX);
        Assert.True(cover.MaxX >= decimals[1].MaxX);
        Assert.True(cover.MinY <= fixture.JogFrame.MinY && cover.MaxY >= fixture.JogFrame.MaxY);
    }

    [Theory]
    [InlineData("deck1")]
    [InlineData("deck2")]
    public void NoDecimalsCoversThePointAndBothDecimals(string deck)
    {
        var fixture = Named(deck);
        var image = fixture.Image();

        var found = BPMGlyphMask.Glyphs(fixture.JogFrame, image)!;
        var cover = BPMGlyphMask.Cover(BPMLimit.NoDecimals, fixture.JogFrame, image);

        Assert.True(cover.MinX > found.IntegerDigits[2].MaxX);
        Assert.True(cover.MinX <= found.Point.MinX);
        Assert.True(cover.MaxX >= found.Decimals[1].MaxX);
    }

    [Fact]
    public void CoverFullyCoversTheWholeDisplay()
    {
        Assert.Equal(Deck1.JogFrame, BPMGlyphMask.Cover(BPMLimit.CoverFully, Deck1.JogFrame, Deck1.Image()));
    }

    [Fact]
    public void UnreadableDigitsAreCoveredFully()
    {
        var jogFrame = Rect(10, 10, 54, 23);
        var blank = new PixelBuffer(160, 90);

        Assert.Equal(jogFrame, BPMGlyphMask.Cover(BPMLimit.OneDecimal, jogFrame, blank.Image()));
    }

    [Theory]
    [InlineData("deck1")]
    [InlineData("deck2")]
    public void PhysicalPixelCoordinatesCoverTheSameDigits(string deck)
    {
        var fixture = Named(deck);
        var buffer = PixelBuffer.Png(fixture.File);

        foreach (var limit in Enum.GetValues<BPMLimit>())
        {
            var points = BPMGlyphMask.Cover(limit, fixture.JogFrame, buffer.Image(fixture.Origin));
            var physical = BPMGlyphMask.Cover(limit, Physical.Rect(fixture.JogFrame), buffer.WindowsImage(fixture.Origin));
            Assert.Equal(Physical.Rect(points), physical);
        }
    }
}
