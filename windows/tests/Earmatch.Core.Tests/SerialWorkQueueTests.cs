using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class SerialWorkQueueTests
{
    [Fact]
    public void WorkRunsInTheOrderItWasQueued()
    {
        var queue = new SerialWorkQueue();
        var order = new List<int>();
        using var first = new ManualResetEventSlim();
        queue.Enqueue(() =>
        {
            first.Wait(TimeSpan.FromSeconds(5));
            lock (order) order.Add(1);
        });
        queue.Enqueue(() =>
        {
            lock (order) order.Add(2);
        });
        first.Set();

        Assert.True(queue.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal([1, 2], order);
    }

    [Fact]
    public void FlushingWaitsForTheWorkStillQueued()
    {
        var queue = new SerialWorkQueue();
        var saved = false;
        queue.Enqueue(() =>
        {
            Thread.Sleep(200);
            Volatile.Write(ref saved, true);
        });

        Assert.True(queue.Flush(TimeSpan.FromSeconds(5)));
        Assert.True(Volatile.Read(ref saved));
    }

    [Fact]
    public void FlushingGivesUpAfterTheTimeout()
    {
        var queue = new SerialWorkQueue();
        using var release = new ManualResetEventSlim();
        queue.Enqueue(() => release.Wait(TimeSpan.FromSeconds(5)));

        Assert.False(queue.Flush(TimeSpan.FromMilliseconds(50)));
        release.Set();
        Assert.True(queue.Flush(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void AFailingItemIsReportedAndTheNextOnesStillRun()
    {
        var errors = new List<Exception>();
        var queue = new SerialWorkQueue(error =>
        {
            lock (errors) errors.Add(error);
        });
        var ran = false;
        queue.Enqueue(() => throw new IOException("disk full"));
        queue.Enqueue(() => Volatile.Write(ref ran, true));

        Assert.True(queue.Flush(TimeSpan.FromSeconds(5)));
        Assert.True(Volatile.Read(ref ran));
        Assert.Equal("disk full", Assert.Single(errors).Message);
    }

    [Fact]
    public void AnEmptyQueueFlushesAtOnce()
    {
        Assert.True(new SerialWorkQueue().Flush(TimeSpan.Zero));
    }
}
