namespace SameGame.Model;

/// <summary>
/// Computes SameGame move scores using Points(n) = n² - 3n + 4.
/// </summary>
public static class Scoring
{
    public static int PointsForGroup(int groupSize)
    {
        if (groupSize < 2)
        {
            return 0;
        }

        return groupSize * groupSize - 3 * groupSize + 4;
    }
}
