using System.Collections.Concurrent;

namespace Earmatch.App;

/// A background thread with a Win32 message loop for Earmatch's system-wide hooks (the low-level keyboard hook and the
/// WinEvent hooks). Windows calls a hook on the thread that installed it, between that thread's messages, so hooks on
/// the window's thread would wait for every layout and animation frame of the window, and a slow keyboard hook delays
/// every key the user types in rekordbox. Post runs an action on the thread, where the hooks are installed and removed.
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

    /// Runs the queued actions (the hooks' removal), then ends the loop.
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
