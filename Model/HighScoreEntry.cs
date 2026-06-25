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

    public HighScoreEntry(string name, string initials, int score, int width, int height, int numColors)
        : this(name, initials, score, width, height, numColors, DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
    {
    }

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

    public string BoardDescription() => $"{Width}×{Height}";

    public int CompareTo(HighScoreEntry? other)
    {
        if (other is null)
        {
            return -1;
        }

        return other.Score.CompareTo(Score);
    }
}
