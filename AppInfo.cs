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
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
