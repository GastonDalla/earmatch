using System.Runtime.InteropServices;
using Earmatch.Core;

namespace Earmatch.Core.Tests;

public class ClipboardRetryTests
{
    [Fact]
    public void AWriteThatWorksIsReportedAtOnce()
    {
        var writes = 0;
        var pauses = 0;

        Assert.True(ClipboardRetry.TryWrite(() => writes++, () => pauses++));
        Assert.Equal(1, writes);
        Assert.Equal(0, pauses);
    }

    [Fact]
    public void AClipboardHeldForAMomentIsTriedAgain()
    {
        var writes = 0;
        var pauses = 0;

        var copied = ClipboardRetry.TryWrite(() =>
        {
            if (++writes < 3) throw new COMException("OpenClipboard failed", unchecked((int)0x800401D0));
        }, () => pauses++);

        Assert.True(copied);
        Assert.Equal(3, writes);
        Assert.Equal(2, pauses);
    }

    [Fact]
    public void AClipboardThatStaysBusyIsReportedAsAFailure()
    {
        var writes = 0;
        var pauses = 0;

        var copied = ClipboardRetry.TryWrite(() =>
        {
            writes++;
            throw new ExternalException("OpenClipboard failed");
        }, () => pauses++);

        Assert.False(copied);
        Assert.Equal(ClipboardRetry.Attempts, writes);
        Assert.Equal(ClipboardRetry.Attempts - 1, pauses);
    }

    [Fact]
    public void OtherProblemsAreNotSwallowed()
    {
        Assert.Throws<InvalidOperationException>(() => ClipboardRetry.TryWrite(() => throw new InvalidOperationException(), () => { }));
    }
}
