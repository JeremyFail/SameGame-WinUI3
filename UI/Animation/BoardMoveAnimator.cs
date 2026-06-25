using Microsoft.UI.Xaml;
using SameGame.Model;
using Windows.UI;

namespace SameGame.UI.Animation;

/// <summary>
/// Drives shatter, fall, and column-slide animations for a board move.
/// </summary>
public sealed class BoardMoveAnimator
{
    private const int FrameMs = 16;
    private const int ShatterMs = 380;
    private const int ShatterMaxMs = ShatterMs + 28 * FrameMs + 32;
    private const int FallMs = 420;
    private const int SlideMs = 280;

    private readonly List<Particle> _particles = [];
    private readonly Dictionary<long, float> _yOffsetsCells = [];
    private readonly Dictionary<int, int> _newColumnFromOld = [];
    private float _slideProgress = 1f;
    private float _fallProgress = 1f;

    private DispatcherTimer? _timer;
    private long _phaseStart;
    private Phase _phase = Phase.Done;
    private Action? _onComplete;

    private Board? _boardBefore;
    private Board? _boardAfterGravity;
    private Board? _boardFinal;
    private Board.Group? _removedGroup;

    /// <summary>
    /// Public-facing animation phase identifiers exposed to the board canvas.
    /// </summary>
    public enum AnimationPhase
    {
        /// <summary>Shatter particles are active over removed cells.</summary>
        Shatter,
        /// <summary>Tiles are falling to fill vertical gaps.</summary>
        Fall,
        /// <summary>Columns are sliding left to close horizontal gaps.</summary>
        Slide
    }

    /// <summary>
    /// Internal animation state machine phase including the idle state.
    /// </summary>
    private enum Phase
    {
        Shatter,
        Fall,
        Slide,
        Done
    }

