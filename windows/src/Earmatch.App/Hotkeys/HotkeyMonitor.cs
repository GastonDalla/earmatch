using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Hotkeys;

/// Earmatch's hotkeys (HotkeyMonitor.swift): a low-level keyboard hook on the hook thread feeds every key-down and
/// key-up to the HotkeyRecognizer and passes each one on to the apps, rekordbox included; only the E of
/// Ctrl+Alt+Win+E is kept from them, so Windows does not open File Explorer for Win+E. The peek and the toggle are
/// raised on the window's dispatcher. Windows needs no permission for the hook.
internal sealed class HotkeyMonitor : IDisposable
{
    private readonly HookThread _thread;
    private readonly Dispatcher _dispatcher;
    private readonly HotkeyRecognizer _recognizer = new();
    private readonly NativeMethods.LowLevelKeyboardProc _procedure;
    private IntPtr _hook;

    public HotkeyMonitor(HookThread thread, Dispatcher dispatcher)
    {
        _thread = thread;
        _dispatcher = dispatcher;
        _procedure = OnKey;
    }

    /// Raised on the dispatcher with the new peek state.
    public event Action<bool>? PeekChanged;

    /// Raised on the dispatcher when Ctrl+Alt+Win+E is pressed.
    public event Action? ToggleEnabled;

    public void Start() => _thread.Post(() =>
    {
        if (_hook != IntPtr.Zero) return;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _procedure, NativeMethods.GetModuleHandle(null), 0);
    });

    /// Ends a peek and forgets the held keys, when the hook may have missed a release (the secure desktop of a lock or
    /// a UAC prompt keeps keys from every hook).
    public void Reset() => _thread.Post(() => Apply(_recognizer.Reset()));

    private unsafe IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = (int)wParam;
            var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            if (isDown || message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
            {
                var info = *(NativeMethods.KeyboardHookInfo*)lParam;
                var outcome = _recognizer.Handle(new HotkeyRecognizer.Event((int)info.VirtualKey, isDown, (int)info.ScanCode));
                Apply(outcome);
                if (outcome.Swallows) return 1;
            }
        }
        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void Apply(HotkeyRecognizer.Outcome outcome)
    {
        if (outcome.Peek is { } peek) _dispatcher.BeginInvoke(() => PeekChanged?.Invoke(peek));
        if (outcome.TogglesEnabled) _dispatcher.BeginInvoke(() => ToggleEnabled?.Invoke());
    }

    public void Dispose() => _thread.Post(() =>
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    });
}
