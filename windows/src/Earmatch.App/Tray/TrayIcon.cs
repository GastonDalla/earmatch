using System.Drawing;
using System.Windows.Forms;
using Earmatch.App.Panes;
using Earmatch.Core;

namespace Earmatch.App.Tray;

/// The tray icon and its menu, the Windows counterpart of the macOS menu bar menu (MenuBarController.swift): the
/// status, every option, Open Earmatch…, Copy Diagnostics, About, the credit and Quit. Like the macOS menu bar icon
/// (waveform.circle.fill or waveform.circle), the icon is in full colour while the overlays run on rekordbox and
/// greyed out otherwise; its tooltip says the status.
public sealed class TrayIcon : IDisposable
{
    private const int MaxTooltipLength = 127;

    private readonly NotifyIcon _icon;
    private readonly Icon _activeIcon;
    private readonly Icon _inactiveIcon;
    private readonly IntPtr _inactiveHandle;
    private bool? _isActive;
    private string _tooltip = "Earmatch";
    private readonly ContextMenuStrip _menu = new() { Renderer = new DarkMenuRenderer(), ShowCheckMargin = true, ShowImageMargin = false };
    private readonly MainWindowModel _model;
    private readonly Action _open;
    private readonly Action _about;
    private readonly Func<bool> _copyDiagnostics;
    private readonly Action _quit;

    public TrayIcon(MainWindowModel model, Action open, Action about, Func<bool> copyDiagnostics, Action quit)
    {
        _model = model;
        _open = open;
        _about = about;
        _copyDiagnostics = copyDiagnostics;
        _quit = quit;
        _menu.Opening += (_, _) => Rebuild();
        _menu.ForeColor = MenuColors.Text;
        _menu.BackColor = MenuColors.Background;
        _activeIcon = AppIcon.TrayIcon(System.Windows.Forms.SystemInformation.SmallIconSize.Width);
        (_inactiveIcon, _inactiveHandle) = Greyed(_activeIcon);
        _icon = new NotifyIcon
        {
            Icon = _inactiveIcon,
            Text = _tooltip,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _open();
        };
        Rebuild();
    }

    /// Shows whether the overlays run on rekordbox, and the status in the tooltip.
    public void SetStatus(bool isActive, string status)
    {
        if (isActive != _isActive)
        {
            _isActive = isActive;
            _icon.Icon = isActive ? _activeIcon : _inactiveIcon;
        }
        var tooltip = $"Earmatch · {status}";
        if (tooltip.Length > MaxTooltipLength) tooltip = tooltip[..(MaxTooltipLength - 1)] + "…";
        if (tooltip == _tooltip) return;
        _tooltip = tooltip;
        _icon.Text = tooltip;
    }

    /// The icon in grey at 55% opacity, and the icon handle to destroy with it.
    private static (Icon Icon, IntPtr Handle) Greyed(Icon icon)
    {
        using var source = icon.ToBitmap();
        using var greyed = new Bitmap(source.Width, source.Height);
        using (var graphics = Graphics.FromImage(greyed))
        {
            float[][] matrix =
            [
                [0.3f, 0.3f, 0.3f, 0, 0],
                [0.59f, 0.59f, 0.59f, 0, 0],
                [0.11f, 0.11f, 0.11f, 0, 0],
                [0, 0, 0, 0.55f, 0],
                [0, 0, 0, 0, 1],
            ];
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(matrix));
            graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }
        var handle = greyed.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    private void Rebuild()
    {
        var items = _menu.Items;
        DisposeAll(items);
        var settings = _model.Settings;
        items.Add(Note(_model.Activation.Message));
        items.Add(new ToolStripSeparator());
        items.Add(Item(WindowText.Enable, settings.IsEnabled, (ref OverlaySettings s) => s.IsEnabled = !s.IsEnabled));
        items.Add(new ToolStripSeparator());
        items.Add(Submenu(OptionText.BoothMenuTitle, BoothPreset.AllCases.Select(preset => (preset.DisplayName, (BoothPreset?)preset)).ToList(),
            BoothPreset.Selected(settings), (ref OverlaySettings s, BoothPreset? preset) => preset?.Apply(ref s)));
        items.Add(Item(MenuText.LimitBPM, settings.IsBPMLimitEnabled, (ref OverlaySettings s) => s.IsBPMLimitEnabled = !s.IsBPMLimitEnabled));
        items.Add(Submenu(MenuText.BpmPrecision,
            [(MenuText.OneDecimal, BPMLimit.OneDecimal), (MenuText.NoDecimals, BPMLimit.NoDecimals), (MenuText.CoverFully, BPMLimit.CoverFully)],
            settings.BPMLimit, (ref OverlaySettings s, BPMLimit value) => s.BPMLimit = value));
        items.Add(Item(OptionText.BlindModeMenuTitle, settings.IsBlindModeEnabled, (ref OverlaySettings s) => s.IsBlindModeEnabled = !s.IsBlindModeEnabled));
        items.Add(Submenu(WindowText.Waveform, WaveformMode.AllCases.Select(mode => (mode.DisplayName, mode)).ToList(), settings.WaveformMode,
            (ref OverlaySettings s, WaveformMode value) => s.WaveformMode = value));
        items.Add(Item(MenuText.PhaseMeter, settings.IsPhaseMeterEnabled, (ref OverlaySettings s) => s.IsPhaseMeterEnabled = !s.IsPhaseMeterEnabled));
        items.Add(Submenu(OptionText.PhaseMeterStyleMenuTitle, PhaseMeterType.AllCases.Select(type => (type.Title, type)).ToList(), settings.PhaseMeterType,
            (ref OverlaySettings s, PhaseMeterType value) => s.PhaseMeterType = value));
        items.Add(Submenu(OptionText.CounterMenuTitle, CounterStyle.AllCases.Select(style => (style.Title, style)).ToList(), settings.CounterStyle,
            (ref OverlaySettings s, CounterStyle value) => s.CounterStyle = value));
        items.Add(Item(OptionText.MixTrainerMenuTitle, settings.IsMixTrainerEnabled, (ref OverlaySettings s) => s.IsMixTrainerEnabled = !s.IsMixTrainerEnabled));
        items.Add(new ToolStripSeparator());
        items.Add(Action(MenuText.Open, _open));
        items.Add(Action(WindowText.CopyDiagnostics, CopyDiagnostics));
        items.Add(Note(MenuText.PeekNote));
        items.Add(new ToolStripSeparator());
        items.Add(Action(MenuText.About, _about));
        items.Add(Action(Credit.MenuLine, SetupPane.OpenProfile));
        items.Add(Note(MenuText.Independence));
        items.Add(Action(WindowText.Quit, _quit));
    }

