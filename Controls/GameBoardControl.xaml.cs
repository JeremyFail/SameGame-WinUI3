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

/// <summary>
/// Win2D-backed interactive game board with tile rendering, selection highlighting, and move animations.
/// </summary>
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

    /// <summary>
    /// Raised when the user confirms a highlighted group with a second click.
    /// </summary>
    public event Action<Board.Group>? GroupConfirmed;

    /// <summary>
    /// Raised when a new tile group becomes highlighted.
    /// </summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Raised when the current selection is cleared.
    /// </summary>
    public event Action? SelectionCleared;

    /// <summary>
    /// Initializes the control, timers, and canvas draw handler.
    /// </summary>
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

    /// <summary>
    /// Gets whether a remove/fall/slide animation is currently running.
    /// </summary>
    public bool IsAnimating => _animator.IsRunning;

    /// <summary>
    /// Gets the currently highlighted tile group, if any.
    /// </summary>
    public Board.Group? HighlightedGroup => _highlightedGroup;

    /// <summary>
    /// Binds a new board and settings, resetting animation and selection state.
    /// </summary>
    /// <param name="board">Board model to display.</param>
    /// <param name="settings">Rendering and animation settings.</param>
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

    /// <summary>
    /// Sets or clears the highlighted group and notifies selection listeners.
    /// </summary>
    /// <param name="group">Group to highlight, or null to clear selection.</param>
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

    /// <summary>
    /// Clears the current tile group highlight.
    /// </summary>
    public void ClearHighlight() => SetHighlightedGroup(null);

    /// <summary>
    /// Marks cached static frames stale and refreshes selection animation state after settings change.
    /// </summary>
    public void SettingsChanged()
    {
        _frameBufferStaticStale = true;
        UpdateSelectionSpinTimer();
        BoardCanvas.Invalidate();
    }

    /// <summary>
    /// Plays the remove-group animation and invokes the callback when complete.
    /// </summary>
    /// <param name="group">Tile group being removed.</param>
    /// <param name="onComplete">Callback invoked after animation finishes or is skipped.</param>
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

    /// <summary>
    /// Main canvas draw handler: idle board, static buffer blit, or animated partial repaint.
    /// </summary>
    /// <param name="sender">The Win2D canvas control.</param>
    /// <param name="args">Draw event arguments providing the drawing session.</param>
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
            // Idle path: try cached static buffer, else full paint and sync buffer
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
            // Classic skin repaints the entire board each animation frame
            PaintFullBoardAnimFrame(ds, layout.Value, displayBoard);
        }
        else
        {
            // Other skins: update off-screen buffer then blit to screen
            PaintAnimatingBoard(ds, layout.Value, displayBoard, sender.Size, sender.Dpi);
        }
    }

    /// <summary>
    /// Releases the off-screen frame buffer and marks static cache stale.
    /// </summary>
    private void DisposeFrameBuffer()
    {
        _frameBuffer?.Dispose();
        _frameBuffer = null;
        _frameBufferWidth = 0;
        _frameBufferHeight = 0;
        _frameBufferDpi = 0;
        _frameBufferStaticStale = true;
    }

    /// <summary>
    /// Determines whether gem selection spin requires a dynamic overlay over the static buffer.
    /// </summary>
    /// <param name="highlightActive">True when a group is highlighted and no move animation runs.</param>
    /// <returns>True when gem skin animations should draw spinning overlay cells.</returns>
    private bool UsesGemAnimatedOverlay(bool highlightActive) =>
        _settings?.SkinValue == GameSettings.Skin.Gems
        && _settings.AnimationsEnabled
        && (highlightActive || _selectionAnimCoasting);

    /// <summary>
    /// Blits the cached static frame buffer and paints only dynamic overlay cells when applicable.
    /// </summary>
    /// <param name="ds">Screen drawing session.</param>
    /// <param name="layout">Computed board layout metrics.</param>
    /// <param name="displayBoard">Board state to render.</param>
    /// <param name="highlightActive">True when selection highlight should appear.</param>
    /// <param name="controlSize">Control size in DIPs.</param>
    /// <param name="dpi">Canvas DPI scale.</param>
    /// <returns>True when the idle frame was painted from cache.</returns>
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

    /// <summary>
    /// Fills background color over cells that will be redrawn by the gem spin overlay.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state defining overlay cell positions.</param>
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

    /// <summary>
    /// Rebuilds the static off-screen buffer when marked stale after idle paint.
    /// </summary>
    /// <param name="screenSession">Screen session used to obtain the canvas device.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state without highlight overlay.</param>
    /// <param name="controlSize">Control size in DIPs.</param>
    /// <param name="dpi">Canvas DPI scale.</param>
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

    /// <summary>
    /// Creates or reuses an off-screen render target matching the control size and DPI.
    /// </summary>
    /// <param name="resourceCreator">Canvas device used to allocate the render target.</param>
    /// <param name="size">Control size in DIPs.</param>
    /// <param name="dpi">Canvas DPI scale.</param>
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

    /// <summary>
    /// Snapshots the current idle board into the frame buffer before an animation starts.
    /// </summary>
    /// <param name="controlSize">Control size in DIPs.</param>
    /// <param name="dpi">Canvas DPI scale.</param>
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

    /// <summary>
    /// Paints background color into letterbox margins around the centered board.
    /// </summary>
    /// <param name="ds">Drawing session, typically the off-screen buffer.</param>
    /// <param name="layout">Board layout metrics.</param>
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

    /// <summary>
    /// Determines whether the classic skin requires a full-board repaint during animation.
    /// </summary>
    /// <returns>True for the classic tile skin.</returns>
    private bool UsesFullBoardAnimRepaint() =>
        _settings?.SkinValue == GameSettings.Skin.Classic;

    /// <summary>
    /// Determines whether the current skin uses tight seam fixes during animation repaints.
    /// </summary>
    /// <returns>True for modern, blockcraft, and bricks skins.</returns>
    private bool UsesTightAnimSeamFix() =>
        _settings?.SkinValue is GameSettings.Skin.Modern
            or GameSettings.Skin.Blockcraft
            or GameSettings.Skin.Bricks;

    /// <summary>
    /// Returns extra cell padding applied when repainting animated dirty regions.
    /// </summary>
    /// <returns>One cell of padding for tight-seam skins, zero otherwise.</returns>
    private int AnimRepaintCellPadding() => UsesTightAnimSeamFix() ? 1 : 0;

    /// <summary>
    /// Returns pixel inset applied when clearing animated dirty rectangles.
    /// </summary>
    /// <returns>1.5 pixels for tight-seam skins, zero otherwise.</returns>
    private float AnimClearInsetPx() => UsesTightAnimSeamFix() ? 1.5f : 0f;

    /// <summary>
    /// Paints all idle board cells, then dynamic or highlighted overlay cells as needed.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state to render.</param>
    /// <param name="highlightActive">True when selection highlight should appear.</param>
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

    /// <summary>
    /// Paints highlighted cells for non-gem skins during idle state.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state to read tile colors from.</param>
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

    /// <summary>
    /// Paints all non-overlay idle cells into the static buffer or screen.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state to render.</param>
    /// <param name="highlightActive">Unused; static cells never draw highlight state.</param>
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

    /// <summary>
    /// Paints gem spin overlay cells for highlighted or coasting groups.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state to render.</param>
    /// <param name="highlightActive">True when the current highlight should spin.</param>
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

    /// <summary>
    /// Determines whether a cell is drawn on the dynamic overlay rather than the static buffer.
    /// </summary>
    /// <param name="x">Cell column index.</param>
    /// <param name="y">Cell row index.</param>
    /// <returns>True when the cell belongs to the highlight or coasting group.</returns>
    private bool IsOverlayCell(int x, int y)
    {
        if (IsInGroup(x, y, _highlightedGroup))
        {
            return true;
        }

        return _selectionAnimCoasting && IsInCoastingGroup(x, y);
    }

    /// <summary>
    /// Tests whether a board cell belongs to the given tile group.
    /// </summary>
    /// <param name="x">Cell column index.</param>
    /// <param name="y">Cell row index.</param>
    /// <param name="group">Group to test membership against, or null.</param>
    /// <returns>True when the cell is part of the group.</returns>
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

    /// <summary>
    /// Determines whether the gem spin angle requires a full overlay redraw this frame.
    /// </summary>
    /// <returns>False at rest (near 0°); true while visibly rotated.</returns>
    private bool SelectionSpinNeedsFullDraw()
    {
        float angleNorm = _gemSpinDegrees % 360f;
        if (angleNorm < 0f)
        {
            angleNorm += 360f;
        }

        return angleNorm >= 0.5f && angleNorm <= 359.5f;
    }

    /// <summary>
    /// Repaints the entire board for classic-skin animation frames including shatter particles.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Animated board state from the animator.</param>
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

    /// <summary>
    /// Updates the off-screen buffer for partial animation repaint, then blits to the screen.
    /// </summary>
    /// <param name="ds">Screen drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Animated board state from the animator.</param>
    /// <param name="controlSize">Control size in DIPs.</param>
    /// <param name="dpi">Canvas DPI scale.</param>
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

    /// <summary>
    /// Blits the off-screen frame buffer to the screen with appropriate interpolation.
    /// </summary>
    /// <param name="ds">Screen drawing session.</param>
    /// <param name="controlSize">Control size in DIPs.</param>
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

    /// <summary>
    /// Repaints only dirty regions of the off-screen buffer for the current animation phase.
    /// </summary>
    /// <param name="ds">Off-screen buffer drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Animated board state from the animator.</param>
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
                // Shatter: expand dirty rect to include particles, then draw debris
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
                // Fall: repaint cells affected by gravity plus prior particle footprint
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
                // Slide: repaint columns that shifted horizontally
                _lastParticlePixelBounds = null;
                int[] bounds = _animator.SlideDirtyCellBounds();
                RepaintAnimRegion(ds, layout, displayBoard, bounds, GrowPixelRect(CellBoundsToPixelRect(bounds, layout)));
                break;
            }
        }
    }

    /// <summary>
    /// Clears and repaints a bounded cell region during animation.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Animated board state from the animator.</param>
    /// <param name="bounds">Dirty cell bounds as [minX, minY, maxX, maxY].</param>
    /// <param name="clearRect">Optional pixel rectangle to clear before repaint.</param>
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

    /// <summary>
    /// Expands cell bounds by a padding amount clamped to the board dimensions.
    /// </summary>
    /// <param name="bounds">Original bounds as [minX, minY, maxX, maxY].</param>
    /// <param name="pad">Cells to add on each side.</param>
    /// <param name="boardW">Board width in cells.</param>
    /// <param name="boardH">Board height in cells.</param>
    /// <returns>Padded bounds array, or the original when padding is zero or bounds invalid.</returns>
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

    /// <summary>
    /// Shrinks a pixel rectangle inward by the given inset, preserving the original when too small.
    /// </summary>
    /// <param name="rect">Optional pixel rectangle.</param>
    /// <param name="inset">Pixels to subtract from each side.</param>
    /// <returns>Inset rectangle, or the original when inset is zero or result would be non-positive.</returns>
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

    /// <summary>
    /// Paints cells within bounds given as a four-element array.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state to render.</param>
    /// <param name="bounds">Cell bounds as [minX, minY, maxX, maxY].</param>
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

    /// <summary>
    /// Paints cells within an inclusive cell rectangle, skipping removed and empty cells.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="displayBoard">Board state to render.</param>
    /// <param name="minX">Minimum column index.</param>
    /// <param name="minY">Minimum row index.</param>
    /// <param name="maxX">Maximum column index.</param>
    /// <param name="maxY">Maximum row index.</param>
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

    /// <summary>
    /// Draws a stationary cell during animation at its grid position.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="x">Cell column index.</param>
    /// <param name="y">Cell row index.</param>
    /// <param name="color">Tile color index.</param>
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

    /// <summary>
    /// Draws a cell at its animated offset during fall or slide phases.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="x">Logical cell column index.</param>
    /// <param name="y">Logical cell row index.</param>
    /// <param name="color">Tile color index.</param>
    private void DrawAnimatedCell(CanvasDrawingSession ds, BoardLayout layout, int x, int y, int color)
    {
        float xOffCols = _animator.XOffsetColumns(x);
        float yOffCells = _animator.YOffsetCells(x, y);
        float px = layout.OffsetX + MathF.Round((x + xOffCols) * layout.CellSize);
        float py = layout.OffsetY + MathF.Round((y + yOffCells) * layout.CellSize);
        TileRenderer.DrawCell(ds, px, py, layout.CellSize, color, _settings!, highlighted: false);
    }

    /// <summary>
    /// Draws a single board cell with optional gem spin for highlight or coast states.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="x">Cell column index.</param>
    /// <param name="y">Cell row index.</param>
    /// <param name="color">Tile color index.</param>
    /// <param name="highlighted">True when the cell is part of the active selection.</param>
    /// <param name="coasting">True when the cell is finishing a spin coast animation.</param>
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

    /// <summary>
    /// Combines shatter dirty cell bounds with particle footprint cell bounds.
    /// </summary>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="drawBoard">Board state used for dimension clamping.</param>
    /// <param name="particleClear">Optional pixel clear region for particles.</param>
    /// <returns>Union of shatter and particle cell bounds.</returns>
    private int[] ShatterRepaintCellBounds(BoardLayout layout, Board drawBoard, Rect? particleClear)
    {
        int[] shatter = _animator.ShatterDirtyCellBounds();
        int[] particles = PixelRectToCellBounds(particleClear, layout, drawBoard.Width, drawBoard.Height);
        return UnionCellBounds(shatter, particles);
    }

    /// <summary>
    /// Computes the padded pixel region that must be cleared for current shatter particles.
    /// </summary>
    /// <param name="layout">Board layout metrics.</param>
    /// <returns>Padded pixel rectangle covering current and previous particle bounds.</returns>
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

    /// <summary>
    /// Unions cell-bounds pixel clear region with an optional extra clear rectangle.
    /// </summary>
    /// <param name="bounds">Dirty cell bounds as [minX, minY, maxX, maxY].</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="extraClear">Optional additional pixel clear region.</param>
    /// <returns>Grown union pixel rectangle, or null when both inputs are empty.</returns>
    private static Rect? UnionPixelClear(int[] bounds, BoardLayout layout, Rect? extraClear) =>
        GrowPixelRect(UnionRects(GrowPixelRect(CellBoundsToPixelRect(bounds, layout)), extraClear));

    /// <summary>
    /// Expands a pixel rectangle by one pixel on each side to avoid seam artifacts.
    /// </summary>
    /// <param name="rect">Optional source rectangle.</param>
    /// <returns>Grown rectangle, or null when input is null.</returns>
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

    /// <summary>
    /// Returns the smallest axis-aligned rectangle containing both inputs.
    /// </summary>
    /// <param name="a">First optional rectangle.</param>
    /// <param name="b">Second optional rectangle.</param>
    /// <returns>Union rectangle, or whichever operand is non-null.</returns>
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

    /// <summary>
    /// Unions two cell-bound arrays into a single bounding box.
    /// </summary>
    /// <param name="a">First bounds as [minX, minY, maxX, maxY].</param>
    /// <param name="b">Second bounds as [minX, minY, maxX, maxY].</param>
    /// <returns>Union bounds, or a clone of the valid operand when the other is empty.</returns>
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

    /// <summary>
    /// Converts a pixel rectangle to inclusive cell bounds clamped to the board.
    /// </summary>
    /// <param name="rect">Optional pixel rectangle in canvas coordinates.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="boardW">Board width in cells.</param>
    /// <param name="boardH">Board height in cells.</param>
    /// <returns>Cell bounds array, or empty when conversion fails.</returns>
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

    /// <summary>
    /// Converts a four-element cell bounds array to a pixel rectangle.
    /// </summary>
    /// <param name="bounds">Cell bounds as [minX, minY, maxX, maxY].</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <returns>Pixel rectangle, or null when bounds are invalid.</returns>
    private static Rect? CellBoundsToPixelRect(int[] bounds, BoardLayout layout)
    {
        if (bounds.Length < 4)
        {
            return null;
        }

        return CellBoundsToPixelRect(bounds[0], bounds[1], bounds[2], bounds[3], layout);
    }

    /// <summary>
    /// Converts inclusive cell indices to a pixel rectangle on the canvas.
    /// </summary>
    /// <param name="minX">Minimum column index.</param>
    /// <param name="minY">Minimum row index.</param>
    /// <param name="maxX">Maximum column index.</param>
    /// <param name="maxY">Maximum row index.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <returns>Pixel rectangle covering the cell range.</returns>
    private static Rect CellBoundsToPixelRect(int minX, int minY, int maxX, int maxY, BoardLayout layout)
    {
        float x = layout.OffsetX + minX * layout.CellSize;
        float y = layout.OffsetY + minY * layout.CellSize;
        float w = (maxX - minX + 1) * layout.CellSize;
        float h = (maxY - minY + 1) * layout.CellSize;
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// Clears a dirty pixel rectangle, respecting board letterbox margins outside the grid.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
    /// <param name="dirty">Optional dirty rectangle in canvas coordinates.</param>
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

    /// <summary>
    /// Draws shatter phase particle sprites at their animated positions.
    /// </summary>
    /// <param name="ds">Drawing session.</param>
    /// <param name="layout">Board layout metrics.</param>
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

    /// <summary>
    /// Handles pointer press: skip animation, select group, or confirm highlighted group.
    /// </summary>
    /// <param name="sender">The canvas that received the pointer event.</param>
    /// <param name="e">Pointer routed event arguments.</param>
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

    /// <summary>
    /// Compares two tile groups for equal size, color, and point set.
    /// </summary>
    /// <param name="a">First group.</param>
    /// <param name="b">Second group.</param>
    /// <returns>True when both groups represent the same tiles.</returns>
    private static bool GroupsEqual(Board.Group a, Board.Group b)
    {
        if (a.Size != b.Size || a.Color != b.Color)
        {
            return false;
        }

        var setA = a.Points.Select(p => (p.X, p.Y)).ToHashSet();
        return b.Points.All(p => setA.Contains((p.X, p.Y)));
    }

    /// <summary>
    /// Computes centered board layout metrics for the current control size.
    /// </summary>
    /// <param name="controlSize">Available control size in DIPs.</param>
    /// <returns>Layout metrics, or null when the board or size is invalid.</returns>
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

    /// <summary>
    /// Advances gem spin angle each frame and commits coasting groups to the static buffer at rest.
    /// </summary>
    /// <param name="sender">The selection spin dispatcher timer.</param>
    /// <param name="e">Tick event arguments.</param>
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

    /// <summary>
    /// Starts a spin coast when deselecting a gem group mid-rotation so it finishes smoothly.
    /// </summary>
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

    /// <summary>
    /// Tests whether a cell belongs to any group currently coasting to rest.
    /// </summary>
    /// <param name="x">Cell column index.</param>
    /// <param name="y">Cell row index.</param>
    /// <returns>True when the cell is in a coasting group.</returns>
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

    /// <summary>
    /// Bakes coasting group tiles into the static frame buffer at their final unspun appearance.
    /// </summary>
    /// <param name="groups">Groups whose tiles should be committed to the static buffer.</param>
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

    /// <summary>
    /// Starts or stops the selection spin timer based on highlight and coast state.
    /// </summary>
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

    /// <summary>
    /// Stops the selection spin timer and resets spin state unless a coast is active.
    /// </summary>
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

    /// <summary>
    /// Cached board geometry: offsets, cell size, and helper coordinate conversions.
    /// </summary>
    /// <param name="OffsetX">Horizontal offset centering the board in the control.</param>
    /// <param name="OffsetY">Vertical offset centering the board in the control.</param>
    /// <param name="CellSize">Square cell edge length in DIPs.</param>
    /// <param name="Width">Board width in cells.</param>
    /// <param name="Height">Board height in cells.</param>
    private readonly record struct BoardLayout(float OffsetX, float OffsetY, float CellSize, int Width, int Height)
    {
        /// <summary>
        /// Gets the total board width in DIPs.
        /// </summary>
        public float BoardWidth => CellSize * Width;

        /// <summary>
        /// Gets the total board height in DIPs.
        /// </summary>
        public float BoardHeight => CellSize * Height;

        /// <summary>
        /// Returns the pixel rectangle for a cell at the given grid coordinates.
        /// </summary>
        /// <param name="x">Cell column index.</param>
        /// <param name="y">Cell row index.</param>
        /// <returns>Cell bounds in canvas coordinates.</returns>
        public Rect GetCellRect(int x, int y) =>
            new(OffsetX + x * CellSize, OffsetY + y * CellSize, CellSize, CellSize);

        /// <summary>
        /// Maps a canvas point to board cell coordinates when inside the grid.
        /// </summary>
        /// <param name="point">Pointer or hit-test position in canvas coordinates.</param>
        /// <param name="cellX">When successful, the cell column index.</param>
        /// <param name="cellY">When successful, the cell row index.</param>
        /// <returns>True when the point lies within the board grid.</returns>
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
