using System.Windows.Threading;
using Earmatch.Core;

namespace Earmatch.App.Panes;

public sealed class MainWindowModel : MainWindowState
{
    public MainWindowModel(SettingsStore store) : base(store, OnDispatcher(Dispatcher.CurrentDispatcher))
    {
    }

    public MainWindowModel(OverlaySettings sample, MainWindowPane pane, StatusContext context, LiveDecksSnapshot decks,
        PracticeOverview overview, PracticeLiveStatus? practiceLive, DateTimeOffset now)
        : base(sample, pane, context, decks, overview, practiceLive, now)
    {
    }

    public Func<bool> CopyDiagnostics { get; set; } = () => false;
    public Action Quit { get; set; } = () => { };

    public Action CheckForUpdates { get; set; } = () => { };

    public string VersionLabel => SystemInfo.ShortVersion is { } version ? $"v{version}" : WindowText.DevelopmentBuild;

    protected override void OnLanguageSelected(AppLanguageChoice choice) => AppLanguage.Install(choice);

    private static Action<Action> OnDispatcher(Dispatcher dispatcher) => action =>
    {
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    };
}
