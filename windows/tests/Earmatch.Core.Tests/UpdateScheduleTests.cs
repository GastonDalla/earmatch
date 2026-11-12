using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class UpdateScheduleTests
{
    static readonly DateTimeOffset LastCheck = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ACopyThatHasNotCheckedYetChecksNow()
    {
        Assert.True(UpdateSchedule.IsDue(lastCheck: null, now: LastCheck));
    }

    [Fact]
    public void ItChecksAgainOnceADayHasPassed()
    {
        Assert.Equal(TimeSpan.FromDays(1), UpdateSchedule.Interval);
        Assert.True(UpdateSchedule.IsDue(LastCheck, LastCheck.AddDays(1)));
        Assert.True(UpdateSchedule.IsDue(LastCheck, LastCheck.AddDays(3)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(24 * 60 - 1)]
    public void ItWaitsWithinADayOfTheLastCheck(int minutes)
    {
        Assert.False(UpdateSchedule.IsDue(LastCheck, LastCheck.AddMinutes(minutes)));
    }

    [Fact]
    public void AClockSetBackBeforeTheLastCheckChecksNow()
    {
        Assert.True(UpdateSchedule.IsDue(LastCheck, LastCheck.AddMinutes(-5)));
    }
}
