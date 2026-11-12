using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Earmatch.Core.Tests;

/// Earmatch for Windows and for macOS ship as one release (v<version> on GitHub), so they carry one version.
public class AppVersionTests
{
    static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));

    static string MacOSVersion()
    {
        var keys = XDocument.Load(Path.Combine(RepositoryRoot(), "Support", "Info.plist")).Root!.Element("dict")!.Elements().ToList();
        var key = keys.FindIndex(element => element.Name == "key" && element.Value == "CFBundleShortVersionString");
        Assert.True(key >= 0, "Info.plist has no CFBundleShortVersionString");
        return keys[key + 1].Value;
    }

    static string? WindowsVersion() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "windows", "Directory.Build.props")).Descendants("Version").SingleOrDefault()?.Value;

    [Fact]
    public void TheWindowsAppHasTheVersionOfTheMacOSApp()
    {
        Assert.Equal(MacOSVersion(), WindowsVersion());
    }

    [Fact]
    public void NoProjectSetsAVersionOfItsOwn()
    {
        var projects = Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "windows"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .ToList();
        Assert.NotEmpty(projects);
        foreach (var project in projects)
            Assert.Empty(XDocument.Load(project).Descendants("Version"));
    }
}
