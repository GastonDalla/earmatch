using System.IO;

namespace Earmatch.App;

public static class AppPaths
{
    /// %APPDATA%\Earmatch, where Earmatch keeps its settings (UserDefaults on macOS).
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Earmatch");

    public static string SettingsFile => Path.Combine(Folder, "settings.json");

    /// The mix trainer's history, in the macOS app's format (practice.json in Application Support/Earmatch there).
    public static string PracticeFile => Path.Combine(Folder, "practice.json");
}
