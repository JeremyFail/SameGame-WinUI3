using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace SameGame.UI;

/// <summary>
/// Advanced Options body grid. Uses the preferred page height when space allows,
/// and shrinks the tab scroll viewport when the hosting dialog is vertically constrained.
/// </summary>
internal sealed class SettingsDialogShellGrid : Grid
{
    private ScrollViewer? _pageScroll;
    private ContentDialog? _hostDialog;
    private double _resolvedPageHeight = SettingsLayoutHelper.PageMaxHeight;

    public void AttachPageScroll(ScrollViewer pageScroll) => _pageScroll = pageScroll;

    public void AttachHostDialog(ContentDialog dialog) => _hostDialog = dialog;

    public void ApplyScrollConstraintsToCurrentPage() => ApplyScrollHeight(_resolvedPageHeight);

    protected override Size MeasureOverride(Size availableSize)
    {
        _resolvedPageHeight = ResolvePageHeight(availableSize);
        ApplyScrollHeight(_resolvedPageHeight);

        double width = double.IsInfinity(availableSize.Width) ? SettingsLayoutHelper.DialogShellWidth : availableSize.Width;
        var constraint = new Size(width, _resolvedPageHeight);
        return base.MeasureOverride(constraint);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _resolvedPageHeight = ResolvePageHeightForArrange(finalSize);
        ApplyScrollHeight(_resolvedPageHeight);
        return base.ArrangeOverride(new Size(finalSize.Width, _resolvedPageHeight));
    }

    private static double ResolvePageHeight(Size availableSize)
    {
        double preferred = SettingsLayoutHelper.PageMaxHeight;

        if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
        {
            return Math.Min(preferred, availableSize.Height);
        }

        return preferred;
    }

    private double ResolvePageHeightForArrange(Size finalSize)
    {
        double preferred = SettingsLayoutHelper.PageMaxHeight;

        if (!double.IsInfinity(finalSize.Height) && finalSize.Height > 0)
        {
            return Math.Min(preferred, finalSize.Height);
        }

        if (_hostDialog is not null && _hostDialog.ActualHeight > 0)
        {
            const double dialogChrome = 128;
            double fromDialog = _hostDialog.ActualHeight - dialogChrome;
            if (fromDialog < preferred)
            {
                return Math.Max(160, fromDialog);
            }
        }

        var root = App.DialogXamlRoot;
        if (root is not null)
        {
            const double reservedChrome = 132;
            double fromWindow = root.Size.Height - reservedChrome;
            if (fromWindow < preferred)
            {
                return Math.Max(160, fromWindow);
            }
        }

        return preferred;
    }

    private void ApplyScrollHeight(double pageHeight)
    {
        Height = pageHeight;
        MaxHeight = pageHeight;

        if (pageHeight >= SettingsLayoutHelper.PageMaxHeight - 0.5)
        {
            MinHeight = SettingsLayoutHelper.PageMaxHeight;
        }
        else
        {
            MinHeight = pageHeight;
        }

        if (_pageScroll is null)
        {
            return;
        }

        _pageScroll.Height = pageHeight;
        _pageScroll.MaxHeight = pageHeight;

        if (pageHeight >= SettingsLayoutHelper.PageMaxHeight - 0.5)
        {
            _pageScroll.MinHeight = SettingsLayoutHelper.PageMaxHeight;
        }
        else
        {
            _pageScroll.ClearValue(MinHeightProperty);
        }
    }
}
