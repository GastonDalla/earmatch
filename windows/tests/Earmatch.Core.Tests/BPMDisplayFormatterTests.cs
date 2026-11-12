using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class BPMDisplayFormatterTests
{
    [Theory]
    [InlineData("135.00", 135.0)]
    [InlineData(" 124.00", 124.0)]
    [InlineData("127.96", 127.96)]
    [InlineData("99.5", 99.5)]
    [InlineData(" 140.00", 140.0)]
    public void ParsesRekordboxBPMStrings(string raw, double expected)
    {
        Assert.Equal(expected, BPMDisplayFormatter.Parse(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("BPM")]
    [InlineData("00.00.0")]
    public void RejectsTextThatIsNotABPM(string raw)
    {
        Assert.Null(BPMDisplayFormatter.Parse(raw));
    }
}
