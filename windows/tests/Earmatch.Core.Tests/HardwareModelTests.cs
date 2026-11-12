using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class HardwareModelTests
{
    [Theory]
    [InlineData("Dell Inc.", "XPS 15 9530", "Dell Inc. XPS 15 9530")]
    [InlineData("LENOVO", "LENOVO 21HM", "LENOVO 21HM")]
    [InlineData("Microsoft Corporation", "Surface Laptop 7", "Microsoft Corporation Surface Laptop 7")]
    [InlineData("  ASUS  ", " ROG Strix G16 ", "ASUS ROG Strix G16")]
    public void Describe_JoinsTheMakerAndTheModelOnce(string manufacturer, string product, string expected)
    {
        Assert.Equal(expected, HardwareModel.Describe(manufacturer, product));
    }

    [Theory]
    [InlineData("System manufacturer", "System Product Name", null)]
    [InlineData("To Be Filled By O.E.M.", "B550 Phantom", "B550 Phantom")]
    [InlineData("Gigabyte", "Default string", "Gigabyte")]
    [InlineData(null, null, null)]
    [InlineData("", "  ", null)]
    public void Describe_LeavesOutTheBoardMakersPlaceholders(string? manufacturer, string? product, string? expected)
    {
        Assert.Equal(expected, HardwareModel.Describe(manufacturer, product));
    }
}
