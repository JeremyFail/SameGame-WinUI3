using System.Reflection;

namespace SameGame.I18n;

/// <summary>
/// Discovers available UI languages from embedded locale metadata.
/// </summary>
public static class LocaleCatalog
{
    /// <summary>
    /// Loads the map of supported language codes to display names.
    /// </summary>
    /// <returns>
    /// A read-only dictionary of language code to localized language name pairs.
    /// Falls back to English when the embedded catalog is missing or empty.
    /// </returns>
    public static IReadOnlyDictionary<string, string> AvailableLanguages()
    {
        var languages = new Dictionary<string, string>();
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SameGame.Assets.locales.languages.properties");
        if (stream is null)
        {
            languages["en"] = "English";
            return languages;
        }

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            languages[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }

        if (languages.Count == 0)
        {
            languages["en"] = "English";
        }

        return languages;
    }
}
