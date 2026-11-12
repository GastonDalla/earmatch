using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// The main window's model: MainWindowState (Earmatch.Core, where its logic is tested) on the window's dispatcher, with
/// the app's language catalog, version and actions.
public sealed class MainWindowModel : MainWindowState
{
    public MainWindowModel(SettingsStore store) : base(store, OnDispatcher(Dispatcher.CurrentDispatcher))
    {
    }

    /// The model of a window render: the sample session, fixed at `now`.
    public MainWindowModel(OverlaySettings sample, MainWindowPane pane, StatusContext context, LiveDecksSnapshot decks,
        PracticeOverview overview, PracticeLiveStatus? practiceLive, DateTimeOffset now)
        : base(sample, pane, context, decks, overview, practiceLive, now)
    {
    }

    /// Copies the diagnostics report and says whether the clipboard took it.
    public Func<bool> CopyDiagnostics { get; set; } = () => false;
    public Action Quit { get; set; } = () => { };

    public string VersionLabel => SystemInfo.ShortVersion is { } version ? $"v{version}" : WindowText.DevelopmentBuild;

    protected override void OnLanguageSelected(AppLanguageChoice choice) => AppLanguage.Install(choice);

    /// Runs a settings change at once on the window's thread and queues it there from any other thread.
    private static Action<Action> OnDispatcher(Dispatcher dispatcher) => action =>
    {
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    };
}
