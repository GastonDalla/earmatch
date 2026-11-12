using System.Collections.Concurrent;

namespace Earmatch.App;

internal sealed class HookThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _actions = new();
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;
    private bool _isDisposed;

    public HookThread(string name)
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.Start();
        _ready.Wait();
    }

    public void Post(Action action)
    {
        if (_isDisposed) return;
        _actions.Enqueue(action);
        NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmApp, IntPtr.Zero, IntPtr.Zero);
    }

    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.CreateMessageQueue();
        _ready.Set();
        while (NativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.Window == IntPtr.Zero && message.Id == NativeMethods.WmApp)
            {
                while (_actions.TryDequeue(out var action)) action();
                continue;
            }
            NativeMethods.TranslateMessage(message);
            NativeMethods.DispatchMessage(message);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmApp, IntPtr.Zero, IntPtr.Zero);
        NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
