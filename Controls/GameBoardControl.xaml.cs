using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SameGame.Model;
using SameGame.UI;
using SameGame.UI.Animation;
using Windows.Foundation;
using Windows.UI;

namespace SameGame.Controls;

public sealed partial class GameBoardControl : UserControl
{
    private Board? _board;
    private GameSettings? _settings;
    private Board.Group? _highlightedGroup;
    private readonly BoardMoveAnimator _animator = new();
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _selectionSpinTimer;
    private float _gemSpinDegrees;
    private long _lastSpinTick;
    private readonly List<Board.Group> _coastingGroups = [];
    private bool _selectionAnimCoasting;
    private float _coastTarget;
    private Windows.Foundation.Rect? _lastParticlePixelBounds;
    private Windows.Foundation.Rect? _shatterParticleFootprint;
    private CanvasRenderTarget? _frameBuffer;
    private float _frameBufferWidth;
    private float _frameBufferHeight;
    private float _frameBufferDpi;
    private bool _frameBufferStaticStale = true;

    public event Action<Board.Group>? GroupConfirmed;
    public event Action? SelectionChanged;
    public event Action? SelectionCleared;

    public GameBoardControl()
    {
        InitializeComponent();
        BoardCanvas.ClearColor = Color.FromArgb(0, 0, 0, 0);
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) =>
        {
            if (_animator.IsRunning)
            {
                BoardCanvas.Invalidate();
            }
        };
        _selectionSpinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _selectionSpinTimer.Tick += SelectionSpinTimer_Tick;
        Loaded += (_, _) => BoardCanvas.Invalidate();
        SizeChanged += (_, _) =>
        {
            DisposeFrameBuffer();
            BoardCanvas.Invalidate();
        };
    }

    public bool IsAnimating => _animator.IsRunning;

    public Board.Group? HighlightedGroup => _highlightedGroup;

    public void Configure(Board board, GameSettings settings)
    {
        _board = board;
        _settings = settings;
        _highlightedGroup = null;
        _animator.Stop();
        _animationTimer.Stop();
        StopSelectionSpin();
        _selectionAnimCoasting = false;
        _coastingGroups.Clear();
        _coastTarget = 0f;
        _gemSpinDegrees = 0f;
        _lastParticlePixelBounds = null;
        _shatterParticleFootprint = null;
        _frameBufferStaticStale = true;
        DisposeFrameBuffer();
        BoardCanvas.Invalidate();
    }

    public void SetHighlightedGroup(Board.Group? group)
    {
        if (_highlightedGroup is not null && group is not null && !GroupsEqual(_highlightedGroup, group))
        {
            BeginSelectionAnimationCoastIfNeeded();
        }
        else if (_highlightedGroup is not null && group is null)
        {
            BeginSelectionAnimationCoastIfNeeded();
        }

        _highlightedGroup = group;
        UpdateSelectionSpinTimer();
        BoardCanvas.Invalidate();
        if (group is null)
        {
            SelectionCleared?.Invoke();
        }
        else
        {
            SelectionChanged?.Invoke();
        }
    }

    public void ClearHighlight() => SetHighlightedGroup(null);

    public void SettingsChanged()
    {
        _frameBufferStaticStale = true;
        UpdateSelectionSpinTimer();
        BoardCanvas.Invalidate();
    }

    public void AnimateRemove(Board.Group group, Action onComplete)
    {
        BeginSelectionAnimationCoastIfNeeded();
        _highlightedGroup = null;
        UpdateSelectionSpinTimer();
        if (_board is null || _settings is null)
        {
            onComplete();
            return;
        }

        void WrappedComplete()
        {
            _animationTimer.Stop();
            _frameBufferStaticStale = true;
            onComplete();
            BoardCanvas.Invalidate();
        }

        var layout = ComputeLayout(BoardCanvas.Size);
        int cellSize = layout is null ? 32 : (int)layout.Value.CellSize;
        _lastParticlePixelBounds = null;
        _shatterParticleFootprint = null;
        CaptureFrameBufferBeforeAnimation(BoardCanvas.Size, BoardCanvas.Dpi);
        if (_animator.Start(_board, group, _settings, cellSize, WrappedComplete))
        {
            _animationTimer.Start();
            BoardCanvas.Invalidate();
        }
        else
        {
            WrappedComplete();
        }
    }

    private void BoardCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_board is null || _settings is null)
        {
            return;
        }

        var ds = args.DrawingSession;
        var layout = ComputeLayout(sender.Size);
        if (layout is null)
        {
            return;
        }

        var displayBoard = _animator.DisplayBoard(_board);
        bool highlightActive = _highlightedGroup is not null && !_animator.IsRunning;

        if (!_animator.IsRunning)
        {
            _lastParticlePixelBounds = null;
            _shatterParticleFootprint = null;
            if (TryPaintIdleFromStaticBuffer(ds, layout.Value, displayBoard, highlightActive, sender.Size, sender.Dpi))
            {
                return;
            }

            ds.Clear(_settings.BackgroundColor());
            PaintIdleBoard(ds, layout.Value, displayBoard, highlightActive);
            SyncStaticFrameBuffer(ds, layout.Value, displayBoard, sender.Size, sender.Dpi);
        }
        else if (UsesFullBoardAnimRepaint())
        {
            PaintFullBoardAnimFrame(ds, layout.Value, displayBoard);
        }
        else
        {
            PaintAnimatingBoard(ds, layout.Value, displayBoard, sender.Size, sender.Dpi);
        }
    }

    private void DisposeFrameBuffer()
    {
        _frameBuffer?.Dispose();
        _frameBuffer = null;
        _frameBufferWidth = 0;
        _frameBufferHeight = 0;
        _frameBufferDpi = 0;
        _frameBufferStaticStale = true;
    }

    private bool UsesGemAnimatedOverlay(bool highlightActive) =>
        _settings?.SkinValue == GameSettings.Skin.Gems
        && _settings.AnimationsEnabled
        && (highlightActive || _selectionAnimCoasting);

    private bool TryPaintIdleFromStaticBuffer(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        bool highlightActive,
        Size controlSize,
        float dpi)
    {
        if (_frameBufferStaticStale || _frameBuffer is null)
        {
            return false;
        }

        BlitFrameBuffer(ds, controlSize);

        if (UsesGemAnimatedOverlay(highlightActive))
        {
            EraseOverlayCellBackgrounds(ds, layout, displayBoard);
            PaintDynamicIdleCells(ds, layout, displayBoard, highlightActive);
        }
        else if (highlightActive)
        {
            PaintHighlightedIdleCells(ds, layout, displayBoard);
        }

        return true;
    }

    private void EraseOverlayCellBackgrounds(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard)
    {
        var bg = _settings!.BackgroundColor();
        for (int y = 0; y < displayBoard.Height; y++)
        {
            for (int x = 0; x < displayBoard.Width; x++)
            {
                if (!IsOverlayCell(x, y))
                {
                    continue;
                }

                var rect = layout.GetCellRect(x, y);
                ds.FillRectangle((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height, bg);
            }
        }
    }

    private void SyncStaticFrameBuffer(
        CanvasDrawingSession screenSession,
        BoardLayout layout,
        Board displayBoard,
        Size controlSize,
        float dpi)
    {
        if (!_frameBufferStaticStale)
        {
            return;
        }

        EnsureFrameBuffer(screenSession.Device, controlSize, dpi);
        if (_frameBuffer is null)
        {
            return;
        }

        var bg = _settings!.BackgroundColor();
        using var bufferDs = _frameBuffer.CreateDrawingSession();
        bufferDs.Clear(bg);
        PaintStaticIdleCells(bufferDs, layout, displayBoard, highlightActive: false);
        _frameBufferStaticStale = false;
    }

    private void EnsureFrameBuffer(ICanvasResourceCreator resourceCreator, Size size, float dpi)
    {
        float width = (float)Math.Ceiling(size.Width);
        float height = (float)Math.Ceiling(size.Height);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_frameBuffer is not null
            && Math.Abs(_frameBufferWidth - width) < 0.5f
            && Math.Abs(_frameBufferHeight - height) < 0.5f
            && Math.Abs(_frameBufferDpi - dpi) < 0.5f)
        {
            return;
        }

        DisposeFrameBuffer();
        _frameBuffer = new CanvasRenderTarget(resourceCreator, width, height, dpi);
        _frameBufferWidth = width;
        _frameBufferHeight = height;
        _frameBufferDpi = dpi;
    }

    private void CaptureFrameBufferBeforeAnimation(Size controlSize, float dpi)
    {
        if (_board is null || _settings is null)
        {
            return;
        }

        var layout = ComputeLayout(controlSize);
        if (layout is null)
        {
            return;
        }

        EnsureFrameBuffer(BoardCanvas.Device, controlSize, dpi);
        if (_frameBuffer is null)
        {
            return;
        }

        var bg = _settings.BackgroundColor();
        using var bufferDs = _frameBuffer.CreateDrawingSession();
        bufferDs.Clear(bg);
        PaintStaticIdleCells(bufferDs, layout.Value, _board, highlightActive: false);
    }

    private void ClearBoardLetterboxMargins(CanvasDrawingSession ds, BoardLayout layout)
    {
        var bg = _settings!.BackgroundColor();
        float boardRight = layout.OffsetX + layout.BoardWidth;
        float boardBottom = layout.OffsetY + layout.BoardHeight;
        float canvasW = _frameBufferWidth;
        float canvasH = _frameBufferHeight;

        if (layout.OffsetX > 0)
        {
            ds.FillRectangle(0, 0, layout.OffsetX, canvasH, bg);
        }

        if (boardRight < canvasW)
        {
            ds.FillRectangle(boardRight, 0, canvasW - boardRight, canvasH, bg);
        }

        if (layout.OffsetY > 0)
        {
            ds.FillRectangle(layout.OffsetX, 0, layout.BoardWidth, layout.OffsetY, bg);
        }

        if (boardBottom < canvasH)
        {
            ds.FillRectangle(layout.OffsetX, boardBottom, layout.BoardWidth, canvasH - boardBottom, bg);
        }
    }

    private bool UsesFullBoardAnimRepaint() =>
        _settings?.SkinValue == GameSettings.Skin.Classic;

    private bool UsesTightAnimSeamFix() =>
        _settings?.SkinValue is GameSettings.Skin.Modern
            or GameSettings.Skin.Blockcraft
            or GameSettings.Skin.Bricks;

    private int AnimRepaintCellPadding() => UsesTightAnimSeamFix() ? 1 : 0;

    private float AnimClearInsetPx() => UsesTightAnimSeamFix() ? 1.5f : 0f;

    private void PaintIdleBoard(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        bool highlightActive)
    {
        PaintStaticIdleCells(ds, layout, displayBoard, highlightActive);
        if (UsesGemAnimatedOverlay(highlightActive))
        {
            PaintDynamicIdleCells(ds, layout, displayBoard, highlightActive);
        }
        else if (highlightActive)
        {
            PaintHighlightedIdleCells(ds, layout, displayBoard);
        }
    }

    private void PaintHighlightedIdleCells(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard)
    {
        if (_highlightedGroup is null)
        {
            return;
        }

        foreach (var p in _highlightedGroup.Points)
        {
            int color = displayBoard.Get(p.X, p.Y);
            if (color == Board.Empty)
            {
                continue;
            }

            DrawBoardCell(ds, layout, p.X, p.Y, color, highlighted: true, coasting: false);
        }
    }

    private void PaintStaticIdleCells(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        bool highlightActive)
    {
        for (int y = 0; y < displayBoard.Height; y++)
        {
            for (int x = 0; x < displayBoard.Width; x++)
            {
                if (IsOverlayCell(x, y))
                {
                    continue;
                }

                int color = displayBoard.Get(x, y);
                if (color == Board.Empty)
                {
                    continue;
                }

                DrawBoardCell(ds, layout, x, y, color, highlighted: false, coasting: false);
            }
        }
    }

    private void PaintDynamicIdleCells(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        bool highlightActive)
    {
        for (int y = 0; y < displayBoard.Height; y++)
        {
            for (int x = 0; x < displayBoard.Width; x++)
            {
                int color = displayBoard.Get(x, y);
                if (color == Board.Empty)
                {
                    continue;
                }

                bool highlighted = highlightActive && IsInGroup(x, y, _highlightedGroup);
                bool coasting = _selectionAnimCoasting && IsInCoastingGroup(x, y);
                if (!highlighted && !coasting)
                {
                    continue;
                }

                if (!highlighted && coasting && !SelectionSpinNeedsFullDraw())
                {
                    continue;
                }

                DrawBoardCell(ds, layout, x, y, color, highlighted, coasting);
            }
        }
    }

    private bool IsOverlayCell(int x, int y)
    {
        if (IsInGroup(x, y, _highlightedGroup))
        {
            return true;
        }

        return _selectionAnimCoasting && IsInCoastingGroup(x, y);
    }

    private static bool IsInGroup(int x, int y, Board.Group? group)
    {
        if (group is null)
        {
            return false;
        }

        foreach (var p in group.Points)
        {
            if (p.X == x && p.Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private bool SelectionSpinNeedsFullDraw()
    {
        float angleNorm = _gemSpinDegrees % 360f;
        if (angleNorm < 0f)
        {
            angleNorm += 360f;
        }

        return angleNorm >= 0.5f && angleNorm <= 359.5f;
    }

    private void PaintFullBoardAnimFrame(CanvasDrawingSession ds, BoardLayout layout, Board displayBoard)
    {
        ds.Clear(_settings!.BackgroundColor());
        PaintCellsInBounds(ds, layout, displayBoard, 0, 0, displayBoard.Width - 1, displayBoard.Height - 1);
        if (_animator.CurrentPhase == BoardMoveAnimator.AnimationPhase.Shatter)
        {
            DrawParticles(ds, layout);
        }
        else
        {
            _lastParticlePixelBounds = null;
            _shatterParticleFootprint = null;
        }
    }

    private void PaintAnimatingBoard(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        Size controlSize,
        float dpi)
    {
        if (_frameBuffer is null)
        {
            CaptureFrameBufferBeforeAnimation(controlSize, dpi);
        }

        if (_frameBuffer is null)
        {
            PaintFullBoardAnimFrame(ds, layout, displayBoard);
            return;
        }

        using (var bufferDs = _frameBuffer.CreateDrawingSession())
        {
            PaintAnimatingBoardCore(bufferDs, layout, displayBoard);
        }

        BlitFrameBuffer(ds, controlSize);
    }

    private void BlitFrameBuffer(CanvasDrawingSession ds, Size controlSize)
    {
        if (_frameBuffer is null)
        {
            return;
        }

        ds.Clear(_settings!.BackgroundColor());
        float destW = (float)Math.Ceiling(controlSize.Width);
        float destH = (float)Math.Ceiling(controlSize.Height);
        var source = new Rect(0, 0, _frameBufferWidth, _frameBufferHeight);
        var dest = new Rect(0, 0, destW, destH);
        var interpolation = Math.Abs(destW - _frameBufferWidth) < 0.5f && Math.Abs(destH - _frameBufferHeight) < 0.5f
            ? CanvasImageInterpolation.NearestNeighbor
            : CanvasImageInterpolation.Linear;
        ds.DrawImage(_frameBuffer, source, dest, 1f, interpolation);
    }

    private void PaintAnimatingBoardCore(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard)
    {
        ClearBoardLetterboxMargins(ds, layout);

        switch (_animator.CurrentPhase)
        {
            case BoardMoveAnimator.AnimationPhase.Shatter:
            {
                var particleClear = ParticleClearRegion(layout);
                if (particleClear is not null)
                {
                    _shatterParticleFootprint = UnionRects(_shatterParticleFootprint, particleClear);
                }

                int[] bounds = ShatterRepaintCellBounds(layout, displayBoard, particleClear);
                RepaintAnimRegion(
                    ds,
                    layout,
                    displayBoard,
                    bounds,
                    GrowPixelRect(UnionPixelClear(bounds, layout, particleClear)));
                DrawParticles(ds, layout);
                break;
            }
            case BoardMoveAnimator.AnimationPhase.Fall:
            {
                _lastParticlePixelBounds = null;
                int[] bounds = _animator.FallDirtyCellBounds();
                bounds = UnionCellBounds(
                    bounds,
                    PixelRectToCellBounds(_shatterParticleFootprint, layout, displayBoard.Width, displayBoard.Height));
                _shatterParticleFootprint = null;
                RepaintAnimRegion(ds, layout, displayBoard, bounds, GrowPixelRect(CellBoundsToPixelRect(bounds, layout)));
                break;
            }
            case BoardMoveAnimator.AnimationPhase.Slide:
            {
                _lastParticlePixelBounds = null;
                int[] bounds = _animator.SlideDirtyCellBounds();
                RepaintAnimRegion(ds, layout, displayBoard, bounds, GrowPixelRect(CellBoundsToPixelRect(bounds, layout)));
                break;
            }
        }
    }

    private void RepaintAnimRegion(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        int[] bounds,
        Rect? clearRect)
    {
        if (bounds.Length < 4)
        {
            return;
        }

        int[] paintBounds = ExpandCellBounds(
            bounds,
            AnimRepaintCellPadding(),
            displayBoard.Width,
            displayBoard.Height);
        ClearAnimDirtyRect(ds, layout, InsetPixelRect(clearRect, AnimClearInsetPx()));
        PaintCellsInBounds(ds, layout, displayBoard, paintBounds);
    }

    private static int[] ExpandCellBounds(int[] bounds, int pad, int boardW, int boardH)
    {
        if (bounds.Length < 4 || pad <= 0)
        {
            return bounds;
        }

        return
        [
            Math.Max(0, bounds[0] - pad),
            Math.Max(0, bounds[1] - pad),
            Math.Min(boardW - 1, bounds[2] + pad),
            Math.Min(boardH - 1, bounds[3] + pad)
        ];
    }

    private static Rect? InsetPixelRect(Rect? rect, float inset)
    {
        if (rect is null || inset <= 0f)
        {
            return rect;
        }

        var value = rect.Value;
        float width = (float)value.Width - inset * 2f;
        float height = (float)value.Height - inset * 2f;
        if (width <= 0f || height <= 0f)
        {
            return rect;
        }

        return new Rect(value.X + inset, value.Y + inset, width, height);
    }

    private void PaintCellsInBounds(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        int[] bounds)
    {
        if (bounds.Length < 4)
        {
            return;
        }

        PaintCellsInBounds(ds, layout, displayBoard, bounds[0], bounds[1], bounds[2], bounds[3]);
    }

    private void PaintCellsInBounds(
        CanvasDrawingSession ds,
        BoardLayout layout,
        Board displayBoard,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (_animator.IsRemovedCell(x, y))
                {
                    continue;
                }

                int color = displayBoard.Get(x, y);
                if (color == Board.Empty)
                {
                    continue;
                }

                if (_animator.IsDynamicCell(x, y, displayBoard))
                {
                    DrawAnimatedCell(ds, layout, x, y, color);
                }
                else
                {
                    DrawAnimRestCell(ds, layout, x, y, color);
                }
            }
        }
    }

    private void DrawAnimRestCell(CanvasDrawingSession ds, BoardLayout layout, int x, int y, int color)
    {
        var rect = layout.GetCellRect(x, y);
        TileRenderer.DrawCell(
            ds,
            (float)rect.X,
            (float)rect.Y,
            (float)rect.Width,
            color,
            _settings!,
            highlighted: false);
    }

    private void DrawAnimatedCell(CanvasDrawingSession ds, BoardLayout layout, int x, int y, int color)
    {
        float xOffCols = _animator.XOffsetColumns(x);
        float yOffCells = _animator.YOffsetCells(x, y);
        float px = layout.OffsetX + MathF.Round((x + xOffCols) * layout.CellSize);
        float py = layout.OffsetY + MathF.Round((y + yOffCells) * layout.CellSize);
        TileRenderer.DrawCell(ds, px, py, layout.CellSize, color, _settings!, highlighted: false);
    }

    private void DrawBoardCell(
        CanvasDrawingSession ds,
        BoardLayout layout,
        int x,
        int y,
        int color,
        bool highlighted,
        bool coasting)
    {
        var rect = layout.GetCellRect(x, y);
        float spin = 0f;
        if (_settings!.SkinValue == GameSettings.Skin.Gems
            && _settings.AnimationsEnabled
            && (highlighted || coasting))
        {
            spin = _gemSpinDegrees;
        }

        TileRenderer.DrawCell(
            ds,
            (float)rect.X,
            (float)rect.Y,
            (float)rect.Width,
            color,
            _settings,
            highlighted,
            spin);
    }

    private int[] ShatterRepaintCellBounds(BoardLayout layout, Board drawBoard, Rect? particleClear)
    {
        int[] shatter = _animator.ShatterDirtyCellBounds();
        int[] particles = PixelRectToCellBounds(particleClear, layout, drawBoard.Width, drawBoard.Height);
        return UnionCellBounds(shatter, particles);
    }

    private Rect? ParticleClearRegion(BoardLayout layout)
    {
        var current = _animator.ParticlePixelBounds(layout.OffsetX, layout.OffsetY, layout.CellSize);
        var clear = UnionRects(current, _lastParticlePixelBounds);
        _lastParticlePixelBounds = current;
        if (clear is null)
        {
            return null;
        }

        float pad = layout.CellSize / 3f;
        return new Rect(
            clear.Value.X - pad,
            clear.Value.Y - pad,
            clear.Value.Width + pad * 2,
            clear.Value.Height + pad * 2);
    }

    private static Rect? UnionPixelClear(int[] bounds, BoardLayout layout, Rect? extraClear) =>
        GrowPixelRect(UnionRects(GrowPixelRect(CellBoundsToPixelRect(bounds, layout)), extraClear));

    private static Rect? GrowPixelRect(Rect? rect)
    {
        if (rect is null)
        {
            return null;
        }

        const float grow = 1f;
        var value = rect.Value;
        return new Rect(value.X - grow, value.Y - grow, value.Width + grow * 2, value.Height + grow * 2);
    }

    private static Rect? UnionRects(Rect? a, Rect? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        float x1 = (float)Math.Min(a.Value.X, b.Value.X);
        float y1 = (float)Math.Min(a.Value.Y, b.Value.Y);
        float x2 = (float)Math.Max(a.Value.X + a.Value.Width, b.Value.X + b.Value.Width);
        float y2 = (float)Math.Max(a.Value.Y + a.Value.Height, b.Value.Y + b.Value.Height);
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    private static int[] UnionCellBounds(int[] a, int[] b)
    {
        if (a.Length < 4)
        {
            return b.Length < 4 ? [] : (int[])b.Clone();
        }

        if (b.Length < 4)
        {
            return (int[])a.Clone();
        }

        return
        [
            Math.Min(a[0], b[0]),
            Math.Min(a[1], b[1]),
            Math.Max(a[2], b[2]),
            Math.Max(a[3], b[3])
        ];
    }

    private static int[] PixelRectToCellBounds(Rect? rect, BoardLayout layout, int boardW, int boardH)
    {
        if (rect is null || boardW <= 0 || boardH <= 0)
        {
            return [];
        }

        var value = rect.Value;
        int minX = (int)Math.Floor((value.X - layout.OffsetX) / layout.CellSize);
        int minY = (int)Math.Floor((value.Y - layout.OffsetY) / layout.CellSize);
        int maxX = (int)Math.Floor((value.X + value.Width - 1 - layout.OffsetX) / layout.CellSize);
        int maxY = (int)Math.Floor((value.Y + value.Height - 1 - layout.OffsetY) / layout.CellSize);
        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(boardW - 1, maxX);
        maxY = Math.Min(boardH - 1, maxY);
        if (minX > maxX || minY > maxY)
        {
            return [];
        }

        return [minX, minY, maxX, maxY];
    }

    private static Rect? CellBoundsToPixelRect(int[] bounds, BoardLayout layout)
    {
        if (bounds.Length < 4)
        {
            return null;
        }

        return CellBoundsToPixelRect(bounds[0], bounds[1], bounds[2], bounds[3], layout);
    }

    private static Rect CellBoundsToPixelRect(int minX, int minY, int maxX, int maxY, BoardLayout layout)
    {
        float x = layout.OffsetX + minX * layout.CellSize;
        float y = layout.OffsetY + minY * layout.CellSize;
        float w = (maxX - minX + 1) * layout.CellSize;
        float h = (maxY - minY + 1) * layout.CellSize;
        return new Rect(x, y, w, h);
    }

    private void ClearAnimDirtyRect(CanvasDrawingSession ds, BoardLayout layout, Rect? dirty)
    {
        if (dirty is null || dirty.Value.Width <= 0 || dirty.Value.Height <= 0)
        {
            return;
        }

        var value = dirty.Value;
        float dirtyX = (float)value.X;
        float dirtyY = (float)value.Y;
        float dirtyW = (float)value.Width;
        float dirtyH = (float)value.Height;
        float boardLeft = layout.OffsetX;
        float boardRight = layout.OffsetX + layout.BoardWidth;
        float boardTop = layout.OffsetY;
        float boardBottom = layout.OffsetY + layout.BoardHeight;
        var boardBg = _settings!.BackgroundColor();

        float dirtyRight = dirtyX + dirtyW;
        float dirtyBottom = dirtyY + dirtyH;

        if (dirtyX < boardLeft)
        {
            float w = Math.Min(boardLeft - dirtyX, dirtyW);
            ds.FillRectangle(dirtyX, dirtyY, w, dirtyH, boardBg);
        }

        if (dirtyRight > boardRight)
        {
            float x = Math.Max(boardRight, dirtyX);
            ds.FillRectangle(x, dirtyY, dirtyRight - x, dirtyH, boardBg);
        }

        float ix0 = Math.Max(dirtyX, boardLeft);
        float ix1 = Math.Min(dirtyRight, boardRight);
        float iy0 = Math.Max(dirtyY, boardTop);
        float iy1 = Math.Min(dirtyBottom, boardBottom);
        if (ix1 > ix0 && iy1 > iy0)
        {
            ds.FillRectangle(ix0, iy0, ix1 - ix0, iy1 - iy0, boardBg);
        }
    }

    private void DrawParticles(CanvasDrawingSession ds, BoardLayout layout)
    {
        foreach (var particle in _animator.Particles)
        {
            float px = layout.OffsetX + MathF.Round(particle.CellX * layout.CellSize);
            float py = layout.OffsetY + MathF.Round(particle.CellY * layout.CellSize);
            float half = particle.Size / 2f;
            var color = particle.Color;
            byte alpha = (byte)(particle.Alpha * color.A);
            var fill = Color.FromArgb(alpha, color.R, color.G, color.B);
            var center = new Vector2(px, py);
            ds.Transform = Matrix3x2.CreateRotation(particle.Rotation * MathF.PI / 180f, center);
            ds.FillRectangle(px - half, py - half, particle.Size, particle.Size, fill);
            ds.Transform = Matrix3x2.Identity;
        }
    }

    private void BoardCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_board is null || _settings is null)
        {
            return;
        }

        if (_animator.IsRunning)
        {
            _animator.Skip();
            return;
        }

        var point = e.GetCurrentPoint(BoardCanvas).Position;
        var layout = ComputeLayout(BoardCanvas.Size);
        if (layout is null)
        {
            return;
        }

        if (!layout.Value.TryCellAt(point, out int cellX, out int cellY))
        {
            ClearHighlight();
            return;
        }

        var group = _board.FindGroupAt(cellX, cellY);
        if (group is null)
        {
            ClearHighlight();
            return;
        }

        if (_highlightedGroup is not null && GroupsEqual(_highlightedGroup, group))
        {
            GroupConfirmed?.Invoke(group);
        }
        else
        {
            SetHighlightedGroup(group);
        }
    }

    private static bool GroupsEqual(Board.Group a, Board.Group b)
    {
        if (a.Size != b.Size || a.Color != b.Color)
        {
            return false;
        }

        var setA = a.Points.Select(p => (p.X, p.Y)).ToHashSet();
        return b.Points.All(p => setA.Contains((p.X, p.Y)));
    }

    private BoardLayout? ComputeLayout(Size controlSize)
    {
        if (_board is null || controlSize.Width <= 0 || controlSize.Height <= 0)
        {
            return null;
        }

        float cellSize = Math.Min(
            (float)controlSize.Width / _board.Width,
            (float)controlSize.Height / _board.Height);
        cellSize = Math.Max(8f, (float)Math.Floor(cellSize));
        float boardW = cellSize * _board.Width;
        float boardH = cellSize * _board.Height;
        float offsetX = ((float)controlSize.Width - boardW) / 2f;
        float offsetY = ((float)controlSize.Height - boardH) / 2f;
        return new BoardLayout(offsetX, offsetY, cellSize, _board.Width, _board.Height);
    }

    private void SelectionSpinTimer_Tick(object? sender, object e)
    {
        long now = Environment.TickCount64;
        if (_lastSpinTick == 0)
        {
            _lastSpinTick = now;
        }

        float deltaSec = Math.Min((now - _lastSpinTick) / 1000f, 0.1f);
        _lastSpinTick = now;
        _gemSpinDegrees += 187.5f * deltaSec;
        if (_selectionAnimCoasting && _gemSpinDegrees >= _coastTarget)
        {
            CommitOverlayGroupsToStaticBuffer(_coastingGroups);
            _gemSpinDegrees = 0f;
            _selectionAnimCoasting = false;
            _coastingGroups.Clear();
            _coastTarget = 0f;
        }

        bool selectionActive = _settings?.SkinValue == GameSettings.Skin.Gems
            && _settings.AnimationsEnabled
            && _highlightedGroup is not null
            && !_animator.IsRunning;

        if (!selectionActive && !_selectionAnimCoasting)
        {
            StopSelectionSpin();
        }

        BoardCanvas.Invalidate();
    }

    private void BeginSelectionAnimationCoastIfNeeded()
    {
        if (_highlightedGroup is null
            || _settings is null
            || !_settings.AnimationsEnabled
            || _settings.SkinValue != GameSettings.Skin.Gems
            || _animator.IsRunning)
        {
            return;
        }

        float normalized = _gemSpinDegrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        if (normalized < 0.5f)
        {
            _gemSpinDegrees = 0f;
            return;
        }

        _coastingGroups.Add(_highlightedGroup);
        _selectionAnimCoasting = true;
        float nextTarget = (float)(Math.Floor(_gemSpinDegrees / 360.0) + 1.0) * 360f;
        if (nextTarget > _coastTarget)
        {
            _coastTarget = nextTarget;
        }
    }

    private bool IsInCoastingGroup(int x, int y)
    {
        foreach (var group in _coastingGroups)
        {
            foreach (var p in group.Points)
            {
                if (p.X == x && p.Y == y)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CommitOverlayGroupsToStaticBuffer(IReadOnlyList<Board.Group> groups)
    {
        if (_frameBuffer is null || _board is null || groups.Count == 0)
        {
            return;
        }

        var layout = ComputeLayout(BoardCanvas.Size);
        if (layout is null)
        {
            return;
        }

        using var bufferDs = _frameBuffer.CreateDrawingSession();
        foreach (var group in groups)
        {
            foreach (var p in group.Points)
            {
                int color = _board.Get(p.X, p.Y);
                if (color == Board.Empty)
                {
                    continue;
                }

                DrawBoardCell(bufferDs, layout.Value, p.X, p.Y, color, highlighted: false, coasting: false);
            }
        }
    }

    private void UpdateSelectionSpinTimer()
    {
        bool shouldAnimateSelection = _settings?.SkinValue == GameSettings.Skin.Gems
            && _settings.AnimationsEnabled
            && _highlightedGroup is not null
            && !_animator.IsRunning;
        bool shouldRun = shouldAnimateSelection || _selectionAnimCoasting;

        if (shouldRun)
        {
            if (!_selectionSpinTimer.IsEnabled)
            {
                _lastSpinTick = 0;
                _selectionSpinTimer.Start();
            }
        }
        else
        {
            StopSelectionSpin();
        }
    }

    private void StopSelectionSpin()
    {
        _selectionSpinTimer.Stop();
        if (!_selectionAnimCoasting)
        {
            _gemSpinDegrees = 0f;
            _coastTarget = 0f;
            _coastingGroups.Clear();
        }

        _lastSpinTick = 0;
    }

    private readonly record struct BoardLayout(float OffsetX, float OffsetY, float CellSize, int Width, int Height)
    {
        public float BoardWidth => CellSize * Width;

        public float BoardHeight => CellSize * Height;

        public Rect GetCellRect(int x, int y) =>
            new(OffsetX + x * CellSize, OffsetY + y * CellSize, CellSize, CellSize);

        public bool TryCellAt(Point point, out int cellX, out int cellY)
        {
            float localX = (float)point.X - OffsetX;
            float localY = (float)point.Y - OffsetY;
            if (localX < 0 || localY < 0)
            {
                cellX = cellY = 0;
                return false;
            }

            cellX = (int)(localX / CellSize);
            cellY = (int)(localY / CellSize);
            return cellX >= 0 && cellX < Width && cellY >= 0 && cellY < Height;
        }
    }
}
