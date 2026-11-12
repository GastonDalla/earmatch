using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Earmatch.Core;

namespace Earmatch.App.Rekordbox;

internal static class DumpUiaCommand
{
    public const string Flag = "--dump-uia";

    public static int Run()
    {
        using var error = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };
        using var process = RekordboxProcess.Find();
        if (process is null)
        {
            error.WriteLine("rekordbox is not running.");
            return 1;
        }
        var window = process.MainWindow();
        if (window == IntPtr.Zero)
        {
            error.WriteLine("The rekordbox main window was not found.");
            return 1;
        }
        var watch = Stopwatch.StartNew();
        UiaWindowReader.Read read;
        try
        {
            read = new UiaWindowReader().ReadWindow(window);
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or TimeoutException or COMException or InvalidOperationException
                                              or ArgumentException or FormatException)
        {
            error.WriteLine($"Reading rekordbox's window failed: {exception.Message}");
            return 1;
        }
        var elapsed = watch.Elapsed.TotalMilliseconds;
        var snapshot = read.Snapshot;
        var info = new UiaDumpInfo(DateTimeOffset.Now, SystemInfo.WindowsVersion, Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
            ?? RuntimeInformation.OSArchitecture.ToString().ToUpperInvariant(),
            snapshot.Scale, process.Id, process.Path, process.ProductVersion);
        using (var output = Console.OpenStandardOutput())
        {
            var bytes = Encoding.UTF8.GetBytes(UiaDump.Write(info, read.Nodes));
            output.Write(bytes, 0, bytes.Length);
        }

        var layout = RekordboxLayout.Locate(snapshot);
        var isFrontmost = NativeMethods.GetForegroundWindow() == window && !NativeMethods.IsIconic(window);
        var activation = L10n.English(() => OverlayActivation.Evaluate(isRunning: true, layout, isFrontmost).Message);
        error.WriteLine($"rekordbox {process.Version ?? "?"}: {snapshot.Elements.Count} elements read in {Number(elapsed, "F1")} ms, "
            + $"scale {Number(snapshot.Scale, "0.##")}");
        error.WriteLine($"mode={EnumRawValue.Of(layout.Mode)} layout={layout.LayoutName ?? "-"} active={layout.IsSupportedPerformanceLayout} "
            + $"waveform={Describe(layout.StackedWaveform)} activation=\"{activation}\"");
        foreach (var deck in layout.Decks)
        {
            error.WriteLine($"deck {deck.Number}: title={deck.Title ?? "-"} bpm={Optional(deck.BPM)} header={Describe(deck.HeaderBPMFrame)} "
                + $"jog={Describe(deck.JogBPMFrame)} pitch={Describe(deck.PitchFrame)} master={Describe(deck.MasterButtonFrame)} "
                + $"strip={Describe(deck.WaveformStrip)} markers={Describe(deck.Strip?.MarkerArea)} bars={deck.BarsText ?? "-"} "
                + $"elapsed={Optional(deck.ElapsedSeconds)} remaining={Optional(deck.RemainingSeconds)}");
        }
        return 0;
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Optional(double? value) => value is { } number ? number.ToString(CultureInfo.InvariantCulture) : "-";

    private static string Describe(ScreenRect? rect) => rect is { } frame
        ? $"({Number(frame.MinX, "0")},{Number(frame.MinY, "0")} {Number(frame.Width, "0")}x{Number(frame.Height, "0")})"
        : "-";
}
