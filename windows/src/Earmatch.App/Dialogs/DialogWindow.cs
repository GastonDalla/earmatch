using System.Windows;
using System.Windows.Input;
using Earmatch.App.Theme;

namespace Earmatch.App.Dialogs;

public class DialogWindow : Window
{
    public DialogWindow(string title, Window? owner)
    {
        Title = title;
        Icon = AppIcon.WindowIcon;
        Background = WindowTheme.Background;
        Foreground = WindowTheme.PrimaryText;
        UseLayoutRounding = true;
        ShowInTaskbar = owner is null;
        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public bool IsClosed { get; private set; }

    public void CloseIfOpen()
    {
        if (!IsClosed) Close();
    }

    public void ShowAndActivate()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.StyleFrame(this, WindowTheme.BackgroundColor);
    }

    protected override void OnClosed(EventArgs e)
    {
        IsClosed = true;
        base.OnClosed(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.Escape) return;
        Dismiss();
        e.Handled = true;
    }

    protected virtual void Dismiss() => Close();
}
