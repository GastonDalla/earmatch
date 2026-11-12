using Earmatch.Core;

namespace Earmatch.App.Capture;

internal sealed record FrameReaderInput(IReadOnlyList<DeckLayout> Decks, NativeMethods.WindowRect ReadRect, double ScreenUnitsPerPoint,
    FrameAnalysisOptions Options);

internal sealed class FrameReader
{
    private readonly object _gate = new();
    private readonly FrameAnalyzer _analyzer = new();
    private FrameReaderInput? _input;

    public FrameStats Stats { get; } = new(Environment.GetEnvironmentVariable("EARMATCH_TRACE") == "1");

    public void Configure(FrameReaderInput? input)
    {
        lock (_gate) _input = input;
    }

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
        var image = pixels.Image(placement.Origin, input.ScreenUnitsPerPoint);
        var decks = _analyzer.Analyze(image, input.Decks, input.Options, problem is null, raw.Time);
        if (Stats.Record(CaptureClock.Now - started, raw.Time, decks) is { } trace) Console.Error.WriteLine(trace);
        return new CapturedFrame(pixels, placement.Origin, new FrameAnalysis(decks, problem, raw.Time), raw.Generation);
    }
}
