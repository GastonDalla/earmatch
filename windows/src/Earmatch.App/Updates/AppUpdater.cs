using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Earmatch.Core;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Earmatch.App.Updates;

/// Updates with Velopack, the Windows counterpart of AppUpdater.swift (Sparkle): only in a copy installed with
/// Earmatch's Setup, which checks the GitHub releases of its architecture's channel when it opens, once a day and on
/// Check for Updates…. An update found by the check at opening asks at once unless rekordbox is in front; any other
/// check that finds one only turns the menus' item into Update Available… (UpdatePrompt), so a set is never
/// interrupted. Installing asks first, downloads, quits Earmatch the normal way and lets Velopack restart it.
public sealed class AppUpdater : IDisposable
{
    /// How often the daily check looks whether a day has passed (UpdateSchedule decides).
    private static readonly TimeSpan ScheduleTick = TimeSpan.FromHours(1);

    private readonly UpdateManager? _manager;
    private readonly Func<bool> _isRekordboxFrontmost;
    private readonly Func<Window?> _owner;
    private readonly Action _quit;
    private readonly Stopwatch _sinceLaunch = Stopwatch.StartNew();
    private DispatcherTimer? _schedule;
    private UpdateInfo? _update;
    private bool _isBusy;

    /// <param name="isRekordboxFrontmost">Whether rekordbox (or one of its dialogs) is the foreground app.</param>
    /// <param name="owner">The main window while it is shown, to own the update's questions.</param>
    /// <param name="quit">Quits Earmatch the normal way (saves, removes the tray icon) once Velopack waits to update.</param>
    public AppUpdater(Func<bool> isRekordboxFrontmost, Func<Window?> owner, Action quit)
    {
        _isRekordboxFrontmost = isRekordboxFrontmost;
        _owner = owner;
        _quit = quit;
        (_manager, State) = Create();
    }

    public event Action? Changed;

    public UpdateState State { get; private set; }

    public bool IsAvailable => _manager is not null;

    /// The check at opening, then the daily one.
    public void Start()
    {
        if (_manager is null || _schedule is not null) return;
        Check(UpdateTrigger.Launch);
        _schedule = new DispatcherTimer { Interval = ScheduleTick };
        _schedule.Tick += (_, _) =>
        {
            if (UpdateSchedule.IsDue(State.LastCheck, DateTimeOffset.Now)) Check(UpdateTrigger.Daily);
        };
        _schedule.Start();
    }

    /// Check for Updates… or Update Available… in the tray menu or the Setup pane.
    public void ChooseMenuItem()
    {
        if (_manager is null || !State.IsMenuItemEnabled || _isBusy) return;
        if (State.Phase == UpdatePhase.Available && _update is not null) Install();
        else Check(UpdateTrigger.User);
    }

    public void Dispose()
    {
        _schedule?.Stop();
        _schedule = null;
    }

    /// The update manager of an installed copy, or why updates are off. Development builds (dotnet run, a published
    /// folder) are not installed, so they never check.
    private static (UpdateManager? Manager, UpdateState State) Create()
    {
        var feed = UpdateFeed.Resolve(Environment.GetEnvironmentVariable(UpdateFeed.OverrideVariable));
        if (feed.Kind == UpdateFeedKind.Invalid)
            return (null, UpdateState.Off($"{UpdateFeed.OverrideVariable} is not a full folder path: {feed.Location}"));
        try
        {
            IUpdateSource source = feed.Kind == UpdateFeedKind.GitHub
                ? new GithubSource(feed.Location, accessToken: null, prerelease: false)
                : new SimpleFileSource(new DirectoryInfo(feed.Location));
            var manager = new UpdateManager(source);
            if (!manager.IsInstalled) return (null, UpdateState.Off("development build, not installed with Earmatch's Setup"));
            var channel = VelopackLocator.IsCurrentSet ? VelopackLocator.Current.Channel : null;
            return (manager, UpdateState.Ready(channel ?? "default", feed.DisplayName));
        }
        catch (Exception exception)
        {
            // An updater that cannot start must never stop Earmatch from starting; the diagnostics say why.
            return (null, UpdateState.Off($"the updater could not start: {exception.Message}"));
        }
    }

    private async void Check(UpdateTrigger trigger)
    {
        if (_manager is null || _isBusy) return;
        _isBusy = true;
        Set(State.Checking());
        UpdateInfo? update;
        try
        {
            update = await _manager.CheckForUpdatesAsync();
        }
        catch (Exception exception)
        {
            // No network, GitHub's rate limit or a broken feed: each ends the check the same way.
            _update = null;
            Set(State.CheckFailed(DateTimeOffset.Now, exception.Message));
            if (trigger == UpdateTrigger.User) Tell(L10n.Format("Couldn't check for updates: %@", exception.Message), MessageBoxImage.Warning);
            return;
        }
        finally
        {
            _isBusy = false;
        }
        _update = update;
        Set(State.Checked(DateTimeOffset.Now, update?.TargetFullRelease.Version.ToString()));
        if (update is null)
        {
            if (trigger == UpdateTrigger.User) Tell(L10n.Format("You have the latest version of Earmatch (%@).", CurrentVersion), MessageBoxImage.Information);
            return;
        }
        if (UpdatePrompt.AsksNow(trigger, _sinceLaunch.Elapsed.TotalSeconds, _isRekordboxFrontmost())) Install();
    }

    /// Asks to install the update found; on yes it downloads it, hands it to Velopack and quits, and Velopack opens
    /// the new version. On no the menus go back to Check for Updates…, as Sparkle's Later does.
    private async void Install()
    {
        if (_manager is null || _update is not { } update || _isBusy) return;
        _isBusy = true;
        var version = update.TargetFullRelease.Version.ToString();
        var question = L10n.Format("Earmatch %@ is available (you have %@).", version, CurrentVersion) + "\n\n"
            + L10n.Text("Install it now? Earmatch quits and opens again to finish.");
        if (Ask(question) != MessageBoxResult.Yes)
        {
            _isBusy = false;
            Set(State.Postponed());
            return;
        }
        Set(State.Downloading());
        try
        {
            await _manager.DownloadUpdatesAsync(update);
            _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: false, restart: true);
        }
        catch (Exception exception)
        {
            _isBusy = false;
            Set(State.DownloadFailed(exception.Message));
            Tell(L10n.Format("Couldn't download the update: %@", exception.Message), MessageBoxImage.Warning);
            return;
        }
        Set(State.Restarting());
        _quit();
    }

    private string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? SystemInfo.ShortVersion ?? "?";

    private void Set(UpdateState state)
    {
        if (state == State) return;
        State = state;
        Changed?.Invoke();
    }

    private MessageBoxResult Ask(string text) => _owner() is { } owner
        ? MessageBox.Show(owner, text, "Earmatch", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes)
        : MessageBox.Show(text, "Earmatch", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes);

    private void Tell(string text, MessageBoxImage image)
    {
        if (_owner() is { } owner) MessageBox.Show(owner, text, "Earmatch", MessageBoxButton.OK, image);
        else MessageBox.Show(text, "Earmatch", MessageBoxButton.OK, image);
    }
}
