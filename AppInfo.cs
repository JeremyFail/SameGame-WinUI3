using System.Reflection;

namespace SameGame;

/// <summary>
/// Application metadata read once at startup from assembly attributes.
/// </summary>
public static class AppInfo
{
    /// <summary>
    /// Gets the application version string set during <see cref="Initialize"/>.
    /// </summary>
    public static string Version { get; private set; } = "unknown";

    /// <summary>
    /// Reads version metadata from the executing assembly.
    /// </summary>
    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var raw = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        Version = FormatDisplayVersion(raw);
    }

    /// <summary>
    /// Strips SemVer build metadata (e.g. git commit hash after <c>+</c>) for user-facing display.
    /// </summary>
    private static string FormatDisplayVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unknown";
        }

        int plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }
}
