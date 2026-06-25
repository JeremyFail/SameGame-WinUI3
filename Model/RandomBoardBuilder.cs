namespace SameGame.Model;

public static class RandomBoardBuilder
{
    private const int MaxAttempts = 60;

    public static Board Build(GameSettings settings, Random random)
    {
        var difficulty = settings.GenerationDifficultyValue;
        int width = settings.BoardWidth();
        int height = settings.BoardHeight();
        int numColors = settings.NumColors;
        int swaps = SwapCount(settings, difficulty);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var board = FillBoard(width, height, numColors, random, difficulty, settings);
            ShuffleBoard(board, random, swaps);
            if (board.HasAnyMove())
            {
                return board;
            }
        }

        var fallback = FillBoard(width, height, numColors, random, difficulty, settings);
        ShuffleBoard(fallback, random, swaps);
        return fallback;
    }

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

    private static int PickEasyColor(List<int> neighborColors, int numColors, Random random, GameSettings settings)
    {
        if (random.NextDouble() < EasyNeighborMatchChance(settings))
        {
            return neighborColors[random.Next(neighborColors.Count)];
        }

        return random.Next(numColors);
    }

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

    private static double EasyNeighborMatchChance(GameSettings settings) =>
        0.50 + settings.Randomness * 0.0024;

    private static double HardNeighborAvoidChance(GameSettings settings) =>
        0.38 + settings.Randomness * 0.0024;

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
