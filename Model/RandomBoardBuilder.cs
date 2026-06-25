namespace SameGame.Model;

/// <summary>
/// Builds randomized boards tuned by generation difficulty and randomness settings.
/// </summary>
public static class RandomBoardBuilder
{
    private const int MaxAttempts = 60;

    /// <summary>
    /// Creates a filled and shuffled board that preferably has at least one valid move.
    /// </summary>
    /// <param name="settings">Game configuration controlling size, colors, difficulty, and randomness.</param>
    /// <param name="random">Random number generator used for placement and shuffling.</param>
    /// <returns>A board populated according to the current settings.</returns>
    public static Board Build(GameSettings settings, Random random)
    {
        var difficulty = settings.GenerationDifficultyValue;
        int width = settings.BoardWidth();
        int height = settings.BoardHeight();
        int numColors = settings.NumColors;
        int swaps = SwapCount(settings, difficulty);

        // Retry fill + shuffle until a playable board is found
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var board = FillBoard(width, height, numColors, random, difficulty, settings);
            ShuffleBoard(board, random, swaps);
            if (board.HasAnyMove())
            {
                return board;
            }
        }

        // Return the last attempt even if no move exists (caller may regenerate)
        var fallback = FillBoard(width, height, numColors, random, difficulty, settings);
        ShuffleBoard(fallback, random, swaps);
        return fallback;
    }

    /// <summary>
    /// Fills every cell using difficulty-aware color selection.
    /// </summary>
    /// <param name="width">Board width in cells.</param>
    /// <param name="height">Board height in cells.</param>
    /// <param name="numColors">Number of distinct tile colors.</param>
    /// <param name="random">Random number generator for color picks.</param>
    /// <param name="difficulty">Generation difficulty influencing neighbor matching.</param>
    /// <param name="settings">Full settings, used for randomness scaling.</param>
    /// <returns>A fully populated board before shuffling.</returns>
    private static Board FillBoard(
        int width, int height, int numColors, Random random,
        GameSettings.GenerationDifficulty difficulty, GameSettings settings)
    {
        var board = new Board(width, height, numColors);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                board.Set(x, y, PickColor(board, x, y, numColors, random, difficulty, settings));
            }
        }

        return board;
    }

    /// <summary>
    /// Chooses a color for a cell based on already-placed neighbors and difficulty rules.
    /// </summary>
    /// <param name="board">The board being filled.</param>
    /// <param name="x">Column index of the cell.</param>
    /// <param name="y">Row index of the cell.</param>
    /// <param name="numColors">Number of distinct tile colors.</param>
    /// <param name="random">Random number generator for color picks.</param>
    /// <param name="difficulty">Generation difficulty influencing neighbor matching.</param>
    /// <param name="settings">Full settings, used for randomness scaling.</param>
    /// <returns>A color index for the cell.</returns>
    private static int PickColor(
        Board board, int x, int y, int numColors, Random random,
        GameSettings.GenerationDifficulty difficulty, GameSettings settings)
    {
        var neighborColors = NeighborColors(board, x, y);
        if (neighborColors.Count == 0)
        {
            return random.Next(numColors);
        }

        return difficulty switch
        {
            GameSettings.GenerationDifficulty.Easy => PickEasyColor(neighborColors, numColors, random, settings),
            GameSettings.GenerationDifficulty.Hard => PickHardColor(neighborColors, numColors, random, settings),
            _ => random.Next(numColors)
        };
    }

    /// <summary>
    /// Collects colors from already-filled left and top neighbors during row-major fill.
    /// </summary>
    /// <param name="board">The board being filled.</param>
    /// <param name="x">Column index of the cell.</param>
    /// <param name="y">Row index of the cell.</param>
    /// <returns>Colors of adjacent filled cells, if any.</returns>
    private static List<int> NeighborColors(Board board, int x, int y)
    {
        var neighbors = new List<int>(2);
        if (x > 0)
        {
            neighbors.Add(board.Get(x - 1, y));
        }

        if (y > 0)
        {
            neighbors.Add(board.Get(x, y - 1));
        }

        return neighbors;
    }

    /// <summary>
    /// Picks a color that often matches a neighbor to create larger groups on easy boards.
    /// </summary>
    /// <param name="neighborColors">Colors of adjacent already-filled cells.</param>
    /// <param name="numColors">Number of distinct tile colors.</param>
    /// <param name="random">Random number generator for color picks.</param>
    /// <param name="settings">Full settings, used for randomness scaling.</param>
    /// <returns>A color index for the cell.</returns>
    private static int PickEasyColor(List<int> neighborColors, int numColors, Random random, GameSettings settings)
    {
        if (random.NextDouble() < EasyNeighborMatchChance(settings))
        {
            return neighborColors[random.Next(neighborColors.Count)];
        }

        return random.Next(numColors);
    }

    /// <summary>
    /// Picks a color that often differs from neighbors to reduce groups on hard boards.
    /// </summary>
    /// <param name="neighborColors">Colors of adjacent already-filled cells.</param>
    /// <param name="numColors">Number of distinct tile colors.</param>
    /// <param name="random">Random number generator for color picks.</param>
    /// <param name="settings">Full settings, used for randomness scaling.</param>
    /// <returns>A color index for the cell.</returns>
    private static int PickHardColor(List<int> neighborColors, int numColors, Random random, GameSettings settings)
    {
        if (random.NextDouble() < HardNeighborAvoidChance(settings))
        {
            var alternatives = new List<int>();
            for (int color = 0; color < numColors; color++)
            {
                if (!neighborColors.Contains(color))
                {
                    alternatives.Add(color);
                }
            }

            if (alternatives.Count > 0)
            {
                return alternatives[random.Next(alternatives.Count)];
            }
        }

        return random.Next(numColors);
    }

    /// <summary>
    /// Returns the probability of matching a neighbor color on easy difficulty.
    /// </summary>
    /// <param name="settings">Full settings, used for randomness scaling.</param>
    /// <returns>A value between 0 and 1.</returns>
    private static double EasyNeighborMatchChance(GameSettings settings) =>
        0.50 + settings.Randomness * 0.0024;

    /// <summary>
    /// Returns the probability of avoiding neighbor colors on hard difficulty.
    /// </summary>
    /// <param name="settings">Full settings, used for randomness scaling.</param>
    /// <returns>A value between 0 and 1.</returns>
    private static double HardNeighborAvoidChance(GameSettings settings) =>
        0.38 + settings.Randomness * 0.0024;

    /// <summary>
    /// Randomly swaps cell pairs to break up initial grouping patterns.
    /// </summary>
    /// <param name="board">The board to shuffle in place.</param>
    /// <param name="random">Random number generator for swap selection.</param>
    /// <param name="swaps">Number of pairwise swaps to perform.</param>
    private static void ShuffleBoard(Board board, Random random, int swaps)
    {
        int width = board.Width;
        int height = board.Height;
        for (int i = 0; i < swaps; i++)
        {
            int x1 = random.Next(width);
            int y1 = random.Next(height);
            int x2 = random.Next(width);
            int y2 = random.Next(height);
            int temp = board.Get(x1, y1);
            board.Set(x1, y1, board.Get(x2, y2));
            board.Set(x2, y2, temp);
        }
    }

    /// <summary>
    /// Calculates how many random swaps to apply based on board size, difficulty, and randomness.
    /// </summary>
    /// <param name="settings">Game configuration providing board dimensions and randomness.</param>
    /// <param name="difficulty">Generation difficulty influencing the base swap rate.</param>
    /// <returns>The number of swaps to perform during shuffling.</returns>
    private static int SwapCount(GameSettings settings, GameSettings.GenerationDifficulty difficulty)
    {
        int cells = settings.BoardWidth() * settings.BoardHeight();
        double baseRate = difficulty switch
        {
            GameSettings.GenerationDifficulty.Easy => 0.06,
            GameSettings.GenerationDifficulty.Medium => 0.30,
            GameSettings.GenerationDifficulty.Hard => 0.45,
            _ => 0.30
        };
        double randomnessScale = 0.55 + settings.Randomness / 100.0;
        return Math.Max(2, (int)(cells * baseRate * randomnessScale));
    }
}
