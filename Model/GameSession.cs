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

    public GameSession(GameSettings settings)
    {
        _settings = settings;
    }

    public void NewGame(long seed)
    {
        var generated = BoardGenerator.Generate(_settings, seed);
        NewGame(generated.Board, generated.Seed);
    }

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

    public void NewRandomGame() => NewGame(DateTime.UtcNow.Ticks);

    public void Restart()
    {
        _board = _initialBoard.Copy();
        _score = 0;
        _moveCount = 0;
        _locked = false;
        _undoStack.Clear();
        _redoStack.Clear();
    }

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

    public bool CanUndo() => !_locked && _undoStack.Count > 0;
    public bool CanRedo() => !_locked && _redoStack.Count > 0;
    public void Lock() => _locked = true;
    public void Unlock() => _locked = false;
    public bool IsLocked => _locked;
    public bool IsGameOver() => !_board.HasAnyMove();
    public bool HasScore() => _score > 0;
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

    private Snapshot SnapshotOfCurrentState() =>
        new(_board.CellsCopy(), _board.Width, _board.Height, _board.NumColors, _score, _moveCount);

    private void RestoreSnapshot(Snapshot snapshot)
    {
        _board = new Board(snapshot.Width, snapshot.Height, snapshot.NumColors, snapshot.Cells);
        _score = snapshot.Score;
        _moveCount = snapshot.MoveCount;
    }

    private readonly record struct Snapshot(
        int[] Cells,
        int Width,
        int Height,
        int NumColors,
        int Score,
        int MoveCount);
}
