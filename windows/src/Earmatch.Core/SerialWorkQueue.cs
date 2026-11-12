namespace Earmatch.Core;

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

    public bool Flush(TimeSpan timeout)
    {
        Task tail;
        lock (_gate) tail = _tail;
        return tail.Wait(timeout);
    }
}
