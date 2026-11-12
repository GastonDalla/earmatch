using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Earmatch.App.Dialogs;
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
    private readonly SettingsStore _store;
    private readonly Func<bool> _isRekordboxFrontmost;
    private readonly Func<Window?> _owner;
    private readonly Action _quit;
    private readonly Stopwatch _sinceLaunch = Stopwatch.StartNew();
    private DispatcherTimer? _schedule;
    private UpdateInfo? _update;
    private UpdateWindow? _window;
    private bool _isBusy;

    /// <param name="isRekordboxFrontmost">Whether rekordbox (or one of its dialogs) is the foreground app.</param>
    /// <param name="owner">The main window while it is shown, to own the update's questions.</param>
    /// <param name="quit">Quits Earmatch the normal way (saves, removes the tray icon) once Velopack waits to update.</param>
    public AppUpdater(SettingsStore store, Func<bool> isRekordboxFrontmost, Func<Window?> owner, Action quit)
    {
        _store = store;
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
        Forget(CheckAsync(UpdateTrigger.Launch));
        _schedule = new DispatcherTimer { Interval = ScheduleTick };
        _schedule.Tick += (_, _) =>
        {
            if (UpdateSchedule.IsDue(State.LastCheck, DateTimeOffset.Now)) Forget(CheckAsync(UpdateTrigger.Daily));
        };
        _schedule.Start();
    }

    /// Check for Updates… or Update Available… in the tray menu or the Setup pane.
    public void ChooseMenuItem()
    {
        if (_manager is null || !State.IsMenuItemEnabled || _isBusy) return;
        if (State.Phase == UpdatePhase.Available && _update is not null) Forget(InstallAsync());
        else Forget(CheckAsync(UpdateTrigger.User));
    }

    public void Dispose()
    {
        _schedule?.Stop();
        _schedule = null;
        _window?.CloseIfOpen();
        _window = null;
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

    private async Task CheckAsync(UpdateTrigger trigger)
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
        var version = update?.TargetFullRelease.Version.ToString();
        var found = State.Checked(DateTimeOffset.Now, version);
        if (version is not null && UpdatePrompt.IsSkipped(trigger, version, _store.Value(UpdatePrompt.SkippedVersionKey)))
        {
            Set(found.Skipped());
            return;
        }
        Set(found);
        if (update is null)
        {
            if (trigger == UpdateTrigger.User) Tell(L10n.Format("You have the latest version of Earmatch (%@).", CurrentVersion), MessageBoxImage.Information);
            return;
        }
        if (UpdatePrompt.AsksNow(trigger, _sinceLaunch.Elapsed.TotalSeconds, _isRekordboxFrontmost())) await InstallAsync();
    }

    private async Task InstallAsync()
    {
        if (_manager is null || _update is not { } update || _isBusy) return;
        _isBusy = true;
        try
        {
            var version = update.TargetFullRelease.Version.ToString();
            _window ??= NewWindow(version, update.TargetFullRelease.NotesMarkdown);
            var window = _window;
            while (true)
            {
                switch (await window.ChooseAsync())
                {
                    case UpdateChoice.Later:
                        Set(State.Postponed());
                        window.CloseIfOpen();
                        return;
                    case UpdateChoice.Skip:
                        _store.SetValue(UpdatePrompt.SkippedVersionKey, version);
                        Set(State.Skipped());
                        window.CloseIfOpen();
                        return;
                }
                Set(State.Downloading());
                var cancellation = window.ShowDownload();
                try
                {
                    await _manager.DownloadUpdatesAsync(update, percent => window.Dispatcher.BeginInvoke(() => window.ShowProgress(percent)), cancellation);
                    window.ShowRestarting();
                    _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: false, restart: true);
                }
                catch (OperationCanceledException)
                {
                    Set(State.Postponed());
                    window.CloseIfOpen();
                    return;
                }
                catch (Exception exception)
                {
                    Set(State.DownloadFailed(exception.Message));
                    window.ShowProblem(L10n.Format("Couldn't download the update: %@", exception.Message));
                    continue;
                }
                Set(State.Restarting());
                _quit();
                return;
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static void Forget(Task task) => task.ContinueWith(
        failed => Trace.WriteLine($"earmatch: the updater stopped: {failed.Exception?.GetBaseException().Message}"),
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    private UpdateWindow NewWindow(string version, string? notes)
    {
        var window = new UpdateWindow(version, CurrentVersion, notes, _owner());
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window)) _window = null;
        };
        return window;
    }

    private string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? SystemInfo.ShortVersion ?? "?";

    private void Set(UpdateState state)
    {
        if (state == State) return;
        State = state;
        Changed?.Invoke();
    }

    private void Tell(string text, MessageBoxImage image)
    {
        if (_owner() is { } owner) MessageBox.Show(owner, text, "Earmatch", MessageBoxButton.OK, image);
        else MessageBox.Show(text, "Earmatch", MessageBoxButton.OK, image);
    }
}
