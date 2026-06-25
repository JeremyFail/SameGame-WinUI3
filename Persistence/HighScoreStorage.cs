using System.Text.Json;
using SameGame.Model;

namespace SameGame.Persistence;

/// <summary>
/// Loads, saves, and manages the persisted high-score table stored as JSON on disk.
/// </summary>
public static class HighScoreStorage
{
    private const string FileName = "highscores.json";
    private const int MaxEntries = 10;

    /// <summary>
    /// Loads all persisted high-score entries from disk.
    /// </summary>
    /// <returns>
    /// High scores sorted by descending score, or an empty list when the file is missing,
    /// invalid, or cannot be read.
    /// </returns>
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

    /// <summary>
    /// Adds a new high-score entry and persists the updated table.
    /// </summary>
    /// <param name="entry">The completed high-score entry to insert.</param>
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

    /// <summary>
    /// Determines whether a score qualifies for the persisted high-score table.
    /// </summary>
    /// <param name="score">The score to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> when the table has fewer than the maximum entries or the score
    /// exceeds the lowest stored score; otherwise <see langword="false"/>.
    /// </returns>
    public static bool Qualifies(int score)
    {
        var entries = Load();
        if (entries.Count < MaxEntries)
        {
            return true;
        }

        return score > entries[^1].Score;
    }

    /// <summary>
    /// Deletes the persisted high-score file, if it exists.
    /// </summary>
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

    /// <summary>
    /// Serializes and writes the high-score table to disk.
    /// </summary>
    /// <param name="entries">The ordered high-score entries to persist.</param>
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

    /// <summary>
    /// Gets the folder path used for high-score persistence.
    /// </summary>
    /// <returns>The absolute path to the SameGame app-data folder.</returns>
    private static string GetFolderPath() => AppDataPaths.GetAppFolder();

    /// <summary>
    /// Gets the full path to the high-score JSON file.
    /// </summary>
    /// <returns>The absolute path to <c>highscores.json</c>.</returns>
    private static string GetFilePath() => AppDataPaths.GetFilePath(FileName);

    /// <summary>
    /// JSON-serializable data transfer object for a single high-score entry.
    /// </summary>
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
