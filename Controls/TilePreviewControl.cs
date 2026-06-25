using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.Model;
using SameGame.UI;
using Windows.Foundation;

namespace SameGame.Controls;

public sealed class TilePreviewControl : UserControl
{
    private const double DefaultSize = 36;

    private readonly CanvasControl _canvas;
    private GameSettings? _settings;
    private int _colorIndex;

    public TilePreviewControl()
    {
        _canvas = new CanvasControl();
        _canvas.Draw += OnDraw;
        Content = _canvas;
        Loaded += (_, _) => SyncCanvasSize();
        SizeChanged += (_, _) => SyncCanvasSize();
    }

    public void Configure(GameSettings settings, int colorIndex)
    {
        _settings = (GameSettings)settings.Clone();
        _colorIndex = colorIndex;
        InvalidateCanvas();
    }

    /// <summary>
    /// Binds to a live settings instance for immediate preview updates (e.g. Advanced Options).
    /// </summary>
    public void Bind(GameSettings settings, int colorIndex)
    {
        _settings = settings;
        _colorIndex = colorIndex;
        InvalidateCanvas();
    }

    public void Refresh() => InvalidateCanvas();

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsNaN(Width) ? DefaultSize : Width;
        double height = double.IsNaN(Height) ? DefaultSize : Height;
        var size = new Size(width, height);
        _canvas.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _canvas.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    private void SyncCanvasSize()
    {
        double width = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? DefaultSize : Width);
        double height = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? DefaultSize : Height);
        _canvas.Width = width;
        _canvas.Height = height;
        InvalidateCanvas();
    }

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
