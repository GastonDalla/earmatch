using Earmatch.Core;

namespace Earmatch.Core.Tests;

/// The main window's state without WPF: the settings it reads and writes through the store, the pane and language it
/// remembers, and the live decks it mirrors.
public sealed class MainWindowStateTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), "earmatch-window-" + Guid.NewGuid().ToString("N"));
    readonly Queue<Action> _posted = new();

    string FilePath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    sealed class InstallingState(SettingsStore store, Action<Action> post) : MainWindowState(store, post)
    {
        public List<AppLanguageChoice> Installed { get; } = [];

        protected override void OnLanguageSelected(AppLanguageChoice choice) => Installed.Add(choice);
    }

    static void RunNow(Action action) => action();

    void RunPosted()
    {
        while (_posted.TryDequeue(out var action)) action();
    }

    static LiveDecksSnapshot Decks(params (int Number, double Phase)[] decks)
    {
        var inputs = decks.Select(deck => new PhaseMeterDeckInput(deck.Number, deck.Phase, new BarPosition(1, (int)deck.Phase + 1))).ToList();
        var master = decks.Length > 1 ? decks[^1].Number : (int?)null;
        return new LiveDecksSnapshot(decks.Select(deck => new LiveDeckSnapshot(deck.Number, $"Track {deck.Number}", deck.Number == master,
            PhaseMeterModel.Display(deck.Number, inputs, master, PhaseMeterType.Box), deck.Phase, 2)).ToValueList(), 60, 7.4);
    }

    static MainWindowState Sample(LiveDecksSnapshot decks, OverlaySettings? settings = null) =>
        new(settings ?? WindowRenderSample.Settings, MainWindowPane.Live, WindowRenderSample.StatusContext(2), decks,
            PracticeOverview.Empty, null, DateTimeOffset.UnixEpoch);

    [Fact]
    public void ItOpensOnTheRememberedPaneLanguageAndSettings()
    {
        var store = new SettingsStore(FilePath);
        store.Update((ref OverlaySettings settings) => BoothPreset.Xdj1000.Apply(ref settings));
        store.SetValue(MainWindowPane.StorageKey, "practice");
        store.SetValue(AppLanguageChoice.StorageKey, "es");

        var state = new MainWindowState(new SettingsStore(FilePath), RunNow);

        Assert.Equal(MainWindowPane.Practice, state.Pane);
        Assert.Equal(AppLanguageChoice.Spanish, state.Language);
        Assert.Equal(store.Settings, state.Settings);
        Assert.False(state.IsSample);
    }

    [Fact]
    public void AChangeIsSavedAndAnnouncedOnce()
    {
        var store = new SettingsStore(FilePath);
        var state = new MainWindowState(store, RunNow);
        var changes = 0;
        state.Changed += () => changes++;

        state.Update((ref OverlaySettings settings) => settings.IsEnabled = !settings.IsEnabled);

        Assert.Equal(1, changes);
        Assert.Equal(store.Settings, state.Settings);
        Assert.Equal(store.Settings, new SettingsStore(FilePath).Settings);
    }

    [Fact]
    public void AChangeThatChangesNothingIsNotAnnounced()
    {
        var state = new MainWindowState(new SettingsStore(FilePath), RunNow);
        var changes = 0;
        state.Changed += () => changes++;

        state.Update((ref OverlaySettings settings) => settings.IsEnabled = settings.IsEnabled);

        Assert.Equal(0, changes);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void AStoreChangeReachesTheWindowOnlyThroughItsThread()
    {
        var store = new SettingsStore(FilePath);
        var state = new MainWindowState(store, _posted.Enqueue);
        var before = state.Settings;
        var changes = 0;
        state.Changed += () => changes++;

        // The tray or a hotkey changes the store; the window reads it when its own thread runs the posted work.
        store.Update((ref OverlaySettings settings) => settings.IsBlindModeEnabled = !settings.IsBlindModeEnabled);

        Assert.Equal(before, state.Settings);
        Assert.Equal(0, changes);
        Assert.Single(_posted);
        RunPosted();
        Assert.Equal(store.Settings, state.Settings);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SeveralStoreChangesBeforeTheWindowRunsAreReadOnce()
    {
        var store = new SettingsStore(FilePath);
        var state = new MainWindowState(store, _posted.Enqueue);
        var changes = 0;
        state.Changed += () => changes++;

        store.Update((ref OverlaySettings settings) => settings.IsBlindModeEnabled = true);
        store.Update((ref OverlaySettings settings) => settings.IsMixTrainerEnabled = !settings.IsMixTrainerEnabled);
        RunPosted();

        Assert.Equal(store.Settings, state.Settings);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SelectingAPaneRemembersItAndAnnouncesItOnce()
    {
        var state = new MainWindowState(new SettingsStore(FilePath), RunNow);
        var changes = 0;
        state.PaneChanged += () => changes++;

        state.Select(MainWindowPane.Setup);
        state.Select(MainWindowPane.Setup);

        Assert.Equal(MainWindowPane.Setup, state.Pane);
        Assert.Equal(1, changes);
        Assert.Equal("setup", new SettingsStore(FilePath).Value(MainWindowPane.StorageKey));
    }

    [Fact]
    public void ALanguageIsInstalledAndRememberedBeforeTheWindowRebuilds()
    {
        var state = new InstallingState(new SettingsStore(FilePath), RunNow);
        var installedWhenRebuilt = new List<int>();
        state.LanguageChanged += () => installedWhenRebuilt.Add(state.Installed.Count);

        state.SelectLanguage(AppLanguageChoice.Spanish);
        state.SelectLanguage(AppLanguageChoice.Spanish);

        Assert.Equal([AppLanguageChoice.Spanish], state.Installed);
        Assert.Equal([1], installedWhenRebuilt);
        Assert.Equal("es", new SettingsStore(FilePath).Value(AppLanguageChoice.StorageKey));

        state.SelectLanguage(AppLanguageChoice.System);
        Assert.Null(new SettingsStore(FilePath).Value(AppLanguageChoice.StorageKey));
        Assert.Equal(AppLanguageChoice.System, state.Installed[^1]);
    }

    [Fact]
    public void ApplyingTheSameRekordboxStatusAnnouncesNothing()
    {
        var state = new MainWindowState(new SettingsStore(FilePath), RunNow);
        var changes = 0;
        state.Changed += () => changes++;

        state.Apply(OverlayActivation.Active, "7.2.4", "2Deck Horizontal");
        state.Apply(OverlayActivation.Active, "7.2.4", "2Deck Horizontal");

        Assert.Equal(1, changes);
        Assert.Equal(OverlayActivation.Active, state.Activation);
        Assert.Equal("7.2.4", state.RekordboxVersion);
        Assert.Equal("2Deck Horizontal", state.LayoutName);
    }

    [Fact]
    public void ARenderChangesItsSettingsInMemory()
    {
        var state = Sample(Decks((1, 1.25), (2, 3.5)));
        var changes = 0;
        state.Changed += () => changes++;

        state.Update((ref OverlaySettings settings) => settings.IsEnabled = false);

        Assert.True(state.IsSample);
        Assert.False(state.Settings.IsEnabled);
        Assert.Equal(1, changes);
        Assert.Equal(DateTimeOffset.UnixEpoch, state.Now);
    }

    [Fact]
    public void TheDecksAreListedInDeckOrder()
    {
        var state = Sample(Decks((3, 0.5), (1, 1.25), (2, 3.5)));

        Assert.Equal([1, 2, 3], state.Decks.Select(deck => deck.Number));
    }

    [Fact]
    public void TheWaveformFollowsDecksOneAndTwo()
    {
        var phases = Sample(Decks((1, 1.25), (2, 3.5))).WaveformPhases;

        Assert.NotNull(phases);
        Assert.Equal(1.25, phases.Value.One, precision: 9);
        Assert.Equal(3.5, phases.Value.Two, precision: 9);
        Assert.Null(Sample(Decks((1, 1.25))).WaveformPhases);
    }

    [Fact]
    public void ThePhaseMeterPreviewFollowsADeckThatFollowsAMaster()
    {
        var state = Sample(Decks((1, 1.25), (2, 3.5)));

        Assert.NotNull(state.PreviewMeter);
        Assert.Equal(2, state.PreviewMeter.MasterPlayer);
        Assert.Null(Sample(Decks()).PreviewMeter);
    }

    [Fact]
    public void TheDecksAreMirroredOnlyWhileEarmatchIsOn()
    {
        Assert.True(Sample(Decks((1, 1.25), (2, 3.5))).IsMirroringDecks);
        var off = WindowRenderSample.Settings with { IsEnabled = false };
        Assert.False(Sample(Decks((1, 1.25), (2, 3.5)), off).IsMirroringDecks);
        Assert.Equal(MainWindowStatus.Off, Sample(Decks(), off).WindowStatus);
    }
}
