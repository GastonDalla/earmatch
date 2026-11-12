namespace Earmatch.Core;

/// Runs work off the caller's thread, one item after another in the order queued (the practice history's saves, like
/// the PracticeStore actor on macOS). An item that throws is reported and the next ones still run. Flush waits, a
/// bounded time, for what is queued: quitting gives a finished mix the time to reach the file. Safe from any thread.
public sealed class SerialWorkQueue
{
    private readonly object _gate = new();
    private readonly Action<Exception>? _onError;
    private Task _tail = Task.CompletedTask;

    public SerialWorkQueue(Action<Exception>? onError = null)
    {
        _onError = onError;
    }

    public void Enqueue(Action work)
    {
        lock (_gate)
        {
            _tail = _tail.ContinueWith(_ =>
            {
                try
                {
                    work();
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    _onError?.Invoke(error);
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    /// Waits until everything queued so far has run, or `timeout` passed; true when it all ran.
    public bool Flush(TimeSpan timeout)
    {
        Task tail;
        lock (_gate) tail = _tail;
        return tail.Wait(timeout);
    }
}
