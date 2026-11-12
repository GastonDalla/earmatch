using Earmatch.App.Drawing;
using Earmatch.Core;

namespace Earmatch.App.Panes;

/// What the main window shows and changes (MainWindowModel.swift with LiveDecksModel and the practice controller's
/// state). Overlays, rekordbox monitoring and capture come in later tasks; until then the status waits for rekordbox.
public sealed class MainWindowModel
{
    private readonly SettingsStore? _store;
    private OverlaySettings _settings;
    private LiveDecksSnapshot? _snapshot;

    public MainWindowModel(SettingsStore store)
    {
        _store = store;
        _settings = store.Settings;
        Pane = MainWindowPane.Initial(store.Value(MainWindowPane.StorageKey));
        Language = AppLanguageChoice.FromStoredValue(store.Value(AppLanguageChoice.StorageKey));
        store.Changed += (_, _) =>
        {
            if (store.Settings == _settings) return;
            _settings = store.Settings;
            Changed?.Invoke();
        };
    }

    /// The model of a window render: the sample session, fixed at `now`.
    public MainWindowModel(OverlaySettings sample, MainWindowPane pane, StatusContext context, LiveDecksSnapshot decks,
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

    public Action CopyDiagnostics { get; set; } = () => { };
    public Action Quit { get; set; } = () => { };

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

    public string VersionLabel => SystemInfo.ShortVersion is { } version ? $"v{version}" : WindowText.DevelopmentBuild;

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
        if (copy == _settings) return;
        _settings = copy;
        Changed?.Invoke();
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
            AppLanguage.Install(choice);
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
}
