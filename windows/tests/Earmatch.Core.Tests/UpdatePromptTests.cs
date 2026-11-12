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

    [Fact]
    public void TheCheckWhenEarmatchOpensFollowsTheSameRule()
    {
        Assert.True(UpdatePrompt.AsksNow(UpdateTrigger.Launch, secondsSinceLaunch: 3, rekordboxIsFrontmost: false));
        Assert.False(UpdatePrompt.AsksNow(UpdateTrigger.Launch, secondsSinceLaunch: 3, rekordboxIsFrontmost: true));
        Assert.False(UpdatePrompt.AsksNow(UpdateTrigger.Launch, secondsSinceLaunch: UpdatePrompt.LaunchWindow, rekordboxIsFrontmost: false));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(90_000, false)]
    public void TheDailyCheckNeverAsksInFront(double secondsSinceLaunch, bool rekordboxIsFrontmost)
    {
        Assert.False(UpdatePrompt.AsksNow(UpdateTrigger.Daily, secondsSinceLaunch, rekordboxIsFrontmost));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(90_000, true)]
    public void ACheckTheUserAskedForAlwaysAnswers(double secondsSinceLaunch, bool rekordboxIsFrontmost)
    {
        Assert.True(UpdatePrompt.AsksNow(UpdateTrigger.User, secondsSinceLaunch, rekordboxIsFrontmost));
    }

    [Theory]
    [InlineData(UpdateTrigger.Launch)]
    [InlineData(UpdateTrigger.Daily)]
    public void ASkippedVersionIsLeftAloneByTheAutomaticChecks(UpdateTrigger trigger)
    {
        Assert.True(UpdatePrompt.IsSkipped(trigger, "0.1.3", "0.1.3"));
        Assert.False(UpdatePrompt.IsSkipped(trigger, "0.1.4", "0.1.3"));
        Assert.False(UpdatePrompt.IsSkipped(trigger, "0.1.3", null));
    }

    [Fact]
    public void TheUsersOwnCheckShowsASkippedVersion()
    {
        Assert.False(UpdatePrompt.IsSkipped(UpdateTrigger.User, "0.1.3", "0.1.3"));
    }
}
