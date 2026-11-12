using Earmatch.Core;

namespace Earmatch.App.Capture;

/// What the frames are read for: the decks as UI Automation last located them, the window rectangle of that read
/// (physical pixels) and its layout scale, and the analysis options.
internal sealed record FrameReaderInput(IReadOnlyList<DeckLayout> Decks, NativeMethods.WindowRect ReadRect, double LayoutScale, FrameAnalysisOptions Options);

/// Reads each captured frame on the capture thread (FrameAnalyzer.swift's analyze): places the frame in the layout's
/// coordinates (FramePlacement: pixel (0, 0) at the window's visible bounds, checked in the VM against the decks UI
/// Automation locates with `Earmatch --capture-frame`), checks it shows the window the layout was read from, runs
/// Earmatch.Core's FrameAnalyzer and records the frame in the stats.
internal sealed class FrameReader
{
    private readonly object _gate = new();
    private readonly FrameAnalyzer _analyzer = new();
    private FrameReaderInput? _input;

    public FrameStats Stats { get; } = new(Environment.GetEnvironmentVariable("EARMATCH_TRACE") == "1");

    /// What to read the frames for; null drops them.
    public void Configure(FrameReaderInput? input)
    {
        lock (_gate) _input = input;
    }

    /// The frame read, or null (and its pixels given back) when nothing reads frames now.
    public CapturedFrame? Read(RawFrame raw)
    {
        var started = CaptureClock.Now;
        FrameReaderInput? input;
        lock (_gate) input = _input;
        if (input is null)
        {
            raw.Pixels.Release();
            return null;
        }
        var placement = FramePlacement.Of(raw.Bounds.Screen, raw.WindowRect.Screen, input.ReadRect.Screen);
        var pixels = raw.Pixels;
        var problem = FrameMappingProblem.Check(pixels.Width, pixels.Height, placement.ExpectedSize);
        var image = pixels.Image(placement.Origin, input.LayoutScale);
        var decks = _analyzer.Analyze(image, input.Decks, input.Options, problem is null, raw.Time);
        if (Stats.Record(CaptureClock.Now - started, raw.Time, decks) is { } trace) Console.Error.WriteLine(trace);
        return new CapturedFrame(pixels, placement.Origin, new FrameAnalysis(decks, problem, raw.Time), raw.Generation);
    }
}
