namespace SameGame.Model;

/// <summary>
/// Immutable high-score record ordered by score descending.
/// </summary>
public sealed class HighScoreEntry : IComparable<HighScoreEntry>
{
    public string Name { get; }
    public string Initials { get; }
    public int Score { get; }
    public int Width { get; }
    public int Height { get; }
    public int NumColors { get; }
    public string Date { get; }

    /// <summary>
    /// Creates a high-score entry with the current local date and time.
    /// </summary>
    /// <param name="name">Display name of the player.</param>
    /// <param name="initials">Player initials shown on the leaderboard.</param>
    /// <param name="score">Final score achieved.</param>
    /// <param name="width">Board width for the game.</param>
    /// <param name="height">Board height for the game.</param>
    /// <param name="numColors">Number of colors used in the game.</param>
    public HighScoreEntry(string name, string initials, int score, int width, int height, int numColors)
        : this(name, initials, score, width, height, numColors, DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
    {
    }

    /// <summary>
    /// Creates a high-score entry with an explicit date string.
    /// </summary>
    /// <param name="name">Display name of the player.</param>
    /// <param name="initials">Player initials shown on the leaderboard.</param>
    /// <param name="score">Final score achieved.</param>
    /// <param name="width">Board width for the game.</param>
    /// <param name="height">Board height for the game.</param>
    /// <param name="numColors">Number of colors used in the game.</param>
    /// <param name="date">Formatted date/time string for the entry.</param>
    public HighScoreEntry(string name, string initials, int score, int width, int height, int numColors, string date)
    {
        Name = name;
        Initials = initials;
        Score = score;
        Width = width;
        Height = height;
        NumColors = numColors;
        Date = date;
    }

    /// <summary>
    /// Returns a compact description of the board dimensions.
    /// </summary>
    /// <returns>A string in the form "width×height".</returns>
    public string BoardDescription() => $"{Width}×{Height}";

    /// <summary>
    /// Compares this entry to another for descending score order.
    /// </summary>
    /// <param name="other">The entry to compare against.</param>
    /// <returns>A negative value if this score is higher, zero if equal, or a positive value if lower.</returns>
    public int CompareTo(HighScoreEntry? other)
    {
        if (other is null)
        {
            return -1;
        }

        return other.Score.CompareTo(Score);
    }
}
