namespace SameGame.Model;

/// <summary>
/// Manages score, moves, undo, and redo for an active game session.
/// </summary>
public sealed class GameSession
{
    private GameSettings _settings;
    private Board _board = null!;
    private Board _initialBoard = null!;
    private long _seed;
    private int _score;
    private int _moveCount;
    private bool _locked;
    private readonly Stack<Snapshot> _undoStack = new();
    private readonly Stack<Snapshot> _redoStack = new();

    /// <summary>
    /// Creates a session bound to the given settings; call <see cref="NewGame"/> to start play.
    /// </summary>
    /// <param name="settings">Game configuration used for board generation and display.</param>
    public GameSession(GameSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Starts a new game by generating a board from the session settings and seed.
    /// </summary>
    /// <param name="seed">Seed used for deterministic board generation.</param>
    public void NewGame(long seed)
    {
        var generated = BoardGenerator.Generate(_settings, seed);
        NewGame(generated.Board, generated.Seed);
    }

    /// <summary>
    /// Starts a new game with a pre-built board and seed.
    /// </summary>
    /// <param name="board">The initial board state.</param>
    /// <param name="seed">Seed associated with this board for replay or sharing.</param>
    public void NewGame(Board board, long seed)
    {
        _board = board;
        _initialBoard = board.Copy();
        _seed = seed;
        _score = 0;
        _moveCount = 0;
        _locked = false;
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>
    /// Starts a new game using a seed derived from the current UTC time.
    /// </summary>
    public void NewRandomGame() => NewGame(DateTime.UtcNow.Ticks);

    /// <summary>
    /// Resets the board, score, and move history to the initial state of the current game.
    /// </summary>
    public void Restart()
    {
        _board = _initialBoard.Copy();
        _score = 0;
        _moveCount = 0;
        _locked = false;
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>
    /// Removes a valid group, updates the score, and records the move for undo.
    /// </summary>
    /// <param name="group">The group to remove; must contain at least two blocks.</param>
    /// <returns><c>true</c> if the move was applied; otherwise, <c>false</c>.</returns>
    public bool ApplyMove(Board.Group group)
    {
        if (_locked || group == null || group.Size < 2)
        {
            return false;
        }

        _undoStack.Push(SnapshotOfCurrentState());
        _redoStack.Clear();
        _board.RemoveGroup(group);
        _score += Scoring.PointsForGroup(group.Size);
        _moveCount++;
        return true;
    }

    /// <summary>
    /// Reverts the most recent move when undo is available.
    /// </summary>
    /// <returns><c>true</c> if a state was restored; otherwise, <c>false</c>.</returns>
    public bool Undo()
    {
        if (_locked || _undoStack.Count == 0)
        {
            return false;
        }

        _redoStack.Push(SnapshotOfCurrentState());
        RestoreSnapshot(_undoStack.Pop());
        return true;
    }

    /// <summary>
    /// Re-applies the most recently undone move when redo is available.
    /// </summary>
    /// <returns><c>true</c> if a state was restored; otherwise, <c>false</c>.</returns>
    public bool Redo()
    {
        if (_locked || _redoStack.Count == 0)
        {
            return false;
        }

        _undoStack.Push(SnapshotOfCurrentState());
        RestoreSnapshot(_redoStack.Pop());
        return true;
    }

    /// <summary>
    /// Determines whether undo is currently allowed.
    /// </summary>
    /// <returns><c>true</c> if the session is unlocked and undo history exists; otherwise, <c>false</c>.</returns>
    public bool CanUndo() => !_locked && _undoStack.Count > 0;

    /// <summary>
    /// Determines whether redo is currently allowed.
    /// </summary>
    /// <returns><c>true</c> if the session is unlocked and redo history exists; otherwise, <c>false</c>.</returns>
    public bool CanRedo() => !_locked && _redoStack.Count > 0;

    /// <summary>
    /// Prevents moves, undo, and redo until <see cref="Unlock"/> is called.
    /// </summary>
    public void Lock() => _locked = true;

    /// <summary>
    /// Allows moves, undo, and redo after a prior <see cref="Lock"/> call.
    /// </summary>
    public void Unlock() => _locked = false;

    public bool IsLocked => _locked;

    /// <summary>
    /// Determines whether no valid moves remain on the board.
    /// </summary>
    /// <returns><c>true</c> if the board has no removable groups; otherwise, <c>false</c>.</returns>
    public bool IsGameOver() => !_board.HasAnyMove();

    /// <summary>
    /// Determines whether the current score is greater than zero.
    /// </summary>
    /// <returns><c>true</c> if the score is positive; otherwise, <c>false</c>.</returns>
    public bool HasScore() => _score > 0;

    /// <summary>
    /// Determines whether at least one move has been played in this session.
    /// </summary>
    /// <returns><c>true</c> if the move count is greater than zero; otherwise, <c>false</c>.</returns>
    public bool HasStarted() => _moveCount > 0;

    public GameSettings Settings
    {
        get => _settings;
        set => _settings = value;
    }

    public Board Board => _board;
    public long Seed => _seed;
    public int Score => _score;
    public int MoveCount => _moveCount;

    /// <summary>
    /// Captures the current board and score state for undo/redo stacks.
    /// </summary>
    /// <returns>A snapshot of the active session state.</returns>
    private Snapshot SnapshotOfCurrentState() =>
        new(_board.CellsCopy(), _board.Width, _board.Height, _board.NumColors, _score, _moveCount);

    /// <summary>
    /// Restores board, score, and move count from a saved snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore.</param>
    private void RestoreSnapshot(Snapshot snapshot)
    {
        _board = new Board(snapshot.Width, snapshot.Height, snapshot.NumColors, snapshot.Cells);
        _score = snapshot.Score;
        _moveCount = snapshot.MoveCount;
    }

    /// <summary>
    /// Immutable board and score state stored on the undo/redo stacks.
    /// </summary>
    /// <param name="Cells">Flat cell values in row-major order.</param>
    /// <param name="Width">Board width in cells.</param>
    /// <param name="Height">Board height in cells.</param>
    /// <param name="NumColors">Number of distinct tile colors.</param>
    /// <param name="Score">Score at the time of the snapshot.</param>
    /// <param name="MoveCount">Number of moves played at the time of the snapshot.</param>
    private readonly record struct Snapshot(
        int[] Cells,
        int Width,
        int Height,
        int NumColors,
        int Score,
        int MoveCount);
}
