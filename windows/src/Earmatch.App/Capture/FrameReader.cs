using Earmatch.Core;

namespace Earmatch.App.Capture;

/// What the frames are read for: the decks as UI Automation last located them, the window rectangle of that read
/// (physical pixels) and its layout scale, and the analysis options.
internal sealed record FrameReaderInput(IReadOnlyList<DeckLayout> Decks, NativeMethods.WindowRect ReadRect, double LayoutScale, FrameAnalysisOptions Options);

/// Reads each captured frame on the capture thread (FrameAnalyzer.swift's analyze): places the frame in the layout's
/// coordinates, checks it shows the window the layout was read from, runs Earmatch.Core's FrameAnalyzer and records the
/// frame in the stats. Windows.Graphics.Capture captures a window's visible bounds (DWMWA_EXTENDED_FRAME_BOUNDS: the
/// window without its invisible resize borders, title bar included), so the frame's pixel (0, 0) is at those bounds'
/// top-left corner; checked in the VM against the decks UI Automation locates (`Earmatch --capture-frame`).
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
        var bounds = raw.Bounds.Width > 0 ? raw.Bounds : raw.WindowRect;
        var moved = new ScreenPoint(raw.WindowRect.Left - input.ReadRect.Left, raw.WindowRect.Top - input.ReadRect.Top);
        var origin = new ScreenPoint(bounds.Left - moved.X, bounds.Top - moved.Y);
        var expected = new ScreenSize(input.ReadRect.Width - (raw.WindowRect.Width - bounds.Width), input.ReadRect.Height - (raw.WindowRect.Height - bounds.Height));
        var pixels = raw.Pixels;
        var problem = FrameMappingProblem.Check(pixels.Width, pixels.Height, expected);
        var image = new PixelImage(pixels.Bytes, pixels.Width, pixels.Height, pixels.Stride, PixelImage.PixelLayout.Bgra, 1, origin, input.LayoutScale);
        var decks = _analyzer.Analyze(image, input.Decks, input.Options, problem is null, raw.Time);
        if (Stats.Record(CaptureClock.Now - started, raw.Time, decks) is { } trace) Console.Error.WriteLine(trace);
        return new CapturedFrame(pixels, origin, new FrameAnalysis(decks, problem, raw.Time), raw.Generation);
    }
}
