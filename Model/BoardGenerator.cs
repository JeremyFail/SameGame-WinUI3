namespace SameGame.Model;

/// <summary>
/// Generates playable SameGame boards from settings and seeds, retrying when needed.
/// </summary>
public static class BoardGenerator
{
    private const int MaxGenerationAttempts = 10;

    /// <summary>
    /// Builds a non-empty board that has at least one valid move.
    /// </summary>
    /// <param name="settings">Game configuration controlling board size and generation difficulty.</param>
    /// <param name="seed">Seed used for deterministic random board construction.</param>
    /// <returns>A generated board and the seed that produced it.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no playable board is produced within the attempt limit.</exception>
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

            // Derive a new seed and RNG for the next attempt
            currentSeed = ((long)random.Next() << 32) | (uint)random.Next();
            random = new Random((int)(currentSeed ^ (currentSeed >> 32)));
        }

        throw new InvalidOperationException(
            $"Failed to generate a board after {MaxGenerationAttempts} attempts");
    }

    /// <summary>
    /// Result of successful board generation, including the seed used for replay.
    /// </summary>
    /// <param name="Board">The generated playable board.</param>
    /// <param name="Seed">The seed associated with this board.</param>
    public sealed record GeneratedBoard(Board Board, long Seed);
}
