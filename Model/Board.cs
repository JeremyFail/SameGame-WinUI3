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

    /// <summary>
    /// Creates a new empty board with the given dimensions and color count.
    /// </summary>
    /// <param name="width">Board width in cells.</param>
    /// <param name="height">Board height in cells.</param>
    /// <param name="numColors">Number of distinct tile colors used on the board.</param>
    public Board(int width, int height, int numColors)
    {
        _width = width;
        _height = height;
        _numColors = numColors;
        _cells = new int[width * height];
        Array.Fill(_cells, Empty);
    }

    /// <summary>
    /// Creates a board from an existing cell array.
    /// </summary>
    /// <param name="width">Board width in cells.</param>
    /// <param name="height">Board height in cells.</param>
    /// <param name="numColors">Number of distinct tile colors used on the board.</param>
    /// <param name="cells">Flat cell values in row-major order; copied on construction.</param>
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

    /// <summary>
    /// Gets the color index at the given cell, or <see cref="Empty"/> if the cell is vacant.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>The stored color index or <see cref="Empty"/>.</returns>
    public int Get(int x, int y) => _cells[Index(x, y)];

    /// <summary>
    /// Sets the color index at the given cell.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <param name="value">Color index, or <see cref="Empty"/> to clear the cell.</param>
    public void Set(int x, int y, int value) => _cells[Index(x, y)] = value;

    /// <summary>
    /// Returns a copy of the internal cell array.
    /// </summary>
    /// <returns>A new array containing all cell values in row-major order.</returns>
    public int[] CellsCopy() => (int[])_cells.Clone();

    /// <summary>
    /// Determines whether every cell on the board is empty.
    /// </summary>
    /// <returns><c>true</c> if all cells are <see cref="Empty"/>; otherwise, <c>false</c>.</returns>
    public bool IsEmpty() => _cells.All(c => c == Empty);

    /// <summary>
    /// Counts non-empty cells remaining on the board.
    /// </summary>
    /// <returns>The number of cells that are not <see cref="Empty"/>.</returns>
    public int RemainingBlockCount() => _cells.Count(c => c != Empty);

    /// <summary>
    /// Tallies how many blocks of each color remain on the board.
    /// </summary>
    /// <returns>An array indexed by color with counts for each color.</returns>
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

    /// <summary>
    /// Creates a deep copy of this board.
    /// </summary>
    /// <returns>A new <see cref="Board"/> with identical dimensions and cell values.</returns>
    public Board Copy() => new(_width, _height, _numColors, _cells);

    /// <summary>
    /// Finds every connected group of two or more same-colored blocks on the board.
    /// </summary>
    /// <returns>A list of all valid removable groups.</returns>
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

    /// <summary>
    /// Finds the connected group containing the cell at the given coordinates, if it is removable.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>The group at that cell when it contains at least two blocks; otherwise, <c>null</c>.</returns>
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

    /// <summary>
    /// Determines whether at least one valid move remains on the board.
    /// </summary>
    /// <returns><c>true</c> if any group of size two or greater exists; otherwise, <c>false</c>.</returns>
    public bool HasAnyMove() => FindAllGroups().Count > 0;

    /// <summary>
    /// Removes a group from the board and applies gravity and column trimming.
    /// </summary>
    /// <param name="group">The group to remove.</param>
    public void RemoveGroup(Group group)
    {
        foreach (var p in group.Points)
        {
            Set(p.X, p.Y, Empty);
        }

        ApplyGravity();
        TrimEmptyColumns();
    }

    /// <summary>
    /// Drops all blocks downward within each column so empty cells sit at the top.
    /// </summary>
    public void ApplyGravity()
    {
        for (int x = 0; x < _width; x++)
        {
            // writeY tracks the next row (from bottom) where a block should land
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

    /// <summary>
    /// Shifts non-empty columns left and clears columns that became vacant.
    /// </summary>
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

        // Clear any columns left vacant on the right edge
        for (int x = writeX; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                Set(x, y, Empty);
            }
        }
    }

    /// <summary>
    /// Determines whether every cell in a column is empty.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <returns><c>true</c> if the column contains no blocks; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Collects all orthogonally connected cells of the same color starting from a seed cell.
    /// </summary>
    /// <param name="startX">Seed column index.</param>
    /// <param name="startY">Seed row index.</param>
    /// <param name="color">Color index to flood-fill.</param>
    /// <param name="visited">Global visited flags shared across group scans.</param>
    /// <returns>A <see cref="Group"/> containing every connected cell of the given color.</returns>
    private Group FloodFill(int startX, int startY, int color, bool[] visited)
    {
        var points = new List<Point>();
        var seen = new HashSet<long>();
        FloodFillRecursive(startX, startY, color, visited, points, seen);
        return new Group(color, points);
    }

    /// <summary>
    /// Recursively visits adjacent cells of the same color during flood-fill.
    /// </summary>
    /// <param name="x">Current column index.</param>
    /// <param name="y">Current row index.</param>
    /// <param name="color">Color index being collected.</param>
    /// <param name="visited">Global visited flags shared across group scans.</param>
    /// <param name="points">Accumulated cell coordinates for the current group.</param>
    /// <param name="seen">Per-group set preventing duplicate point insertion.</param>
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
        // Explore four orthogonal neighbors
        FloodFillRecursive(x + 1, y, color, visited, points, seen);
        FloodFillRecursive(x - 1, y, color, visited, points, seen);
        FloodFillRecursive(x, y + 1, color, visited, points, seen);
        FloodFillRecursive(x, y - 1, color, visited, points, seen);
    }

    /// <summary>
    /// Determines whether coordinates lie within the board bounds.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns><c>true</c> if the coordinates are in bounds; otherwise, <c>false</c>.</returns>
    private bool InBounds(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;

    /// <summary>
    /// Converts two-dimensional coordinates to a flat cell index.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>The row-major index into the internal cell array.</returns>
    private int Index(int x, int y) => y * _width + x;

    /// <summary>
    /// Packs coordinates into a single value for hash-set deduplication.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>A unique 64-bit key for the coordinate pair.</returns>
    private static long Pack(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>
    /// A cell coordinate on the board grid.
    /// </summary>
    /// <param name="X">Column index.</param>
    /// <param name="Y">Row index.</param>
    public readonly record struct Point(int X, int Y);

    /// <summary>
    /// A connected set of same-colored blocks that can be removed as one move.
    /// </summary>
    /// <param name="color">The shared color index of every block in the group.</param>
    /// <param name="points">The cell coordinates belonging to the group.</param>
    public sealed class Group(int color, List<Point> points)
    {
        public int Color { get; } = color;
        public IReadOnlyList<Point> Points { get; } = points;
        public int Size => points.Count;
    }
}
