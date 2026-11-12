namespace Earmatch.Core;

/// The beats each deck's playhead is at, for the waveform preview.
public readonly record struct DeckPhases(double One, double Two);

/// What the main window shows and changes (MainWindowModel.swift with LiveDecksModel and the practice controller's
/// state), without WPF so it can be tested: the app's MainWindowModel adds the dispatcher, the language catalog and the
/// app's actions. The app applies rekordbox's activation (Apply) and the decks it mirrors (ApplyLive).
public class MainWindowState
{
    private readonly SettingsStore? _store;
    private LiveDecksSnapshot? _snapshot;
    private OverlaySettings _settings;

    /// A window backed by the settings file. The store may announce a change from another thread (the tray, a hotkey),
    /// so `post` runs the window's side of it on the window's own thread.
    public MainWindowState(SettingsStore store, Action<Action> post)
    {
        _store = store;
        _settings = store.Settings;
        Pane = MainWindowPane.Initial(store.Value(MainWindowPane.StorageKey));
        Language = AppLanguageChoice.FromStoredValue(store.Value(AppLanguageChoice.StorageKey));
        store.Changed += (_, _) => post(() => Show(store.Settings));
    }

    /// The model of a window render: the sample session, fixed at `now`.
    public MainWindowState(OverlaySettings sample, MainWindowPane pane, StatusContext context, LiveDecksSnapshot decks,
        PracticeOverview overview, PracticeLiveStatus? practiceLive, DateTimeOffset now)
    {
        _settings = sample;
        Pane = pane;
        Language = AppLanguageChoice.System;
        Activation = context.Activation;
        RekordboxVersion = context.RekordboxVersion;
        LayoutName = context.LayoutName;
        CaptureProblem = context.CaptureProblem;
        _snapshot = decks;
        PracticeOverview = overview;
        PracticeLive = practiceLive;
        FixedNow = now;
        IsSample = true;
    }

    public event Action? Changed;
    public event Action? PaneChanged;
    public event Action? LanguageChanged;

    public bool IsSample { get; }
    public DateTimeOffset? FixedNow { get; }
    public DateTimeOffset Now => FixedNow ?? DateTimeOffset.Now;

    public OverlaySettings Settings => _settings;
    public OverlayActivation Activation { get; private set; } = OverlayActivation.RekordboxNotRunning;
    public string? RekordboxVersion { get; private set; }
    public string? LayoutName { get; private set; }
    public string? CaptureProblem { get; private set; }
    public MainWindowPane Pane { get; private set; }
    public AppLanguageChoice Language { get; private set; }
    public PracticeOverview PracticeOverview { get; private set; } = PracticeOverview.Empty;
    public PracticeLiveStatus? PracticeLive { get; private set; }
    public bool IsMixing => PracticeLive is not null;

    public StatusContext Status => new(Activation, _settings, RekordboxVersion, CaptureProblem, LayoutName);

    public MainWindowStatus WindowStatus => MainWindowStatus.From(_settings.IsEnabled, Activation);

    public LiveDecksSnapshot? Snapshot => _snapshot;

    public IReadOnlyList<LiveDeckSnapshot> Decks => (_snapshot?.Decks ?? []).OrderBy(deck => deck.Number).ToList();

    public bool IsMirroringDecks => StatusText.IsMirroringDecks(Status, _snapshot);

    /// The meter the phase meter card mirrors: a deck that follows a master, else any deck with a meter.
    public PhaseMeterDisplay? PreviewMeter =>
        Decks.FirstOrDefault(deck => deck.Meter?.MasterPlayer is not null)?.Meter ?? Decks.FirstOrDefault(deck => deck.Meter is not null)?.Meter;

    public DeckPhases? WaveformPhases
    {
        get
        {
            double? Phase(int number)
            {
                var row = Decks.FirstOrDefault(deck => deck.Number == number)?.Meter?.Rows.FirstOrDefault(row => row.Role == PhaseMeterRole.Deck);
                return row?.BeatInBar is { } beat ? beat + (row.Fraction ?? 0) : null;
            }
            return Phase(1) is { } one && Phase(2) is { } two ? new DeckPhases(one, two) : null;
        }
    }

    public void Update(SettingsChange change)
    {
        if (_store is not null)
        {
            _store.Update(change);
            return;
        }
        var copy = _settings;
        change(ref copy);
        Show(copy);
    }

    public void Select(MainWindowPane pane)
    {
        if (pane == Pane) return;
        Pane = pane;
        _store?.SetValue(MainWindowPane.StorageKey, pane.RawValue);
        PaneChanged?.Invoke();
    }

    public void SelectLanguage(AppLanguageChoice choice)
    {
        if (choice == Language) return;
        Language = choice;
        if (_store is not null)
        {
            _store.SetValue(AppLanguageChoice.StorageKey, choice == AppLanguageChoice.System ? null : choice.RawValue);
            OnLanguageSelected(choice);
        }
        LanguageChanged?.Invoke();
    }

    public void Apply(OverlayActivation activation, string? rekordboxVersion, string? layoutName)
    {
        if (activation == Activation && rekordboxVersion == RekordboxVersion && layoutName == LayoutName) return;
        Activation = activation;
        RekordboxVersion = rekordboxVersion;
        LayoutName = layoutName;
        Changed?.Invoke();
    }

    /// Shows why the capture of rekordbox's window cannot be used, in the status lines; null when it can.
    public void ApplyCaptureProblem(string? problem)
    {
        if (problem == CaptureProblem) return;
        CaptureProblem = problem;
        Changed?.Invoke();
    }

    /// Shows the mix trainer's history and the transition it is timing.
    public void ApplyPractice(PracticeOverview overview, PracticeLiveStatus? live)
    {
        if (overview == PracticeOverview && live == PracticeLive) return;
        PracticeOverview = overview;
        PracticeLive = live;
        Changed?.Invoke();
    }

    /// Shows the decks Earmatch mirrors from rekordbox; LiveDecksSnapshot.Empty when it mirrors none.
    public void ApplyLive(LiveDecksSnapshot snapshot)
    {
        if (snapshot == _snapshot) return;
        _snapshot = snapshot;
        Changed?.Invoke();
    }

    /// Called before LanguageChanged rebuilds the window, so the rebuilt window reads the chosen language's texts.
    /// A render keeps the language it started with.
    protected virtual void OnLanguageSelected(AppLanguageChoice choice)
    {
    }

    private void Show(OverlaySettings settings)
    {
        if (settings == _settings) return;
        _settings = settings;
        Changed?.Invoke();
    }
}
