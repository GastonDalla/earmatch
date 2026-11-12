using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Earmatch.Core;
using Microsoft.Win32;

namespace Earmatch.App;

public static class SystemInfo
{
    public static string? ShortVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(version)) return null;
            var plus = version.IndexOf('+');
            return plus >= 0 ? version[..plus] : version;
        }
    }

    public static string? BuildNumber => Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "BuildNumber")?.Value is { Length: > 0 } build ? build : null;

    public static string? VersionWithBuild => ShortVersion is { } version
        ? BuildNumber is { } build ? $"{version} ({build})" : version
        : null;

    public static string WindowsVersion => RuntimeInformation.OSDescription.Trim();

    public static string Architecture
    {
        get
        {
            var process = RuntimeInformation.ProcessArchitecture;
            var machine = RuntimeInformation.OSArchitecture;
            return process == machine ? machine.ToString() : $"{process} on {machine}";
        }
    }

    public static string? HardwareModel
    {
        get
        {
            try
            {
                using var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                return Core.HardwareModel.Describe(bios?.GetValue("SystemManufacturer") as string, bios?.GetValue("SystemProductName") as string);
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }
    }

    public static IReadOnlyList<string> PreferredLanguages
    {
        get
        {
            try
            {
                var languages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
                if (languages.Count > 0) return [.. languages];
            }
            catch (Exception exception) when (exception is COMException or InvalidOperationException or TypeLoadException)
            {
            }
            return [System.Globalization.CultureInfo.CurrentUICulture.Name];
        }
    }
}
