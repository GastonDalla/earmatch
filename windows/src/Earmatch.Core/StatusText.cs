using System.Globalization;

namespace Earmatch.Core;

public enum StatusTone
{
    Live,
    Waiting,
    Off,
}

public readonly record struct StatusLine(StatusTone Tone, string Text);

/// What the status cards of the main window are about. Windows needs no Screen Recording grant, so a capture that
/// cannot run is reported through CaptureProblem only.
public sealed record StatusContext
{
    public StatusContext(OverlayActivation activation, OverlaySettings settings, string? rekordboxVersion, string? captureProblem = null,
        string? layoutName = null)
    {
        Activation = activation;
        Settings = settings;
        RekordboxVersion = rekordboxVersion;
        CaptureProblem = captureProblem;
        LayoutName = layoutName;
    }

    public OverlayActivation Activation { get; init; }
    public OverlaySettings Settings { get; init; }
    public string? RekordboxVersion { get; init; }
    public string? CaptureProblem { get; init; }
    public string? LayoutName { get; init; }

    internal DeckArrangement? Arrangement => DeckArrangement.FromRawValue(LayoutName);
}

public static partial class StatusText
{
    public static StatusLine Connection(StatusContext context) =>
        context.Activation.Kind == OverlayActivationKind.RekordboxNotRunning
            ? new StatusLine(StatusTone.Waiting, context.Activation.Message)
            : new StatusLine(StatusTone.Live, ConnectedText(context.RekordboxVersion, context.LayoutName));

    private static string ConnectedText(string? version, string? layout) => (version, layout) switch
    {
        ({ } v, { } l) => L10n.Format("rekordbox %@ connected · %@", v, l),
        ({ } v, null) => L10n.Format("rekordbox %@ connected", v),
        (null, { } l) => L10n.Format("rekordbox connected · %@", l),
        _ => L10n.Text("rekordbox connected"),
    };

    public static StatusLine BPMPrecision(StatusContext context)
    {
        var settings = context.Settings;
        var isOff = !settings.IsBPMLimitEnabled && !settings.IsBlindModeEnabled;
        if (Blocked(context, isOff ? L10n.Text("Off: rekordbox shows the full BPM") : null) is { } blocked) return blocked;
        if (!settings.IsBPMLimitEnabled) return new StatusLine(StatusTone.Live, L10n.Text("Live: pitch and header BPM covered; jog BPM in full"));
        return JogLimit(context, settings.IsBlindModeEnabled);
    }

    private static StatusLine JogLimit(StatusContext context, bool isBlind)
    {
        var limit = context.Settings.BPMLimit;
        if (limit != BPMLimit.CoverFully && CaptureProblem(context) is { } problem) return problem;
        var text = (limit, isBlind) switch
        {
            (BPMLimit.OneDecimal, false) => L10n.Text("Live: jog BPM shows one decimal"),
            (BPMLimit.NoDecimals, false) => L10n.Text("Live: jog BPM shows no decimals"),
            (BPMLimit.CoverFully, false) => L10n.Text("Live: jog BPM covered"),
            (BPMLimit.OneDecimal, true) => L10n.Text("Live: jog BPM shows one decimal; pitch and header BPM covered"),
            (BPMLimit.NoDecimals, true) => L10n.Text("Live: jog BPM shows no decimals; pitch and header BPM covered"),
            _ => L10n.Text("Live: jog BPM covered; pitch and header BPM covered"),
        };
        return new StatusLine(StatusTone.Live, text);
    }

    public static StatusLine Waveform(StatusContext context)
    {
        var mode = context.Settings.WaveformMode;
        if (Blocked(context, mode == WaveformMode.Normal ? L10n.Text("Normal: rekordbox waveform unchanged") : null) is { } blocked) return blocked;
        var layout = context.LayoutName;
        if (mode == WaveformMode.HideBoth)
        {
            if (context.Settings.IsPhaseMeterEnabled && CaptureProblem(context) is { } hiddenProblem) return hiddenProblem;
            return new StatusLine(StatusTone.Live, layout is null ? L10n.Text("Live: both waveforms hidden") : L10n.Format("Live in %@: waveforms hidden", layout));
        }
        if (context.Arrangement?.Orientation == StripOrientation.Vertical)
        {
            return new StatusLine(StatusTone.Live, L10n.Format("Live in %@: each deck keeps its own column", layout ?? ""));
        }
        if (CaptureProblem(context) is { } problem) return problem;
        return new StatusLine(StatusTone.Live,
            layout is null ? L10n.Text("Live: decks split, one playhead each") : L10n.Format("Live in %@: decks split, one playhead each", layout));
    }

    public static StatusLine PhaseMeter(StatusContext context)
    {
        var settings = context.Settings;
        if (Blocked(context, settings.IsPhaseMeterEnabled ? null : L10n.Text("Phase meter off")) is { } blocked) return blocked;
        if (CaptureProblem(context) is { } problem) return problem;
        var title = settings.PhaseMeterType.Title;
        var showsMasterWaveform = settings.WaveformMode == WaveformMode.Split && context.Arrangement?.Orientation != StripOrientation.Vertical;
        var needsSplit = settings.PhaseMeterType == PhaseMeterType.Cdj3000Waveform && !showsMasterWaveform;
        if (context.LayoutName is not { } layout)
        {
            return new StatusLine(StatusTone.Live,
                needsSplit ? L10n.Format("Live: %@ · master waveform in Split only", title) : L10n.Format("Live: %@", title));
        }
        return new StatusLine(StatusTone.Live, needsSplit
            ? L10n.Format("Live in %@: %@ · master waveform in horizontal Split only", layout, title)
            : L10n.Format("Live in %@: %@", layout, title));
    }

    internal static StatusLine? Blocked(StatusContext context, string? featureOff)
    {
        if (!context.Settings.IsEnabled) return new StatusLine(StatusTone.Off, L10n.Text("Earmatch is off"));
        if (featureOff is not null) return new StatusLine(StatusTone.Off, featureOff);
        if (!context.Activation.IsActive) return new StatusLine(StatusTone.Waiting, context.Activation.Message);
        return null;
    }

    internal static StatusLine? CaptureProblem(StatusContext context)
    {
        var problem = Text.TrimWhitespacesAndNewlines(context.CaptureProblem ?? "");
        return problem.Length == 0 ? null : new StatusLine(StatusTone.Waiting, problem);
    }
}

/// How a BPM reads under a limit: the digits left visible and the digits the jog mask covers.
public readonly record struct BPMSample(string Visible, string Covered)
{
    public BPMSample(double bpm, BPMLimit? limit) : this("", "")
    {
        var text = bpm.ToString("F2", CultureInfo.InvariantCulture);
        var visibleCount = limit switch
        {
            null => text.Length,
            BPMLimit.OneDecimal => text.Length - 1,
            BPMLimit.NoDecimals => text.Length - 3,
            _ => 0,
        };
        Visible = text[..visibleCount];
        Covered = text[visibleCount..];
    }
}
