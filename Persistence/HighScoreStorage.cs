using System.Text.Json;
using SameGame.Model;

namespace SameGame.Persistence;

public static class HighScoreStorage
{
    private const string FileName = "highscores.json";
    private const int MaxEntries = 10;

    public static List<HighScoreEntry> Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return [];
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<HighScoreEntryDto>>(json);
            if (entries is null)
            {
                return [];
            }

            return entries
                .Select(e => new HighScoreEntry(e.Name, e.Initials, e.Score, e.Width, e.Height, e.NumColors, e.Date))
                .OrderByDescending(e => e.Score)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static void Add(HighScoreEntry entry)
    {
        var entries = Load();
        entries.Add(entry);
        entries.Sort();
        if (entries.Count > MaxEntries)
        {
            entries = entries.Take(MaxEntries).ToList();
        }

        Save(entries);
    }

    public static bool Qualifies(int score)
    {
        var entries = Load();
        if (entries.Count < MaxEntries)
        {
            return true;
        }

        return score > entries[^1].Score;
    }

    public static void Clear()
    {
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    private static void Save(List<HighScoreEntry> entries)
    {
        try
        {
            var dto = entries.Select(e => new HighScoreEntryDto
            {
                Name = e.Name,
                Initials = e.Initials,
                Score = e.Score,
                Width = e.Width,
                Height = e.Height,
                NumColors = e.NumColors,
                Date = e.Date
            }).ToList();

            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(GetFolderPath());
            File.WriteAllText(GetFilePath(), json);
        }
        catch
        {
            // Best-effort.
        }
    }

    private static string GetFolderPath() => AppDataPaths.GetAppFolder();

    private static string GetFilePath() => AppDataPaths.GetFilePath(FileName);

    private sealed class HighScoreEntryDto
    {
        public string Name { get; set; } = "";
        public string Initials { get; set; } = "";
        public int Score { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int NumColors { get; set; }
        public string Date { get; set; } = "";
    }
}
