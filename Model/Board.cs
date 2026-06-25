namespace SameGame.Model;

/// <summary>
/// Mutable game board for SameGame. Empty cells use <see cref="Empty"/> (-1).
/// </summary>
public sealed class Board
{
    public const int Empty = -1;

    private readonly int _width;
    private readonly int _height;
    private readonly int _numColors;
    private readonly int[] _cells;

    public Board(int width, int height, int numColors)
    {
        _width = width;
        _height = height;
        _numColors = numColors;
        _cells = new int[width * height];
        Array.Fill(_cells, Empty);
    }

    public Board(int width, int height, int numColors, int[] cells)
    {
        _width = width;
        _height = height;
        _numColors = numColors;
        _cells = (int[])cells.Clone();
    }

    public int Width => _width;
    public int Height => _height;
    public int NumColors => _numColors;

    public int Get(int x, int y) => _cells[Index(x, y)];

    public void Set(int x, int y, int value) => _cells[Index(x, y)] = value;

    public int[] CellsCopy() => (int[])_cells.Clone();

    public bool IsEmpty() => _cells.All(c => c == Empty);

    public int RemainingBlockCount() => _cells.Count(c => c != Empty);

    public int[] ColorCounts()
    {
        var counts = new int[_numColors];
        foreach (int cell in _cells)
        {
            if (cell != Empty)
            {
                counts[cell]++;
            }
        }

        return counts;
    }

    public Board Copy() => new(_width, _height, _numColors, _cells);

    public List<Group> FindAllGroups()
    {
        var visited = new bool[_cells.Length];
        var groups = new List<Group>();
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int idx = Index(x, y);
                if (visited[idx] || _cells[idx] == Empty)
                {
                    continue;
                }

                var group = FloodFill(x, y, _cells[idx], visited);
                if (group.Size >= 2)
                {
                    groups.Add(group);
                }
            }
        }

        return groups;
    }

    public Group? FindGroupAt(int x, int y)
    {
        if (!InBounds(x, y) || Get(x, y) == Empty)
        {
            return null;
        }

        var visited = new bool[_cells.Length];
        var group = FloodFill(x, y, Get(x, y), visited);
        return group.Size >= 2 ? group : null;
    }

    public bool HasAnyMove() => FindAllGroups().Count > 0;

    public void RemoveGroup(Group group)
    {
        foreach (var p in group.Points)
        {
            Set(p.X, p.Y, Empty);
        }

        ApplyGravity();
        TrimEmptyColumns();
    }

    public void ApplyGravity()
    {
        for (int x = 0; x < _width; x++)
        {
            int writeY = _height - 1;
            for (int y = _height - 1; y >= 0; y--)
            {
                int value = Get(x, y);
                if (value != Empty)
                {
                    if (y != writeY)
                    {
                        Set(x, writeY, value);
                        Set(x, y, Empty);
                    }

                    writeY--;
                }
            }
        }
    }

    public void TrimEmptyColumns()
    {
        int writeX = 0;
        for (int x = 0; x < _width; x++)
        {
            if (!IsColumnEmpty(x))
            {
                if (x != writeX)
                {
                    for (int y = 0; y < _height; y++)
                    {
                        Set(writeX, y, Get(x, y));
                        Set(x, y, Empty);
                    }
                }

                writeX++;
            }
        }

        for (int x = writeX; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                Set(x, y, Empty);
            }
        }
    }

    private bool IsColumnEmpty(int x)
    {
        for (int y = 0; y < _height; y++)
        {
            if (Get(x, y) != Empty)
            {
                return false;
            }
        }

        return true;
    }

    private Group FloodFill(int startX, int startY, int color, bool[] visited)
    {
        var points = new List<Point>();
        var seen = new HashSet<long>();
        FloodFillRecursive(startX, startY, color, visited, points, seen);
        return new Group(color, points);
    }

    private void FloodFillRecursive(int x, int y, int color, bool[] visited, List<Point> points, HashSet<long> seen)
    {
        if (!InBounds(x, y))
        {
            return;
        }

        int idx = Index(x, y);
        if (visited[idx] || _cells[idx] != color)
        {
            return;
        }

        visited[idx] = true;
        long key = Pack(x, y);
        if (!seen.Add(key))
        {
            return;
        }

        points.Add(new Point(x, y));
        FloodFillRecursive(x + 1, y, color, visited, points, seen);
        FloodFillRecursive(x - 1, y, color, visited, points, seen);
        FloodFillRecursive(x, y + 1, color, visited, points, seen);
        FloodFillRecursive(x, y - 1, color, visited, points, seen);
    }

    private bool InBounds(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;

    private int Index(int x, int y) => y * _width + x;

    private static long Pack(int x, int y) => ((long)x << 32) | (uint)y;

    public readonly record struct Point(int X, int Y);

    public sealed class Group(int color, List<Point> points)
    {
        public int Color { get; } = color;
        public IReadOnlyList<Point> Points { get; } = points;
        public int Size => points.Count;
    }
}
