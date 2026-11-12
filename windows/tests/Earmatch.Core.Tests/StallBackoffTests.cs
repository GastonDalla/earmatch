using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class StallBackoffTests
{
    static readonly DateTime Start = new(2026, 11, 12, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PollsFreelyUntilAStall()
    {
        Assert.True(new StallBackoff(TimeSpan.FromSeconds(2)).AllowsPoll(at: Start));
    }

    [Fact]
    public void WaitsTheFullBackoffAfterAStall()
    {
        var backoff = new StallBackoff(TimeSpan.FromSeconds(2));
        backoff.RecordStall(at: Start);
        Assert.False(backoff.AllowsPoll(at: Start.AddSeconds(1.9)));
        Assert.True(backoff.AllowsPoll(at: Start.AddSeconds(2.0)));
    }

    [Fact]
    public void ANewStallRestartsTheWait()
    {
        var backoff = new StallBackoff(TimeSpan.FromSeconds(2));
        backoff.RecordStall(at: Start);
        backoff.RecordStall(at: Start.AddSeconds(2.5));
        Assert.False(backoff.AllowsPoll(at: Start.AddSeconds(4)));
        Assert.True(backoff.AllowsPoll(at: Start.AddSeconds(4.5)));
    }
}
