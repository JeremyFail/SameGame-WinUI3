namespace SameGame.Model;

public static class BoardGenerator
{
    private const int MaxGenerationAttempts = 10;

    public static GeneratedBoard Generate(GameSettings settings, long seed)
    {
        var random = new Random((int)(seed ^ (seed >> 32)));
        long currentSeed = seed;

        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var board = RandomBoardBuilder.Build(settings, random);
            if (board.HasAnyMove() && !board.IsEmpty())
            {
                return new GeneratedBoard(board, currentSeed);
            }

            currentSeed = ((long)random.Next() << 32) | (uint)random.Next();
            random = new Random((int)(currentSeed ^ (currentSeed >> 32)));
        }

        throw new InvalidOperationException(
            $"Failed to generate a board after {MaxGenerationAttempts} attempts");
    }

    public sealed record GeneratedBoard(Board Board, long Seed);
}
