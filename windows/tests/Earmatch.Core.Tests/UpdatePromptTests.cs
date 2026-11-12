using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class UpdatePromptTests
{
    [Fact]
    public void AnUpdateFoundRightAfterOpeningAsksInFront()
    {
        Assert.True(UpdatePrompt.AsksNow(secondsSinceLaunch: 3, rekordboxIsFrontmost: false));
    }

    [Fact]
    public void AnUpdateFoundWhileRekordboxIsInFrontWaitsInTheMenus()
    {
        Assert.False(UpdatePrompt.AsksNow(secondsSinceLaunch: 3, rekordboxIsFrontmost: true));
    }

    [Theory]
    [InlineData(UpdatePrompt.LaunchWindow)]
    [InlineData(600)]
    [InlineData(3_600)]
    public void AnUpdateFoundLongAfterOpeningWaitsInTheMenus(double secondsSinceLaunch)
    {
        Assert.False(UpdatePrompt.AsksNow(secondsSinceLaunch, rekordboxIsFrontmost: false));
    }
}
