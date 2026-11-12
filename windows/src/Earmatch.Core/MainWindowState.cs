namespace Earmatch.Core;

public readonly record struct DeckPhases(double One, double Two);

public class MainWindowState
{
    private readonly SettingsStore? _store;
    private LiveDecksSnapshot? _snapshot;
    private OverlaySettings _settings;

    public MainWindowState(SettingsStore store, Action<Action> post)
    {
        _store = store;
        _settings = store.Settings;
        Pane = MainWindowPane.Initial(store.Value(MainWindowPane.StorageKey));
        Language = AppLanguageChoice.FromStoredValue(store.Value(AppLanguageChoice.StorageKey));
        store.Changed += (_, _) => post(() => Show(store.Settings));
    }

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

    public UpdateState Updates { get; private set; } = UpdateState.Off("not started");

    public StatusContext Status => new(Activation, _settings, RekordboxVersion, CaptureProblem, LayoutName);

    public MainWindowStatus WindowStatus => MainWindowStatus.From(_settings.IsEnabled, Activation);

    public LiveDecksSnapshot? Snapshot => _snapshot;

    public IReadOnlyList<LiveDeckSnapshot> Decks => (_snapshot?.Decks ?? []).OrderBy(deck => deck.Number).ToList();

    public bool IsMirroringDecks => StatusText.IsMirroringDecks(Status, _snapshot);

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

    public void ApplyCaptureProblem(string? problem)
    {
        if (problem == CaptureProblem) return;
        CaptureProblem = problem;
        Changed?.Invoke();
    }

    public void ApplyPractice(PracticeOverview overview, PracticeLiveStatus? live)
    {
        if (overview == PracticeOverview && live == PracticeLive) return;
        PracticeOverview = overview;
        PracticeLive = live;
        Changed?.Invoke();
    }

    public void ApplyUpdates(UpdateState updates)
    {
        if (updates == Updates) return;
        Updates = updates;
        Changed?.Invoke();
    }

    public void ApplyLive(LiveDecksSnapshot snapshot)
    {
        if (snapshot == _snapshot) return;
        _snapshot = snapshot;
        Changed?.Invoke();
    }

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
