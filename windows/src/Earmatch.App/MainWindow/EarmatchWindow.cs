using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The main window. Closing it hides it, like the macOS window: Earmatch keeps running in the tray until Quit.
/// WindowChrome takes the title bar away so the content reaches the top edge as on macOS; the top band drags the
/// window (double-click maximizes, right-click opens the system menu) and the caption buttons are drawn by WPF, with
/// the maximize button reported to Windows as HTMAXBUTTON so Windows 11 still shows Snap Layouts over it.
public sealed class EarmatchWindow : Window
{
    private readonly MainWindowModel _model;
    private readonly MainWindowFrame _frame;
    private readonly WindowChrome _chrome;
    private bool _isOverMaximize;
    private bool _isPressingMaximize;

    public EarmatchWindow(MainWindowModel model)
    {
        _model = model;
        _frame = new MainWindowFrame(model);
        Title = "Earmatch";
        Icon = AppIcon.WindowIcon;
        Width = MainWindowMetrics.DefaultSize.Width;
        Height = MainWindowMetrics.DefaultSize.Height;
        MinWidth = MainWindowMetrics.MinimumSize.Width;
        MinHeight = MainWindowMetrics.MinimumSize.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = WindowTheme.Background;
        Content = _frame;

        var resize = SystemParameters.WindowResizeBorderThickness;
        _chrome = new WindowChrome
        {
            // WindowChrome counts the caption from below the top resize border.
            CaptionHeight = Math.Max(0, MainWindowFrame.TitleBandHeight - resize.Top),
            ResizeBorderThickness = resize,
            // A sliver of DWM frame keeps the shadow and the rounded corners; WPF's opaque background covers it.
            GlassFrameThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        };
        WindowChrome.SetWindowChrome(this, _chrome);

        var captions = _frame.Captions;
        captions.Minimize.Click += (_, _) => SystemCommands.MinimizeWindow(this);
        captions.Maximize.Click += (_, _) => ToggleMaximized();
        captions.Close.Click += (_, _) => SystemCommands.CloseWindow(this);
        Activated += (_, _) => captions.IsWindowActive = true;
        Deactivated += (_, _) => captions.IsWindowActive = false;
        StateChanged += (_, _) => FitFrame();
        SizeChanged += (_, _) => FitFrame();
        DpiChanged += (_, _) => FitFrame();
    }

    public bool IsQuitting { get; set; }

    public void ShowAndActivate()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        // WindowChrome adds its message hook while SourceInitialized is raised. HwndSource runs the newest hook first,
        // so this one sees WM_NCHITTEST before WindowChrome answers it.
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source) source.AddHook(OnMessage);
        NativeMethods.StyleFrame(this, WindowTheme.SidebarColor);
        FitFrame();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (IsQuitting) return;
        e.Cancel = true;
        Hide();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down or Key.Space)
        {
            if (!Sidebar.IsKeyboardNavigating)
            {
                Sidebar.IsKeyboardNavigating = true;
                _frame.View.Sidebar.Refresh();
            }
        }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        var digit = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => (char)('1' + (e.Key - Key.D1)),
            >= Key.NumPad1 and <= Key.NumPad9 => (char)('1' + (e.Key - Key.NumPad1)),
            _ => '\0',
        };
        if (digit != '\0' && MainWindowPane.FromKeyEquivalent(digit.ToString()) is { } pane)
        {
            _model.Select(pane);
            e.Handled = true;
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (!Sidebar.IsKeyboardNavigating) return;
        Sidebar.IsKeyboardNavigating = false;
        _frame.View.Sidebar.Refresh();
    }

    private void ToggleMaximized()
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    /// Keeps a maximized window's content on the monitor (Windows hangs the frame past its edges) and shows Restore.
    private void FitFrame()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        _frame.Captions.IsMaximized = isMaximized;
        _frame.Margin = isMaximized ? NativeMethods.MaximizedOverhang(this) : new Thickness(0);
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case NativeMethods.WmNcHitTest:
                if (CaptionHitTest(NativeMethods.ScreenPoint(lParam)) is { } hit)
                {
                    handled = true;
                    return hit;
                }
                break;
            case NativeMethods.WmNcMouseMove when wParam == NativeMethods.HtMaxButton:
                // Left to DefWindowProc, which opens the Snap Layouts flyout after a moment over the button.
                NativeMethods.TrackNonClientLeave(hwnd);
                break;
            case NativeMethods.WmNcMouseLeave:
                ShowMaximize(isOver: false, isPressing: false);
                break;
            case NativeMethods.WmNcLButtonDown or NativeMethods.WmNcLButtonDblClk when wParam == NativeMethods.HtMaxButton:
                // Handled here: DefWindowProc would draw and track a classic caption button over ours.
                ShowMaximize(isOver: true, isPressing: true);
                handled = true;
                break;
            case NativeMethods.WmNcLButtonUp when wParam == NativeMethods.HtMaxButton:
                var wasPressing = _isPressingMaximize;
                ShowMaximize(isOver: true, isPressing: false);
                if (wasPressing) ToggleMaximized();
                handled = true;
                break;
            case NativeMethods.WmNcLButtonUp:
                ShowMaximize(_isOverMaximize, isPressing: false);
                break;
        }
        return IntPtr.Zero;
    }

    /// What the caption buttons are for Windows: the top resize border over them while the window is restored, the
    /// maximize button as HTMAXBUTTON; null leaves the point to WindowChrome (minimize and close answer HTCLIENT and
    /// take WPF's mouse input like any button).
    private IntPtr? CaptionHitTest(Point screen)
    {
        var captions = _frame.Captions;
        if (!captions.IsVisible || !IsInside(captions, screen))
        {
            ShowMaximize(isOver: false, _isPressingMaximize);
            return null;
        }
        if (WindowState == WindowState.Normal)
        {
            var point = PointFromScreen(screen);
            var border = _chrome.ResizeBorderThickness;
            if (point.Y < border.Top)
            {
                ShowMaximize(isOver: false, _isPressingMaximize);
                return point.X >= ActualWidth - border.Right ? NativeMethods.HtTopRight : NativeMethods.HtTop;
            }
        }
        var isOver = IsInside(captions.Maximize, screen);
        ShowMaximize(isOver, _isPressingMaximize);
        return isOver ? NativeMethods.HtMaxButton : null;
    }

    private static bool IsInside(FrameworkElement element, Point screen)
    {
        if (PresentationSource.FromVisual(element) is null) return false;
        var point = element.PointFromScreen(screen);
        return point.X >= 0 && point.Y >= 0 && point.X < element.ActualWidth && point.Y < element.ActualHeight;
    }

    private void ShowMaximize(bool isOver, bool isPressing)
    {
        _isOverMaximize = isOver;
        _isPressingMaximize = isPressing;
        _frame.Captions.Maximize.ShowNonClientState(isOver, isPressing);
    }
}
