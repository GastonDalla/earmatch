using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Hotkeys;

/// Earmatch's hotkeys (HotkeyMonitor.swift): a low-level keyboard hook on the hook thread feeds every key-down and
/// key-up to the HotkeyRecognizer and passes each one on to the apps, rekordbox included; only the E of
/// Ctrl+Alt+Win+E is kept from them, so Windows does not open File Explorer for Win+E; right after it the hook types the
/// unassigned mask key (HotkeyRecognizer.MaskKey) while the modifiers are still held, so releasing Win does not open the
/// Start menu (nor Alt rekordbox's menu bar). The peek and the toggle are raised on the window's dispatcher. Windows needs
/// no permission for the hook; when it refuses to install it, Problem says why (the diagnostics report it) and the hook
/// is tried again on the next Start.
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

    /// Why the keyboard hook is not installed, or null while it is.
    public string? Problem { get; private set; }

    public void Start() => _thread.Post(() =>
    {
        if (_hook != IntPtr.Zero) return;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _procedure, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            Problem = $"keyboard hook not installed (error {error}: {new System.ComponentModel.Win32Exception(error).Message})";
            System.Diagnostics.Trace.WriteLine($"earmatch: {Problem}");
        }
        else
        {
            Problem = null;
        }
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
        // Typed after the hook returns (its own events come back through it), while the shortcut's keys are still held.
        if (outcome.SendsMaskKey) _thread.Post(() => NativeMethods.TypeKey(HotkeyRecognizer.MaskKey));
    }

    public void Dispose() => _thread.Post(() =>
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    });
}
