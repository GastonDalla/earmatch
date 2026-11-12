using System.IO;

namespace Earmatch.App;

public static class AppPaths
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Earmatch");

    public static string SettingsFile => Path.Combine(Folder, "settings.json");

    public static string PracticeFile => Path.Combine(Folder, "practice.json");
}
