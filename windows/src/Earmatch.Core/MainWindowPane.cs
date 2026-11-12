namespace Earmatch.Core;

public enum MainWindowPane
{
    Live,
    Overlay,
    Practice,
    Setup,
}

public static class MainWindowPaneMembers
{
    extension(MainWindowPane pane)
    {
        public string RawValue => EnumRawValue.Of(pane);

        public string Title => pane switch
        {
            MainWindowPane.Live => L10n.Text("Live"),
            MainWindowPane.Overlay => L10n.Text("Overlay"),
            MainWindowPane.Practice => L10n.Text("Practice"),
            _ => L10n.Text("Setup"),
        };

        public int ShortcutNumber => (int)pane + 1;

        public string KeyEquivalent => pane.ShortcutNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public MainWindowPane Moved(int offset)
        {
            var count = Enum.GetValues<MainWindowPane>().Length;
            return (MainWindowPane)Math.Clamp((int)pane + offset, 0, count - 1);
        }
    }

    extension(MainWindowPane)
    {
        public static IReadOnlyList<MainWindowPane> AllCases => Enum.GetValues<MainWindowPane>();

        public static string StorageKey => "mainWindowPane";

        public static MainWindowPane? FromKeyEquivalent(string key)
        {
            if (key.Length != 1 || key[0] < '1' || key[0] > '9') return null;
            var index = key[0] - '1';
            return index < MainWindowPane.AllCases.Count ? MainWindowPane.AllCases[index] : null;
        }

        public static MainWindowPane Initial(string? stored) => EnumRawValue.Parse<MainWindowPane>(stored) ?? MainWindowPane.Live;
    }
}

public enum MainWindowStatus
{
    Live,
    Off,
    Waiting,
}

public static class MainWindowStatusMembers
{
    extension(MainWindowStatus status)
    {
        public StatusTone Tone => status switch
        {
            MainWindowStatus.Live => StatusTone.Live,
            MainWindowStatus.Off => StatusTone.Off,
            _ => StatusTone.Waiting,
        };

        public string Title => status switch
        {
            MainWindowStatus.Live => L10n.Text("Live on rekordbox"),
            MainWindowStatus.Off => L10n.Text("Earmatch is off"),
            _ => L10n.Text("Waiting for rekordbox"),
        };
    }

    extension(MainWindowStatus)
    {
        public static MainWindowStatus From(bool isEnabled, OverlayActivation activation) =>
            !isEnabled ? MainWindowStatus.Off : activation.IsActive ? MainWindowStatus.Live : MainWindowStatus.Waiting;
    }
}
