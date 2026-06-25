using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SameGame.I18n;

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

    public static string Get(string key) =>
        Bundle.TryGetValue(key, out var value) ? value : $"!{key}!";

    public static string Format(string key, params object[] args) =>
        FormatPrintf(Get(key), args);

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
            result.Append(format, lastIndex, match.Index - lastIndex);
            string specifier = match.Groups[3].Value;
            if (specifier == "%")
            {
                result.Append('%');
            }
            else
            {
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

        result.Append(format, lastIndex, format.Length - lastIndex);
        return result.ToString();
    }

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
            .Replace("\\u2022", "•");
    }
}