    /// <summary>
    /// Starts the shatter-fall-slide animation sequence for a removed group.
    /// </summary>
    /// <param name="before">The board state before the group was removed.</param>
    /// <param name="group">The group of cells that was removed.</param>
    /// <param name="settings">Game settings; animations are skipped when disabled.</param>
    /// <param name="cellSize">The pixel size of one cell, used for particle sizing.</param>
    /// <param name="onComplete">Callback invoked when the animation finishes or is skipped.</param>
    /// <returns><see langword="true"/> if the animation started; <see langword="false"/> when animations are disabled.</returns>
    public bool Start(Board before, Board.Group group, GameSettings settings, int cellSize, Action onComplete)
    {
        Stop();
        _onComplete = onComplete;
        _removedGroup = group;
        _boardBefore = before.Copy();

        if (!settings.AnimationsEnabled)
        {
            return false;
        }

        _boardAfterGravity = ComputeAfterGravity(before, group);
        _boardFinal = ComputeFinalBoard(before, group);
        PrepareFallOffsets();
        PrepareSlideMap();
        SpawnParticles(group, settings, cellSize, before.Width * before.Height);

        _phase = Phase.Shatter;
        _fallProgress = 0f;
        _slideProgress = 0f;
        _phaseStart = Environment.TickCount64;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMs) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        return true;
    }

    /// <summary>
    /// Gets whether an animation sequence is currently running.
    /// </summary>
    public bool IsRunning => _phase != Phase.Done;

    /// <summary>
    /// Gets the current public animation phase, or <see langword="null"/> when idle.
    /// </summary>
    public AnimationPhase? CurrentPhase => _phase switch
    {
        Phase.Shatter => AnimationPhase.Shatter,
        Phase.Fall => AnimationPhase.Fall,
        Phase.Slide => AnimationPhase.Slide,
        _ => null
    };

    /// <summary>
    /// Immediately completes the animation if one is running.
    /// </summary>
    public void Skip()
    {
        if (IsRunning)
        {
            FinishImmediately();
        }
    }

    /// <summary>
    /// Stops the animation timer and clears all animation state without invoking the completion callback.
    /// </summary>
    public void Stop()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }

        _phase = Phase.Done;
        _particles.Clear();
        _yOffsetsCells.Clear();
        _newColumnFromOld.Clear();
        ClearAnimationBoards();
    }

    /// <summary>
    /// Returns the board snapshot appropriate for the current animation phase.
    /// </summary>
    /// <param name="fallback">The board to return when no animation board is available.</param>
    /// <returns>The display board for the active phase, or <paramref name="fallback"/> when idle.</returns>
    public Board DisplayBoard(Board fallback) => _phase switch
    {
        Phase.Shatter => _boardBefore ?? fallback,
        Phase.Fall => _boardAfterGravity ?? fallback,
        Phase.Slide => _boardFinal ?? fallback,
        _ => fallback
    };

    /// <summary>
    /// Gets the vertical offset in cell units for a tile during the fall phase.
    /// </summary>
    /// <param name="x">The column index of the cell.</param>
    /// <param name="y">The row index of the cell in the post-gravity board.</param>
    /// <returns>The current Y offset in cell units (0 when not falling).</returns>
    public float YOffsetCells(int x, int y)
    {
        if (_phase != Phase.Fall)
        {
            return 0f;
        }

        float start = _yOffsetsCells.GetValueOrDefault(Pack(x, y), 0f);
        return start * (1f - _fallProgress);
    }

    /// <summary>
    /// Gets the horizontal offset in column units for a column during the slide phase.
    /// </summary>
    /// <param name="column">The destination column index.</param>
    /// <returns>The current X offset in column units (0 when not sliding).</returns>
    public float XOffsetColumns(int column)
    {
        if (_phase != Phase.Slide || !_newColumnFromOld.TryGetValue(column, out int oldX))
        {
            return 0f;
        }

        return (oldX - column) * (1f - _slideProgress);
    }

    /// <summary>
    /// Gets the live list of shatter particles.
    /// </summary>
    public IReadOnlyList<Particle> Particles => _particles;

    /// <summary>
    /// Determines whether a cell is part of the removed group during the shatter phase.
    /// </summary>
    /// <param name="x">The column index of the cell.</param>
    /// <param name="y">The row index of the cell.</param>
    /// <returns><see langword="true"/> if the cell is being shattered; otherwise <see langword="false"/>.</returns>
    public bool IsRemovedCell(int x, int y)
    {
        if (_phase != Phase.Shatter || _removedGroup is null)
        {
            return false;
        }

        return _removedGroup.Points.Any(p => p.X == x && p.Y == y);
    }

    /// <summary>
    /// Determines whether a cell requires per-frame repositioning during fall or slide.
    /// </summary>
    /// <param name="x">The column index of the cell.</param>
    /// <param name="y">The row index of the cell.</param>
    /// <param name="displayBoard">The board snapshot currently being rendered.</param>
    /// <returns><see langword="true"/> if the cell is animated this frame; otherwise <see langword="false"/>.</returns>
    public bool IsDynamicCell(int x, int y, Board displayBoard)
    {
        if (_phase == Phase.Fall)
        {
            return _yOffsetsCells.ContainsKey(Pack(x, y));
        }

        if (_phase == Phase.Slide)
        {
            return _newColumnFromOld.ContainsKey(x) && displayBoard.Get(x, y) != Board.Empty;
        }

        return false;
    }

    /// <summary>
    /// Returns cell bounds [minX, minY, maxX, maxY] that must be repainted during shatter.
    /// </summary>
    /// <returns>A four-element bounds array, or an empty array when not shattering.</returns>
    public int[] ShatterDirtyCellBounds()
    {
        if (_phase != Phase.Shatter || _removedGroup is null || _removedGroup.Points.Count == 0 || _boardBefore is null)
        {
            return [];
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        foreach (var p in _removedGroup.Points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        const int margin = 2;
        minX = Math.Max(0, minX - margin);
        minY = Math.Max(0, minY - margin);
        maxX = Math.Min(_boardBefore.Width - 1, maxX + margin);
        maxY = Math.Min(_boardBefore.Height - 1, maxY + margin);
        return [minX, minY, maxX, maxY];
    }

    /// <summary>
    /// Returns cell bounds covering each falling tile's start and end rows.
    /// </summary>
    /// <returns>A four-element bounds array, or an empty array when not falling.</returns>
    public int[] FallDirtyCellBounds()
    {
        if (_phase != Phase.Fall || _yOffsetsCells.Count == 0 || _boardAfterGravity is null)
        {
            return [];
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        foreach (var entry in _yOffsetsCells)
        {
            int x = (int)(entry.Key >> 32);
            int newY = (int)(entry.Key & 0xffffffff);
            int startY = newY + (int)Math.Ceiling(entry.Value);
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, Math.Min(newY, startY));
            maxY = Math.Max(maxY, Math.Max(newY, startY));
        }

        const int pad = 1;
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(_boardAfterGravity.Width - 1, maxX + pad);
        maxY = Math.Min(_boardAfterGravity.Height - 1, maxY + pad);
        return [minX, minY, maxX, maxY];
    }

    /// <summary>
    /// Returns cell bounds for all sliding columns during the slide phase.
    /// </summary>
    /// <returns>A four-element bounds array, or an empty array when not sliding.</returns>
    public int[] SlideDirtyCellBounds()
    {
        if (_boardFinal is null)
        {
            return [];
        }

        var columns = SlideColumns();
        if (columns.Count == 0)
        {
            return [];
        }

        int minX = columns.Min();
        int maxX = columns.Max();
        return [minX, 0, maxX, _boardFinal.Height - 1];
    }

    /// <summary>
    /// Computes the pixel bounding rectangle enclosing all active particles.
    /// </summary>
    /// <param name="offsetX">The horizontal board offset in pixels.</param>
    /// <param name="offsetY">The vertical board offset in pixels.</param>
    /// <param name="cellSize">The pixel size of one cell.</param>
    /// <returns>The particle bounds in pixels, or <see langword="null"/> when no particles exist.</returns>
    public Windows.Foundation.Rect? ParticlePixelBounds(float offsetX, float offsetY, float cellSize)
    {
        if (_particles.Count == 0)
        {
            return null;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        foreach (var particle in _particles)
        {
            float px = offsetX + particle.CellX * cellSize;
            float py = offsetY + particle.CellY * cellSize;
            float half = particle.Size * 0.75f + 4f;
            minX = Math.Min(minX, px - half);
            minY = Math.Min(minY, py - half);
            maxX = Math.Max(maxX, px + half);
            maxY = Math.Max(maxY, py + half);
        }

        return new Windows.Foundation.Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Collects all column indices that move or are affected during the slide phase.
    /// </summary>
    /// <returns>The set of column indices requiring repaint during slide.</returns>
    private HashSet<int> SlideColumns()
    {
        var columns = new HashSet<int>();
        if (_boardFinal is null)
        {
            return columns;
        }

        int maxCol = 0;
        foreach (var entry in _newColumnFromOld)
        {
            int dest = entry.Key;
            int src = entry.Value;
            int lo = Math.Min(dest, src);
            int hi = Math.Max(dest, src);
            for (int x = lo; x <= hi; x++)
            {
                columns.Add(x);
            }

            maxCol = Math.Max(maxCol, hi);
        }

        for (int x = maxCol + 1; x < _boardFinal.Width; x++)
        {
            columns.Add(x);
        }

        return columns;
    }

    /// <summary>
    /// Gets the removed group being animated, if any.
    /// </summary>
    public Board.Group? RemovedGroup => _removedGroup;

    /// <summary>
    /// Advances the animation one frame and transitions between phases when durations elapse.
    /// </summary>
    private void Tick()
    {
        if (_phase == Phase.Shatter)
        {
            UpdateParticles();
        }

        long elapsed = Environment.TickCount64 - _phaseStart;

        // Shatter phase: wait for particles to finish or hit the max timeout.
        if (_phase == Phase.Shatter && elapsed >= ShatterMs)
        {
            if (_particles.Count == 0 || elapsed >= ShatterMaxMs)
            {
                _particles.Clear();
                _phase = Phase.Fall;
                _phaseStart = Environment.TickCount64;
            }
        }
        // Fall phase: ease tiles down with a bounce curve.
        else if (_phase == Phase.Fall)
        {
            _fallProgress = Math.Min(1f, elapsed / (float)FallMs);
            _fallProgress = BounceOut(_fallProgress);
            if (elapsed >= FallMs)
            {
                _phase = Phase.Slide;
                _phaseStart = Environment.TickCount64;
                _fallProgress = 1f;
            }
        }
        // Slide phase: ease columns left into final positions.
        else if (_phase == Phase.Slide)
        {
            _slideProgress = Math.Min(1f, elapsed / (float)SlideMs);
            _slideProgress = EaseOutCubic(_slideProgress);
            if (elapsed >= SlideMs)
            {
                FinishImmediately();
            }
        }
    }

    /// <summary>
    /// Stops the timer, clears state, and invokes the completion callback.
    /// </summary>
    private void FinishImmediately()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }

        _phase = Phase.Done;
        _particles.Clear();
        _yOffsetsCells.Clear();
        _newColumnFromOld.Clear();
        ClearAnimationBoards();
        Action? complete = _onComplete;
        _onComplete = null;
        complete?.Invoke();
    }

    /// <summary>
    /// Releases references to cached board snapshots.
    /// </summary>
    private void ClearAnimationBoards()
    {
        _boardBefore = null;
        _boardAfterGravity = null;
        _boardFinal = null;
        _removedGroup = null;
    }

    /// <summary>
    /// Computes the board state after removing the group and applying gravity only.
    /// </summary>
    /// <param name="before">The board state before the move.</param>
    /// <param name="group">The removed group.</param>
    /// <returns>A copy of the board with gravity applied but columns not yet compacted.</returns>
    private static Board ComputeAfterGravity(Board before, Board.Group group)
    {
        var board = before.Copy();
        foreach (var p in group.Points)
        {
            board.Set(p.X, p.Y, Board.Empty);
        }

        board.ApplyGravity();
        return board;
    }

    /// <summary>
    /// Computes the final board state after full group removal and column compaction.
    /// </summary>
    /// <param name="before">The board state before the move.</param>
    /// <param name="group">The removed group.</param>
    /// <returns>The fully resolved board after the move.</returns>
    private static Board ComputeFinalBoard(Board before, Board.Group group)
    {
        var board = before.Copy();
        board.RemoveGroup(group);
        return board;
    }

    /// <summary>
    /// Builds the map of vertical fall distances for each tile that moved downward.
    /// </summary>
    private void PrepareFallOffsets()
    {
        var afterRemove = BeforeWithoutGroup();
        for (int x = 0; x < _boardBefore!.Width; x++)
        {
            var oldYs = ColumnFilledYs(afterRemove, x);
            var newYs = ColumnFilledYs(_boardAfterGravity!, x);
            for (int i = 0; i < newYs.Count && i < oldYs.Count; i++)
            {
                int oldY = oldYs[i];
                int newY = newYs[i];
                if (oldY != newY)
                {
                    _yOffsetsCells[Pack(x, newY)] = oldY - newY;
                }
            }
        }
    }

    /// <summary>
    /// Returns a board copy with the removed group cleared but before gravity or compaction.
    /// </summary>
    /// <returns>The intermediate board used to compute fall offsets.</returns>
    private Board BeforeWithoutGroup()
    {
        var board = _boardBefore!.Copy();
        foreach (var p in _removedGroup!.Points)
        {
            board.Set(p.X, p.Y, Board.Empty);
        }

        return board;
    }

    /// <summary>
    /// Builds the mapping from destination column index to source column index for the slide phase.
    /// </summary>
    private void PrepareSlideMap()
    {
        int writeX = 0;
        for (int x = 0; x < _boardAfterGravity!.Width; x++)
        {
            if (!IsColumnEmpty(_boardAfterGravity, x))
            {
                if (x != writeX)
                {
                    _newColumnFromOld[writeX] = x;
                }

                writeX++;
            }
        }
    }

    /// <summary>
    /// Spawns shatter particles at each cell in the removed group.
    /// </summary>
    /// <param name="group">The removed group.</param>
    /// <param name="settings">Game settings used to resolve particle colors.</param>
    /// <param name="cellSize">The pixel size of one cell.</param>
    /// <param name="boardCells">The total number of cells on the board, used to scale particle count.</param>
    private void SpawnParticles(Board.Group group, GameSettings settings, int cellSize, int boardCells)
    {
        int perTile = ParticlesPerTile(cellSize, boardCells);
        int particlePx = ParticlePixelSize(cellSize);
        var random = new Random();
        foreach (var p in group.Points)
        {
            Color color = settings.ColorAt(group.Color);
            for (int i = 0; i < perTile; i++)
            {
                _particles.Add(new Particle(
                    p.X + 0.5f,
                    p.Y + 0.5f,
                    (random.NextSingle() - 0.5f) * 0.18f,
                    random.NextSingle() * 0.08f + 0.04f,
                    random.NextSingle() * 360f,
                    particlePx,
                    color));
            }
        }
    }

    /// <summary>
    /// Determines how many particles to spawn per removed tile based on board and cell size.
    /// </summary>
    /// <param name="cellSize">The pixel size of one cell.</param>
    /// <param name="boardCells">The total number of cells on the board.</param>
    /// <returns>The particle count per tile.</returns>
    private static int ParticlesPerTile(int cellSize, int boardCells)
    {
        int count = Math.Max(2, 6 - boardCells / 400);
        if (cellSize >= 48)
        {
            count = Math.Min(8, count + 2);
        }
        else if (cellSize >= 36)
        {
            count = Math.Min(7, count + 1);
        }
        else if (cellSize < 20)
        {
            count = Math.Max(2, count - 1);
        }

        return count;
    }

    /// <summary>
    /// Computes the pixel diameter of a single shatter particle.
    /// </summary>
    /// <param name="cellSize">The pixel size of one cell.</param>
    /// <returns>The particle size in pixels.</returns>
    private static int ParticlePixelSize(int cellSize) =>
        Math.Max(3, Math.Min(12, (int)Math.Round(cellSize * 0.15f) + 2));

    /// <summary>
    /// Updates all live particles and removes those that have expired.
    /// </summary>
    private void UpdateParticles()
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            _particles[i].Tick();
            if (_particles[i].Dead)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Returns the row indices of filled cells in a column, bottom to top.
    /// </summary>
    /// <param name="board">The board to inspect.</param>
    /// <param name="x">The column index.</param>
    /// <returns>The list of filled row indices from bottom to top.</returns>
    private static List<int> ColumnFilledYs(Board board, int x)
    {
        var ys = new List<int>();
        for (int y = board.Height - 1; y >= 0; y--)
        {
            if (board.Get(x, y) != Board.Empty)
            {
                ys.Add(y);
            }
        }

        return ys;
    }

    /// <summary>
    /// Determines whether every cell in a column is empty.
    /// </summary>
    /// <param name="board">The board to inspect.</param>
    /// <param name="x">The column index.</param>
    /// <returns><see langword="true"/> if the column contains no tiles; otherwise <see langword="false"/>.</returns>
    private static bool IsColumnEmpty(Board board, int x)
    {
        for (int y = 0; y < board.Height; y++)
        {
            if (board.Get(x, y) != Board.Empty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Packs a cell coordinate pair into a single dictionary key.
    /// </summary>
    /// <param name="x">The column index.</param>
    /// <param name="y">The row index.</param>
    /// <returns>The packed coordinate key.</returns>
    private static long Pack(int x, int y) => ((long)x << 32) | (uint)y;

    /// <summary>
    /// Applies a cubic ease-out curve to a normalized time value.
    /// </summary>
    /// <param name="t">The normalized time (0–1).</param>
    /// <returns>The eased progress value.</returns>
    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    /// <summary>
    /// Applies a bounce-out easing curve suitable for falling tiles.
    /// </summary>
    /// <param name="t">The normalized time (0–1).</param>
    /// <returns>The eased progress value with a subtle bounce at the end.</returns>
    private static float BounceOut(float t)
    {
        if (t < 1f / 2.75f)
        {
            return 7.5625f * t * t;
        }

        if (t < 2f / 2.75f)
        {
            t -= 1.5f / 2.75f;
            return 7.5625f * t * t + 0.75f;
        }

        if (t < 2.5f / 2.75f)
        {
            t -= 2.25f / 2.75f;
            return 7.5625f * t * t + 0.9375f;
        }

        t -= 2.625f / 2.75f;
        return 7.5625f * t * t + 0.984375f;
    }

    /// <summary>
    /// A single shatter particle with position, velocity, and lifetime.
    /// </summary>
    public sealed class Particle
    {
        private const int MaxLife = 28;
        private float _cellX;
        private float _cellY;
        private float _velocityX;
        private float _velocityY;
        private float _rotation;
        private readonly float _rotationSpeed;
        private readonly int _size;
        private readonly Color _color;
        private int _life;

        /// <summary>
        /// Creates a new shatter particle at the given cell position.
        /// </summary>
        /// <param name="cellX">The initial horizontal position in cell units.</param>
        /// <param name="cellY">The initial vertical position in cell units.</param>
        /// <param name="velocityX">The initial horizontal velocity in cells per frame.</param>
        /// <param name="velocityY">The initial vertical velocity in cells per frame.</param>
        /// <param name="rotationSpeed">The rotation speed in degrees per frame.</param>
        /// <param name="size">The particle diameter in pixels.</param>
        /// <param name="color">The fill color of the particle.</param>
        public Particle(
            float cellX,
            float cellY,
            float velocityX,
            float velocityY,
            float rotationSpeed,
            int size,
            Color color)
        {
            _cellX = cellX;
            _cellY = cellY;
            _velocityX = velocityX;
            _velocityY = velocityY;
            _rotationSpeed = rotationSpeed;
            _size = size;
            _color = color;
            _life = MaxLife;
        }

        /// <summary>
        /// Advances the particle one frame, applying gravity and decrementing lifetime.
        /// </summary>
        public void Tick()
        {
            _cellX += _velocityX;
            _cellY += _velocityY;
            _velocityY += 0.012f;
            _rotation += _rotationSpeed;
            _life--;
        }

        /// <summary>
        /// Gets whether the particle has expired.
        /// </summary>
        public bool Dead => _life <= 0;

        /// <summary>
        /// Gets the current horizontal position in cell units.
        /// </summary>
        public float CellX => _cellX;

        /// <summary>
        /// Gets the current vertical position in cell units.
        /// </summary>
        public float CellY => _cellY;

        /// <summary>
        /// Gets the current rotation angle in degrees.
        /// </summary>
        public float Rotation => _rotation;

        /// <summary>
        /// Gets the particle diameter in pixels.
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Gets the fill color of the particle.
        /// </summary>
        public Color Color => _color;

        /// <summary>
        /// Gets the current opacity factor (0–1) based on remaining lifetime.
        /// </summary>
        public float Alpha => Math.Max(0f, _life / (float)MaxLife);
    }
}
