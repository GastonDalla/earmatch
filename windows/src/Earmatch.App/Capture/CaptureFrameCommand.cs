using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Earmatch.App.Rekordbox;
using Earmatch.Core;

namespace Earmatch.App.Capture;

internal static class CaptureFrameCommand
{
    public const string Flag = "--capture-frame";

    public static int Run(string folder)
    {
        using var error = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };
        using var process = RekordboxProcess.Find();
        var window = process?.MainWindow() ?? IntPtr.Zero;
        if (window == IntPtr.Zero)
        {
            error.WriteLine("rekordbox's main window was not found.");
            return 1;
        }
        RekordboxLayout layout;
        try
        {
            layout = RekordboxLayout.Locate(new UiaWindowReader().ReadWindow(window).Snapshot);
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or TimeoutException or COMException or InvalidOperationException)
        {
            error.WriteLine($"Reading rekordbox's window failed: {exception.Message}");
            return 1;
        }
        NativeMethods.GetWindowRect(window, out var readRect);

        RawFrame? captured = null;
        var dispatcher = Dispatcher.CurrentDispatcher;
        var done = new DispatcherFrame();
        using var capture = new WindowCapture(dispatcher);
        capture.FrameArrived = raw => dispatcher.BeginInvoke(() =>
        {
            if (captured is null) captured = raw;
            else raw.Pixels.Release();
            done.Continue = false;
        });
        capture.StatusChanged += status => error.WriteLine($"capture: {L10n.English(() => status.Message)}");
        capture.Follow(window);
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timeout.Tick += (_, _) => done.Continue = false;
        timeout.Start();
        Dispatcher.PushFrame(done);
        timeout.Stop();
        capture.Stop();
        if (captured is not { } frame)
        {
            error.WriteLine("No frame arrived within 5 seconds.");
            return 1;
        }

        Directory.CreateDirectory(folder);
        var pixels = frame.Pixels;
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Bgr32, null, pixels.Bytes, pixels.Stride);
        using (var file = File.Create(Path.Combine(folder, "frame.png")))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(file);
        }
        NativeMethods.ClientBounds(window, out var client);
        var lines = new List<string>
        {
            $"frame {pixels.Width}x{pixels.Height} at {frame.Time.ToString("F3", CultureInfo.InvariantCulture)} s (now {CaptureClock.Now.ToString("F3", CultureInfo.InvariantCulture)} s)",
            $"window rect {Describe(frame.WindowRect)}, read at {Describe(readRect)}",
            $"visible bounds {Describe(frame.Bounds)}",
            $"client {Describe(client)}",
            $"dpi {NativeMethods.GetDpiForWindow(window)}, layout {layout.LayoutName ?? "-"} scale {layout.Scale.ToString(CultureInfo.InvariantCulture)}",
        };
        foreach (var (name, rect) in new[] { ("visible bounds", frame.Bounds), ("window rect", frame.WindowRect), ("client", client) })
        {
            var origin = FramePlacement.OriginOf(rect.Screen, frame.WindowRect.Screen, readRect.Screen);
            var image = pixels.Image(origin, layout.Scale);
            var readings = new FrameAnalyzer().Analyze(image, layout.Decks, new FrameAnalysisOptions(true, true), isMapped: true, frame.Time);
            lines.Add($"origin at {name} ({origin.X}, {origin.Y}):");
            foreach (var deck in layout.Decks)
            {
                var reading = readings[deck.Number];
                var playhead = deck.Strip is { } strip ? PlayheadDetector.Locate(strip, image) : null;
                lines.Add($"  deck {deck.Number}: master={reading.IsMaster} grid={(reading.Grid is null ? "-" : $"{reading.Grid.BeatXs.Count} beats, spacing {reading.Grid.BeatSpacing:F2}")} "
                    + $"playhead={(playhead is { } x ? x.ToString("F1", CultureInfo.InvariantCulture) : "-")} strip center={deck.Strip?.Center.ToString("F1", CultureInfo.InvariantCulture) ?? "-"} "
                    + $"oneDecimal={Describe(reading.BPMCovers?.OneDecimal)} jog={Describe(deck.JogBPMFrame)} cue={reading.CueBeatsAhead?.ToString("F2", CultureInfo.InvariantCulture) ?? "-"}");
            }
        }
        File.WriteAllLines(Path.Combine(folder, "frame.txt"), lines);
        foreach (var line in lines) error.WriteLine(line);
        pixels.Release();
        return 0;
    }

    private static string Describe(NativeMethods.WindowRect rect) => $"({rect.Left},{rect.Top} {rect.Width}x{rect.Height})";

    private static string Describe(ScreenRect? rect) => rect is { } frame
        ? string.Create(CultureInfo.InvariantCulture, $"({frame.MinX:0.#},{frame.MinY:0.#} {frame.Width:0.#}x{frame.Height:0.#})")
        : "-";
}
