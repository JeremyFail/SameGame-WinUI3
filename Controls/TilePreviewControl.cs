using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.Model;
using SameGame.UI;
using Windows.Foundation;

namespace SameGame.Controls;

/// <summary>
/// Renders a single tile preview using the current game skin and color palette.
/// </summary>
public sealed class TilePreviewControl : UserControl
{
    private const double DefaultSize = 36;

    private readonly CanvasControl _canvas;
    private GameSettings? _settings;
    private int _colorIndex;

    /// <summary>
    /// Initializes a new tile preview control with a Win2D canvas child.
    /// </summary>
    public TilePreviewControl()
    {
        _canvas = new CanvasControl();
        _canvas.Draw += OnDraw;
        Content = _canvas;
        Loaded += (_, _) => SyncCanvasSize();
        SizeChanged += (_, _) => SyncCanvasSize();
    }

    /// <summary>
    /// Configures the preview from a cloned copy of the given settings and color index.
    /// </summary>
    /// <param name="settings">Game settings that define skin and palette.</param>
    /// <param name="colorIndex">Zero-based tile color index to render.</param>
    public void Configure(GameSettings settings, int colorIndex)
    {
        _settings = (GameSettings)settings.Clone();
        _colorIndex = colorIndex;
        InvalidateCanvas();
    }

    /// <summary>
    /// Binds to a live settings instance for immediate preview updates (e.g. Advanced Options).
    /// </summary>
    /// <param name="settings">Shared game settings instance to read from on each draw.</param>
    /// <param name="colorIndex">Zero-based tile color index to render.</param>
    public void Bind(GameSettings settings, int colorIndex)
    {
        _settings = settings;
        _colorIndex = colorIndex;
        InvalidateCanvas();
    }

    /// <summary>
    /// Requests a redraw of the preview canvas.
    /// </summary>
    public void Refresh() => InvalidateCanvas();

    /// <summary>
    /// Measures the control at its explicit or default size and forwards measurement to the canvas.
    /// </summary>
    /// <param name="availableSize">Available layout space from the parent.</param>
    /// <returns>The desired size of the preview control.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsNaN(Width) ? DefaultSize : Width;
        double height = double.IsNaN(Height) ? DefaultSize : Height;
        var size = new Size(width, height);
        _canvas.Measure(size);
        return size;
    }

    /// <summary>
    /// Arranges the internal canvas to fill the allocated final size.
    /// </summary>
    /// <param name="finalSize">Final size allocated by the parent layout pass.</param>
    /// <returns>The arranged size, equal to <paramref name="finalSize"/>.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _canvas.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    /// <summary>
    /// Synchronizes the canvas pixel dimensions with the control's actual or declared size.
    /// </summary>
    private void SyncCanvasSize()
    {
        double width = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? DefaultSize : Width);
        double height = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? DefaultSize : Height);
        _canvas.Width = width;
        _canvas.Height = height;
        InvalidateCanvas();
    }

    /// <summary>
    /// Invalidates the canvas on the UI thread, marshaling when called from a background thread.
    /// </summary>
    private void InvalidateCanvas()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            _canvas.Invalidate();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => _canvas.Invalidate());
        }
    }

    /// <summary>
    /// Draws a single tile cell centered in the preview canvas using <see cref="TileRenderer"/>.
    /// </summary>
    /// <param name="sender">The canvas control being painted.</param>
    /// <param name="args">Draw event arguments providing the drawing session.</param>
    private void OnDraw(CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
    {
        if (_settings is null)
        {
            return;
        }

        float size = (float)Math.Min(sender.ActualWidth, sender.ActualHeight);
        if (size <= 0)
        {
            size = (float)Math.Min(ActualWidth, ActualHeight);
        }

        if (size <= 0)
        {
            size = (float)DefaultSize;
        }

        args.DrawingSession.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        TileRenderer.DrawCell(args.DrawingSession, 0, 0, size, _colorIndex, _settings, false);
    }
}
