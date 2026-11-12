using System.Reflection;
using System.Runtime.InteropServices;

namespace Earmatch.App;

public static class SystemInfo
{
    /// The app version ("0.1.2"), or null for a development build without one.
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

    /// "Microsoft Windows 10.0.26100": Windows 11 keeps the 10.0 version, its build tells them apart.
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
}
