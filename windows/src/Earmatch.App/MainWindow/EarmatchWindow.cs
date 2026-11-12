using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Panes;

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
            CaptionHeight = Math.Max(0, MainWindowFrame.TitleBandHeight - resize.Top),
            ResizeBorderThickness = resize,
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
        if (!IsKeyboardFocusWithin || ReferenceEquals(Keyboard.FocusedElement, this)) _frame.View.Sidebar.FocusList();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
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
                NativeMethods.TrackNonClientLeave(hwnd);
                break;
            case NativeMethods.WmNcMouseLeave:
                ShowMaximize(isOver: false, isPressing: false);
                break;
            case NativeMethods.WmNcLButtonDown or NativeMethods.WmNcLButtonDblClk when wParam == NativeMethods.HtMaxButton:
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
