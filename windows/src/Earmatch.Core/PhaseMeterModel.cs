using System.Globalization;

namespace Earmatch.Core;

public readonly record struct BarPosition(int Bar, int Beat)
{
    public string Label => Printf.Format("%02d.%d", CultureInfo.InvariantCulture, [Bar, Beat]);
}

public sealed record PhaseMeterDeckInput(int Number, double? PhaseInBar, BarPosition? Counter);

public enum PhaseMeterBox
{
    Idle,
    MasterBeat,
    InPhase,
    OutOfPhase,
}

public readonly record struct PhaseMeterTick(double Offset, double Position, bool IsDownbeat);

public enum PhaseMeterRole
{
    Master,
    Deck,
}

public enum PhaseMeterTickDirection
{
    Up,
    Down,
}

public sealed record PhaseMeterRow(
    PhaseMeterRole Role,
    int DeckNumber,
    bool IsVacant,
    int? BeatInBar,
    double? Fraction,
    bool? IsOnMastersBeat,
    ValueList<PhaseMeterBox> Boxes,
    ValueList<PhaseMeterTick> Ticks,
    PhaseMeterTickDirection TickDirection,
    string Counter);

public sealed record PhaseMeterLabel(ValueList<string> Lines, int Player);

public enum PhaseMeterCounterSide
{
    Left,
    Right,
}

public sealed record PhaseMeterDisplay(
    PhaseMeterType Style,
    int? MasterPlayer,
    PhaseMeterLabel? Label,
    PhaseMeterCounterSide CounterSide,
    double Span,
    ValueList<PhaseMeterRow> Rows)
{
    public const string CounterUnit = "Bars";
    public const string EmptyCounter = "--.-";

    public bool ShowsMasterWaveform => Style == PhaseMeterType.Cdj3000Waveform && MasterPlayer is not null;
}

public static class PhaseMeterModel
{
    private const int BeatsPerBar = (int)BeatGridDetector.BeatsPerBar;
    public const double LineSpan = 4.0;
    public const double Cdj3000Span = 4.8;
    public static IReadOnlyList<string> MasterLabelLines { get; } = ["MASTER", "PLAYER"];

    public static double SpanFor(PhaseMeterType style) => style switch
    {
        PhaseMeterType.Box => 0,
        PhaseMeterType.Line => LineSpan,
        _ => Cdj3000Span,
    };

    public static PhaseMeterDisplay Display(int deckNumber, IReadOnlyList<PhaseMeterDeckInput> decks, int? master, PhaseMeterType style)
    {
        var own = decks.FirstOrDefault(deck => deck.Number == deckNumber) ?? new PhaseMeterDeckInput(deckNumber, null, null);
        var span = SpanFor(style);
        var otherMaster = master is { } number && number != deckNumber ? decks.FirstOrDefault(deck => deck.Number == number) : null;
        var counterSide = style.IsCDJ3000 ? PhaseMeterCounterSide.Left : PhaseMeterCounterSide.Right;

        if (otherMaster is null)
        {
            var rows = new List<PhaseMeterRow> { Row(own, PhaseMeterRole.Deck, Comparison.NoMaster, style, span) };
            if (style.IsCDJ3000) rows.Insert(0, VacantMasterRow(deckNumber));
            return new PhaseMeterDisplay(style, null, null, counterSide, span, rows);
        }
        var masterBeat = Beat(otherMaster.PhaseInBar) is { } beat ? Comparison.MasterBeat(beat) : Comparison.MasterUnread;
        PhaseMeterLabel? label = style switch
        {
            PhaseMeterType.Box or PhaseMeterType.Line => new PhaseMeterLabel(MasterLabelLines.ToValueList(), otherMaster.Number),
            PhaseMeterType.Cdj3000PhaseMeter => null,
            _ => new PhaseMeterLabel([], otherMaster.Number),
        };
        return new PhaseMeterDisplay(style, otherMaster.Number, label, counterSide, span,
        [
            Row(otherMaster, PhaseMeterRole.Master, Comparison.NoMaster, style, span),
            Row(own, PhaseMeterRole.Deck, masterBeat, style, span),
        ]);
    }

    private readonly record struct Comparison(bool HasMaster, int? Beat)
    {
        public static Comparison NoMaster => new(false, null);
        public static Comparison MasterUnread => new(true, null);
        public static Comparison MasterBeat(int beat) => new(true, beat);
    }

    private static PhaseMeterRow Row(PhaseMeterDeckInput deck, PhaseMeterRole role, Comparison comparison, PhaseMeterType style, double span)
    {
        var current = Beat(deck.PhaseInBar);
        bool? isOnMastersBeat = role == PhaseMeterRole.Deck && comparison.Beat is { } masterBeat && current is { } beat ? masterBeat == beat : null;
        var boxes = style != PhaseMeterType.Box
            ? []
            : Enumerable.Range(0, BeatsPerBar).Select(index =>
            {
                if (index != current) return PhaseMeterBox.Idle;
                if (role == PhaseMeterRole.Master) return PhaseMeterBox.MasterBeat;
                if (!comparison.HasMaster) return PhaseMeterBox.InPhase;
                if (comparison.Beat is null) return PhaseMeterBox.Idle;
                return isOnMastersBeat == true ? PhaseMeterBox.InPhase : PhaseMeterBox.OutOfPhase;
            }).ToValueList();
        return new PhaseMeterRow(
            role,
            deck.Number,
            IsVacant: false,
            current,
            deck.PhaseInBar is { } phase ? phase - Math.Floor(phase) : null,
            isOnMastersBeat,
            boxes,
            span > 0 && deck.PhaseInBar is { } tickPhase ? Ticks(tickPhase, span) : [],
            role == PhaseMeterRole.Master ? PhaseMeterTickDirection.Down : PhaseMeterTickDirection.Up,
            deck.Counter?.Label ?? PhaseMeterDisplay.EmptyCounter);
    }

    private static PhaseMeterRow VacantMasterRow(int deckNumber) =>
        new(PhaseMeterRole.Master, deckNumber, IsVacant: true, null, null, null, [], [], PhaseMeterTickDirection.Down, PhaseMeterDisplay.EmptyCounter);

    private static int? Beat(double? phase) => phase is { } value ? Math.Min((int)Math.Floor(value), BeatsPerBar - 1) : null;

    private static ValueList<PhaseMeterTick> Ticks(double phase, double span)
    {
        var first = (int)Math.Ceiling(phase - span);
        var last = (int)Math.Floor(phase + span);
        if (first > last) return [];
        return Enumerable.Range(first, last - first + 1).Select(n =>
        {
            var offset = n - phase;
            return new PhaseMeterTick(offset, offset / span, Numbers.PositiveModulo(n, BeatsPerBar) == 0);
        }).ToValueList();
    }
}
