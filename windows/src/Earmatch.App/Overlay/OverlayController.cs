using System.Windows.Media;
using Earmatch.App.Rekordbox;
using Earmatch.Core;

namespace Earmatch.App.Overlay;

/// Places the overlays over rekordbox (OverlayController.swift) from its UI Automation layout: the jog BPM covers of
/// the BPM limit, blind mode's pitch and header BPM covers, and Hide Both's waveform covers (OverlayCovers in
/// Earmatch.Core). Every overlay hides while Earmatch is off, rekordbox is not active in front, or Right Alt is held.
/// The pixel overlays (split waveform, phase meters, measured BPM digits) attach here once the window is captured.
internal sealed class OverlayController : IDisposable
{
    /// The cover colours of OverlayColor in LayerHostView.swift.
    private static readonly Color Black = Color.FromRgb(0, 0, 0);
    private static readonly Color HeaderGrey = Color.FromRgb(30, 30, 30);

    private readonly Dictionary<(OverlayCoverKind Kind, int Deck), OverlayWindow> _covers = [];

    public void Update(RekordboxState state, OverlaySettings settings, bool isPeeking)
    {
        if (!OverlayCovers.Shows(settings, state.Activation, isPeeking) || state.Layout is not { } layout)
        {
            HideAll();
            return;
        }
        var shown = new HashSet<(OverlayCoverKind, int)>();
        foreach (var cover in OverlayCovers.Plan(layout, settings, state.Scale))
        {
            var key = (cover.Kind, cover.Deck);
            if (!_covers.TryGetValue(key, out var window))
            {
                window = new OverlayWindow(cover.Kind == OverlayCoverKind.HeaderBPM ? HeaderGrey : Black);
                _covers[key] = window;
            }
            window.Place(cover.Frame, state.Offset);
            shown.Add(key);
        }
        foreach (var (key, window) in _covers)
        {
            if (!shown.Contains(key)) window.Hide();
        }
    }

    public void HideAll()
    {
        foreach (var window in _covers.Values) window.Hide();
    }

    public void Dispose()
    {
        foreach (var window in _covers.Values) window.Dispose();
        _covers.Clear();
    }
}
