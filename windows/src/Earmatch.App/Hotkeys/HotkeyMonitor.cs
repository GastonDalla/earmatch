using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Hotkeys;

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

    public event Action<bool>? PeekChanged;

    public event Action? ToggleEnabled;

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
        if (outcome.SendsMaskKey) _thread.Post(() => NativeMethods.TypeKey(HotkeyRecognizer.MaskKey));
    }

    public void Dispose() => _thread.Post(() =>
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    });
}
