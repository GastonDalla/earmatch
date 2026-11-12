using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Earmatch.App.Controls;
using Earmatch.App.Theme;
using Earmatch.Core;

namespace Earmatch.App.Dialogs;

public enum UpdateChoice
{
    Install,
    Later,
    Skip,
}

public sealed class UpdateWindow : DialogWindow
{
    private readonly Grid _choices = new();
    private readonly ActionButton _install;
    private readonly FrameworkElement _download;
    private readonly ProgressBar _progress = new() { Height = 6, Minimum = 0, Maximum = 100, BorderThickness = new Thickness(0) };
    private readonly TextBlock _progressText = Ui.Text("", TypeStyle.Status, WindowTheme.SecondaryText);
    private readonly TextBlock _status = Ui.Text("", TypeStyle.Caption, WindowTheme.Waiting, wraps: true);
    private TaskCompletionSource<UpdateChoice>? _choice;
    private CancellationTokenSource? _downloading;
    private bool _isRestarting;

    public UpdateWindow(string version, string currentVersion, string? releaseNotes, Window? owner) : base(UpdateText.WindowTitle, owner)
    {
        Width = 620;
        MinWidth = 480;
        MinHeight = 300;

        var icon = new Image { Source = AppIcon.Large, Width = 64, Height = 64, VerticalAlignment = VerticalAlignment.Top };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var title = Ui.Text(UpdateText.Headline, new TypeStyle(15, FontWeights.Bold), WindowTheme.PrimaryText, wraps: true);
        AutomationHeading(title);
        var body = Ui.Text(UpdateText.Question(version, currentVersion), TypeStyle.Body, WindowTheme.SecondaryText, wraps: true);
        body.Margin = new Thickness(0, Spacing.Tight, 0, 0);

        var notes = ReleaseNotes.Parse(releaseNotes);
        var texts = new Grid();
        texts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        texts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        texts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        texts.RowDefinitions.Add(new RowDefinition { Height = notes.Count > 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        texts.Children.Add(title);
        Place(texts, body, 1);
        if (notes.Count > 0)
        {
            Height = 440;
            var notesTitle = Ui.Text(UpdateText.ReleaseNotes, TypeStyle.BodyStrong, WindowTheme.PrimaryText);
            notesTitle.Margin = new Thickness(0, Spacing.Medium, 0, Spacing.Small);
            Place(texts, notesTitle, 2);
            Place(texts, NotesView(notes), 3);
        }
        else
        {
            SizeToContent = SizeToContent.Height;
        }

        _install = ActionButton.Plain(ActionKind.Accent, UpdateText.Install, () => Choose(UpdateChoice.Install));
        var later = ActionButton.Plain(ActionKind.Pill, UpdateText.Later, () => Choose(UpdateChoice.Later));
        var skip = ActionButton.Plain(ActionKind.Pill, UpdateText.Skip, () => Choose(UpdateChoice.Skip));
        _choices.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _choices.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _choices.Children.Add(skip);
        var right = Ui.HStack(Spacing.Small, later, _install);
        Grid.SetColumn(right, 2);
        _choices.Children.Add(right);

        _progress.Foreground = WindowTheme.Accent;
        _progress.Background = WindowTheme.Well;
        var cancel = ActionButton.Plain(ActionKind.Pill, UpdateText.Cancel, CancelDownload);
        var progressRow = new Grid();
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _progress.VerticalAlignment = VerticalAlignment.Center;
        progressRow.Children.Add(_progress);
        _progressText.Margin = new Thickness(Spacing.Small, 0, Spacing.Medium, 0);
        _progressText.MinWidth = 40;
        _progressText.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_progressText, 1);
        progressRow.Children.Add(_progressText);
        Grid.SetColumn(cancel, 2);
        progressRow.Children.Add(cancel);
        _download = Ui.VStack(Spacing.Small, Ui.Text(UpdateText.Downloading, TypeStyle.BodyStrong, WindowTheme.PrimaryText), progressRow);
        _download.Visibility = Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(_progress, UpdateText.Downloading);

        _status.Visibility = Visibility.Collapsed;
        var footer = Ui.VStack(Spacing.Small, _status, _choices, _download);
        footer.Margin = new Thickness(0, Spacing.Medium, 0, 0);

        var layout = new Grid { Margin = new Thickness(Spacing.Large - 4, Spacing.Large - 4, Spacing.Large - 4, Spacing.Medium) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        icon.Margin = new Thickness(0, 0, Spacing.Large - 4, 0);
        layout.Children.Add(icon);
        Grid.SetColumn(texts, 1);
        layout.Children.Add(texts);
        Grid.SetColumn(footer, 1);
        Grid.SetRow(footer, 1);
        layout.Children.Add(footer);
        Content = layout;
        Loaded += (_, _) => Keyboard.Focus(_install);
    }

    public Task<UpdateChoice> ChooseAsync()
    {
        _choice = new TaskCompletionSource<UpdateChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        _isRestarting = false;
        _choices.Visibility = Visibility.Visible;
        _download.Visibility = Visibility.Collapsed;
        ShowAndActivate();
        return _choice.Task;
    }

    public CancellationToken ShowDownload()
    {
        _downloading?.Dispose();
        _downloading = new CancellationTokenSource();
        _status.Visibility = Visibility.Collapsed;
        _choices.Visibility = Visibility.Collapsed;
        _download.Visibility = Visibility.Visible;
        ShowProgress(0);
        return _downloading.Token;
    }

    public void ShowProgress(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        _progress.Value = clamped;
        _progressText.Text = $"{clamped}%";
    }

    public void ShowProblem(string message) => ShowStatus(message, WindowTheme.Waiting);

    public void ShowRestarting()
    {
        _isRestarting = true;
        _downloading?.Dispose();
        _downloading = null;
        ShowStatus(UpdateText.Restarting, WindowTheme.SecondaryText);
        _download.Visibility = Visibility.Collapsed;
    }

    private void ShowStatus(string message, Brush brush)
    {
        _status.Text = message;
        _status.Foreground = brush;
        _status.Visibility = Visibility.Visible;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _choices.Visibility == Visibility.Visible && Keyboard.FocusedElement is not ActionButton)
        {
            Choose(UpdateChoice.Install);
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void Dismiss()
    {
        if (_download.Visibility == Visibility.Visible) CancelDownload();
        else Choose(UpdateChoice.Later);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_isRestarting) return;
        _choice?.TrySetResult(UpdateChoice.Later);
        _downloading?.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _downloading?.Dispose();
        _downloading = null;
    }

    private void Choose(UpdateChoice choice) => _choice?.TrySetResult(choice);

    private void CancelDownload() => _downloading?.Cancel();

    private static void Place(Grid grid, UIElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    private static void AutomationHeading(UIElement element) =>
        System.Windows.Automation.AutomationProperties.SetHeadingLevel(element, System.Windows.Automation.AutomationHeadingLevel.Level1);

    private static Border NotesView(IReadOnlyList<ReleaseNoteBlock> notes)
    {
        var stack = new Stack { Spacing = 6 };
        foreach (var note in notes)
        {
            switch (note.Kind)
            {
                case ReleaseNoteKind.Heading:
                    stack.Children.Add(Ui.Text(note.Text, TypeStyle.Headline, WindowTheme.PrimaryText, wraps: true));
                    break;
                case ReleaseNoteKind.Bullet:
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var bullet = Ui.Text("•", TypeStyle.Body, WindowTheme.SecondaryText);
                    bullet.VerticalAlignment = VerticalAlignment.Top;
                    row.Children.Add(bullet);
                    var text = Ui.Text(note.Text, TypeStyle.Body, WindowTheme.PrimaryText, wraps: true);
                    text.VerticalAlignment = VerticalAlignment.Top;
                    Grid.SetColumn(text, 1);
                    row.Children.Add(text);
                    stack.Children.Add(row);
                    break;
                default:
                    stack.Children.Add(Ui.Text(note.Text, TypeStyle.Body, WindowTheme.PrimaryText, wraps: true));
                    break;
            }
        }
        var scroller = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(12, 10, 12, 10),
            Focusable = true,
        };
        System.Windows.Automation.AutomationProperties.SetName(scroller, UpdateText.ReleaseNotes);
        return Ui.Surface(scroller, 10, WindowTheme.Well, WindowTheme.Hairline);
    }
}

public static class UpdateText
{
    public static string WindowTitle => L10n.Text("Software Update");
    public static string Headline => L10n.Text("A new version of Earmatch is available!");
    public static string ReleaseNotes => L10n.Text("Release Notes:");
    public static string Skip => L10n.Text("Skip This Version");
    public static string Later => L10n.Text("Remind Me Later");
    public static string Install => L10n.Text("Install Update");
    public static string Cancel => L10n.Text("Cancel");
    public static string Downloading => L10n.Text("Downloading Update…");
    public static string Restarting => L10n.Text("Earmatch quits and opens again to finish.");

    public static string Question(string version, string current) =>
        L10n.Format("Earmatch %@ is now available—you have %@. Would you like to download it now?", version, current);
}
