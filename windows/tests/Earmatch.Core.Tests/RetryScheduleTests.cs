using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class RetryScheduleTests
{
    [Fact]
    public void AFailureIsRetriedAfterASecondAndAMissingPermissionAfterTwo()
    {
        var schedule = new RetrySchedule();
        var failure = schedule.Retry(RetrySchedule.Reason.Failure, at: 10);
        var permission = new RetrySchedule();
        var denied = permission.Retry(RetrySchedule.Reason.PermissionDenied, at: 10);

        Assert.Equal(new RetrySchedule.Timer(Delay: 1, FireTime: 11), failure);
        Assert.Equal(new RetrySchedule.Timer(Delay: 2, FireTime: 12), denied);
    }

    [Fact]
    public void NoNewAttemptStartsBeforeTheBackoffEnds()
    {
        var schedule = new RetrySchedule();
        Assert.True(schedule.AllowsAttempt(at: 0));
        _ = schedule.Retry(RetrySchedule.Reason.Failure, at: 10);

        Assert.False(schedule.AllowsAttempt(at: 10.5));
        Assert.True(schedule.AllowsAttempt(at: 11));
    }

    [Fact]
    public void AnOffScreenWindowIsLookedForLessAndLessOftenUpToTwoSeconds()
    {
        var schedule = new RetrySchedule();
        var delays = new List<double>();
        var now = 0.0;
        for (var i = 0; i < 7; i++)
        {
            var timer = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: now);
            Assert.NotNull(timer);
            delays.Add(timer.Value.Delay);
            now = timer.Value.FireTime;
            Assert.True(schedule.Fire(timer.Value));
            schedule.AttemptStarted();
        }
        Assert.Equal([0.1, 0.2, 0.4, 0.8, 1.6, 2, 2], delays);
    }

    [Fact]
    public void FindingTheWindowStartsTheOffScreenBackoffOver()
    {
        var schedule = new RetrySchedule();
        _ = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 0);
        schedule.AttemptStarted();
        _ = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 1);
        schedule.AttemptStarted();
        schedule.WindowFound();
        var next = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 5);

        Assert.Equal(0.1, next?.Delay);
    }

    [Fact]
    public void StoppingStartsTheOffScreenBackoffOver()
    {
        var schedule = new RetrySchedule();
        _ = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 0);
        schedule.AttemptStarted();
        _ = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 1);
        schedule.AttemptStarted();
        schedule.Reset();
        var next = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 5);

        Assert.Equal(0.1, next?.Delay);
    }

    [Fact]
    public void AnEarlierRetryReplacesALaterOneAndALaterOneIsNotScheduled()
    {
        var schedule = new RetrySchedule();
        var later = schedule.Retry(RetrySchedule.Reason.PermissionDenied, at: 0);
        var earlier = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 0.5);
        var notArmed = schedule.Retry(RetrySchedule.Reason.Failure, at: 0.55);
        Assert.NotNull(later);
        Assert.NotNull(earlier);
        var laterFired = schedule.Fire(later.Value);
        var earlierFired = schedule.Fire(earlier.Value);

        Assert.Null(notArmed);
        Assert.False(laterFired);
        Assert.True(earlierFired);
    }

    [Fact]
    public void ALaterRetryKeepsTheGateOpenWhenTheArmedEarlierTimerFires()
    {
        var schedule = new RetrySchedule();
        var earlier = schedule.Retry(RetrySchedule.Reason.WindowOffScreen, at: 0);
        Assert.NotNull(earlier);
        var notArmed = schedule.Retry(RetrySchedule.Reason.PermissionDenied, at: 0.05);
        var allowedAtFire = schedule.AllowsAttempt(at: earlier.Value.FireTime);
        var fired = schedule.Fire(earlier.Value);

        Assert.Null(notArmed);
        Assert.False(schedule.AllowsAttempt(at: 0.06));
        Assert.True(allowedAtFire);
        Assert.True(fired);
    }

    [Fact]
    public void ATimerFiresOnceAndNotAfterANewAttemptStarted()
    {
        var schedule = new RetrySchedule();
        var timer = schedule.Retry(RetrySchedule.Reason.Failure, at: 0);
        Assert.NotNull(timer);
        var first = schedule.Fire(timer.Value);
        var second = schedule.Fire(timer.Value);
        var next = schedule.Retry(RetrySchedule.Reason.Failure, at: 2);
        Assert.NotNull(next);
        schedule.AttemptStarted();
        var afterStart = schedule.Fire(next.Value);

        Assert.True(first);
        Assert.False(second);
        Assert.False(afterStart);
    }
}
