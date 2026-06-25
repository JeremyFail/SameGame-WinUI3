namespace SameGame.Model;

/// <summary>
/// Computes SameGame move scores using Points(n) = n² - 3n + 4.
/// </summary>
public static class Scoring
{
    /// <summary>
    /// Calculates the points awarded for removing a group of the given size.
    /// </summary>
    /// <param name="groupSize">Number of blocks in the removed group.</param>
    /// <returns>Points earned, or zero when <paramref name="groupSize"/> is less than two.</returns>
    public static int PointsForGroup(int groupSize)
    {
        if (groupSize < 2)
        {
            return 0;
        }

        return groupSize * groupSize - 3 * groupSize + 4;
    }
}