    /// The menu is rebuilt each time it opens; the items it replaces, their submenus and the submenus' items are
    /// disposed so their window handles and fonts do not pile up.
    private static void DisposeAll(ToolStripItemCollection items)
    {
        var removed = items.Cast<ToolStripItem>().ToList();
        items.Clear();
        foreach (var item in removed)
        {
            if (item is ToolStripDropDownItem { HasDropDownItems: true } parent)
            {
                DisposeAll(parent.DropDownItems);
                parent.DropDown.Dispose();
            }
            item.Dispose();
        }
    }

    /// The tray has no "Copied" to show, so it speaks up only when the copy failed.
    private void CopyDiagnostics()
    {
        if (_copyDiagnostics()) return;
        _icon.ShowBalloonTip(5000, "Earmatch", WindowText.CopyFailedNotice, ToolTipIcon.Warning);
    }

    private static ToolStripMenuItem Note(string title) => new(title) { Enabled = false };

    private static ToolStripMenuItem Action(string title, Action action)
    {
        var item = new ToolStripMenuItem(title);
        item.Click += (_, _) => action();
        return item;
    }

    private ToolStripMenuItem Item(string title, bool isChecked, SettingsChange change)
    {
        var item = new ToolStripMenuItem(title) { Checked = isChecked };
        item.Click += (_, _) => _model.Update(change);
        return item;
    }

    private delegate void OptionChange<T>(ref OverlaySettings settings, T value);

    private ToolStripMenuItem Submenu<T>(string title, IReadOnlyList<(string Label, T Value)> options, T selected, OptionChange<T> apply)
    {
        var parent = new ToolStripMenuItem(title);
        parent.DropDown.BackColor = MenuColors.Background;
        parent.DropDown.ForeColor = MenuColors.Text;
        parent.DropDown.Renderer = _menu.Renderer;
        foreach (var (label, value) in options)
        {
            var option = value;
            parent.DropDownItems.Add(Item(label, EqualityComparer<T>.Default.Equals(value, selected), (ref OverlaySettings s) => apply(ref s, option)));
        }
        return parent;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _activeIcon.Dispose();
        _inactiveIcon.Dispose();
        NativeMethods.DestroyIcon(_inactiveHandle);
    }
}

internal static class MenuColors
{
    public static readonly Color Background = Color.FromArgb(37, 37, 40);
    public static readonly Color Hover = Color.FromArgb(58, 64, 40);
    public static readonly Color Text = Color.FromArgb(235, 235, 235);
    public static readonly Color Disabled = Color.FromArgb(140, 140, 140);
    public static readonly Color Separator = Color.FromArgb(60, 60, 64);
    public static readonly Color Accent = Color.FromArgb(184, 242, 59);
}

/// Draws the tray menu in the window's dark colours with lime check marks.
internal sealed class DarkMenuRenderer() : ToolStripProfessionalRenderer(new DarkColors())
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? MenuColors.Text : MenuColors.Disabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = MenuColors.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var box = e.ImageRectangle;
        using var pen = new Pen(MenuColors.Accent, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var x = box.Left + box.Width / 2f;
        var y = box.Top + box.Height / 2f;
        graphics.DrawLines(pen, [new PointF(x - 5, y), new PointF(x - 1.5f, y + 3.5f), new PointF(x + 5, y - 4)]);
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => MenuColors.Background;
        public override Color ImageMarginGradientBegin => MenuColors.Background;
        public override Color ImageMarginGradientMiddle => MenuColors.Background;
        public override Color ImageMarginGradientEnd => MenuColors.Background;
        public override Color MenuBorder => MenuColors.Separator;
        public override Color MenuItemBorder => MenuColors.Hover;
        public override Color MenuItemSelected => MenuColors.Hover;
        public override Color MenuItemSelectedGradientBegin => MenuColors.Hover;
        public override Color MenuItemSelectedGradientEnd => MenuColors.Hover;
        public override Color SeparatorDark => MenuColors.Separator;
        public override Color SeparatorLight => MenuColors.Separator;
        public override Color CheckBackground => MenuColors.Background;
        public override Color CheckSelectedBackground => MenuColors.Hover;
        public override Color CheckPressedBackground => MenuColors.Hover;
    }
}
