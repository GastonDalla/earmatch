using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Earmatch.App.Capture;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

internal sealed class WaveformPanelView : Canvas
{
    private readonly Dictionary<int, DeckPane> _panes = [];
    private readonly List<Rectangle> _dividers = [];
    private WaveformOverlayPlan? _plan;
    private IReadOnlyDictionary<int, ScreenRect> _strips = new Dictionary<int, ScreenRect>();
    private double _scale = 1;
    private CapturedFrame? _shownFrame;
    private int? _shownMaster;

    public WaveformPanelView()
    {
        ClipToBounds = true;
        Background = OverlayColors.Black;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public void Configure(WaveformOverlayPlan plan, IReadOnlyDictionary<int, ScreenRect> strips, double scale)
    {
        if (Equals(plan, _plan) && scale == _scale && strips.Count == _strips.Count && strips.All(entry => _strips.TryGetValue(entry.Key, out var strip) && strip == entry.Value))
            return;
        _plan = plan;
        _strips = strips;
        _scale = scale;
        if (plan.Panel is { } panel)
        {
            Width = panel.Width / scale;
            Height = panel.Height / scale;
        }
        while (_dividers.Count < plan.Dividers.Count)
        {
            var divider = new Rectangle { Fill = OverlayColors.Divider };
            Children.Add(divider);
            _dividers.Add(divider);
        }
        for (var index = 0; index < _dividers.Count; index++)
        {
            var divider = _dividers[index];
            divider.Visibility = index < plan.Dividers.Count ? Visibility.Visible : Visibility.Collapsed;
            if (index < plan.Dividers.Count) Place(divider, plan.Dividers[index]);
        }
        var decks = plan.Panes.Select(pane => pane.Deck).ToHashSet();
        foreach (var number in _panes.Keys.Where(number => !decks.Contains(number)).ToList())
        {
            Children.Remove(_panes[number]);
            _panes.Remove(number);
        }
        foreach (var spec in plan.Panes)
        {
            if (!_panes.TryGetValue(spec.Deck, out var pane))
            {
                pane = new DeckPane();
                _panes[spec.Deck] = pane;
                Children.Insert(0, pane);
            }
            Place(pane, spec.Frame);
            pane.Layout(spec, plan.AllowsMasterWaveform, scale);
        }
    }

    public void ShowFrame(CapturedFrame? frame, int? master)
    {
        if (_plan is not { } plan) return;
        var showsWaveforms = plan.AllowsMasterWaveform;
        _shownFrame = showsWaveforms ? frame : null;
        _shownMaster = master;
        ScreenRect? masterStrip = master is { } number && _strips.TryGetValue(number, out var strip) ? strip : null;
        foreach (var spec in plan.Panes)
        {
            if (!_panes.TryGetValue(spec.Deck, out var pane)) continue;
            if (showsWaveforms && frame is not null)
            {
                pane.ShowWaveform(frame, spec.Source);
                pane.ShowMasterWaveform(master == spec.Deck ? null : frame, masterStrip, _scale);
            }
            else
            {
                pane.ShowWaveform(null, default);
                pane.ShowMasterWaveform(null, null, _scale);
            }
        }
    }

    public void ShowMeter(PhaseMeterDisplay? display, int deck)
    {
        if (!_panes.TryGetValue(deck, out var pane)) return;
        pane.Meter.Display = pane.ShowsMeter ? display : null;
        if (pane.Meter.IsWaitingForMasterWaveform && _shownMaster is { } master && master != deck && _strips.TryGetValue(master, out var strip))
            pane.ShowMasterWaveform(_shownFrame, strip, _scale);
    }

    private void Place(FrameworkElement element, ScreenRect frame)
    {
        SetLeft(element, frame.X / _scale);
        SetTop(element, frame.Y / _scale);
        element.Width = frame.Width / _scale;
        element.Height = frame.Height / _scale;
    }

    private sealed class DeckPane : Grid
    {
        private readonly Image _waveform = new() { Stretch = Stretch.Fill, Visibility = Visibility.Collapsed };
        private readonly PixelCopy _waveformPixels = new();
        private readonly PixelCopy _bandPixels = new();
        private double _waveformHeight;

        public DeckPane()
        {
            Background = OverlayColors.Black;
            ClipToBounds = true;
            _waveform.VerticalAlignment = VerticalAlignment.Bottom;
            Children.Add(_waveform);
            Children.Add(Meter);
        }

        public MeterView Meter { get; } = new();

        public bool ShowsMeter { get; private set; } = true;

        public void Layout(WaveformOverlayPane spec, bool showsWaveforms, double scale)
        {
            ShowsMeter = spec.ShowsMeter;
            _waveformHeight = spec.WaveformHeight / scale;
            _waveform.Height = _waveformHeight;
            var height = spec.Frame.Height / scale;
            Meter.MeterBounds = new Rect(0, 0, spec.Frame.Width / scale, Math.Max(0, height - _waveformHeight));
            Meter.PlayheadExtension = _waveformHeight;
            Meter.AllowsMasterWaveform = showsWaveforms;
            if (!spec.ShowsMeter) Meter.Display = null;
            Meter.Refresh();
        }

        public void ShowWaveform(CapturedFrame? frame, ScreenRect source)
        {
            if (frame is null || _waveformHeight <= 0)
            {
                _waveform.Visibility = Visibility.Collapsed;
                return;
            }
            var image = _waveformPixels.From(frame, source);
            _waveform.Source = image;
            _waveform.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        }

        public void ShowMasterWaveform(CapturedFrame? frame, ScreenRect? strip, double scale)
        {
            if (frame is null || strip is not { } master || Meter.BandRect is not { } band)
            {
                Meter.BandImage = null;
                return;
            }
            var width = band.Width * scale;
            var source = new ScreenRect(master.MidX - width / 2, master.MinY, width, master.Height);
            Meter.BandImage = _bandPixels.From(frame, source);
            Meter.Refresh();
        }
    }
}

internal sealed class PixelCopy
{
    private WriteableBitmap? _bitmap;

    public WriteableBitmap? From(CapturedFrame frame, ScreenRect source)
    {
        var rect = frame.PixelRect(source);
        if (rect.Width <= 0 || rect.Height <= 0) return null;
        if (_bitmap is null || _bitmap.PixelWidth != rect.Width || _bitmap.PixelHeight != rect.Height)
            _bitmap = new WriteableBitmap(rect.Width, rect.Height, 96, 96, PixelFormats.Bgr32, null);
        var pixels = frame.Pixels;
        _bitmap.WritePixels(new Int32Rect(0, 0, rect.Width, rect.Height), pixels.Bytes, pixels.Stride, rect.Y * pixels.Stride + rect.X * 4);
        return _bitmap;
    }
}
