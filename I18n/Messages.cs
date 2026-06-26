using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SameGame.I18n;

/// <summary>
/// Loads localized message strings from embedded property bundles and formats printf-style placeholders.
/// </summary>
public static class Messages
{
    private static readonly Dictionary<string, string> Bundle = new();
    private static string _languageCode = "en";
    private static readonly Regex PrintfPattern = new(
        @"%(?:(\d+)\$)?(?:0?(\d+))?([dfs%])",
        RegexOptions.Compiled);

    static Messages()
    {
        SetLanguage("en");
    }

    /// <summary>
    /// Switches the active language and reloads the message bundle.
    /// </summary>
    /// <param name="code">The BCP 47-style language code to load; defaults to English when blank.</param>
    public static void SetLanguage(string code)
    {
        _languageCode = string.IsNullOrWhiteSpace(code) ? "en" : code;
        Bundle.Clear();
        LoadBundle(_languageCode);
        if (_languageCode != "en" && Bundle.Count == 0)
        {
            LoadBundle("en");
        }
    }

    public static string LanguageCode => _languageCode;

    /// <summary>
    /// Gets the localized string for a message key.
    /// </summary>
    /// <param name="key">The message key to look up.</param>
    /// <returns>The localized value, or a placeholder of the form <c>!key!</c> when missing.</returns>
    public static string Get(string key) =>
        Bundle.TryGetValue(key, out var value) ? value : $"!{key}!";

    /// <summary>
    /// Gets a localized string and substitutes printf-style arguments.
    /// </summary>
    /// <param name="key">The message key to look up.</param>
    /// <param name="args">Arguments referenced by the format string.</param>
    /// <returns>The formatted localized message.</returns>
    public static string Format(string key, params object[] args) =>
        FormatPrintf(Get(key), args);

    /// <summary>
    /// Formats a printf-style string using positional or sequential argument placeholders.
    /// </summary>
    /// <param name="format">The format string containing <c>%</c> specifiers.</param>
    /// <param name="args">Arguments referenced by the format string.</param>
    /// <returns>The formatted string, or the original format when no arguments are supplied.</returns>
    internal static string FormatPrintf(string format, params object[] args)
    {
        if (args.Length == 0)
        {
            return format;
        }

        var result = new StringBuilder(format.Length + 16);
        int lastIndex = 0;
        int autoArg = 0;

        foreach (Match match in PrintfPattern.Matches(format))
        {
            // Copy literal text before this placeholder.
            result.Append(format, lastIndex, match.Index - lastIndex);
            string specifier = match.Groups[3].Value;
            if (specifier == "%")
            {
                result.Append('%');
            }
            else
            {
                // Resolve positional (e.g. %2$s) or sequential argument index.
                int argIndex = match.Groups[1].Success
                    ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1
                    : autoArg++;
                if (argIndex < 0 || argIndex >= args.Length)
                {
                    result.Append(match.Value);
                    continue;
                }

                object value = args[argIndex];
                if (specifier == "s")
                {
                    result.Append(Convert.ToString(value, CultureInfo.CurrentCulture));
                }
                else if (specifier == "d")
                {
                    // Optional zero-padded width (e.g. %02d).
                    string width = match.Groups[2].Value;
                    if (width.Length > 0)
                    {
                        result.Append(Convert.ToInt32(value, CultureInfo.InvariantCulture)
                            .ToString(new string('0', int.Parse(width, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result.Append(Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            lastIndex = match.Index + match.Length;
        }

        // Append any trailing literal text after the last placeholder.
        result.Append(format, lastIndex, format.Length - lastIndex);
        return result.ToString();
    }

    /// <summary>
    /// Loads key/value message pairs for a language from an embedded properties resource.
    /// </summary>
    /// <param name="code">The language code used to select the embedded bundle file.</param>
    private static void LoadBundle(string code)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"SameGame.Assets.locales.messages_{code}.properties";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return;
        }

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            // Skip blank lines and comments.
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = Unescape(line[(eq + 1)..]);
            Bundle[key] = value;
        }
    }

    /// <summary>
    /// Expands common escape sequences used in property file values.
    /// </summary>
    /// <param name="value">The raw property value read from a bundle line.</param>
    /// <returns>The unescaped display value.</returns>
    private static string Unescape(string value)
    {
        return value
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\u2026", "…")
            .Replace("\\u00d7", "×")
            .Replace("\\u00b2", "²")
            .Replace("\\u2212", "−")
            .Replace("\\u2013", "–")
            .Replace("\\u2014", "—")
            .Replace("\\u201c", "\u201c")
            .Replace("\\u201d", "\u201d")
            .Replace("\\u2192", "→")
            .Replace("\\u00b7", "·")
            .Replace("\\u2022", "•");
    }
}
