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

    /// <summary>
    /// Associates the tab page scroll viewer whose height this grid manages.
    /// </summary>
    /// <param name="pageScroll">The scroll viewer inside the active settings tab.</param>
    public void AttachPageScroll(ScrollViewer pageScroll) => _pageScroll = pageScroll;

    /// <summary>
    /// Associates the hosting content dialog used to derive available vertical space.
    /// </summary>
    /// <param name="dialog">The Advanced Options content dialog.</param>
    public void AttachHostDialog(ContentDialog dialog) => _hostDialog = dialog;

    /// <summary>
    /// Re-applies the last resolved scroll height to the attached page scroll viewer.
    /// </summary>
    public void ApplyScrollConstraintsToCurrentPage() => ApplyScrollHeight(_resolvedPageHeight);

    /// <summary>
    /// Measures the grid with a resolved page height and propagates constraints to the scroll viewer.
    /// </summary>
    /// <param name="availableSize">The available size proposed by the parent.</param>
    /// <returns>The desired size of the grid.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        _resolvedPageHeight = ResolvePageHeight(availableSize);
        ApplyScrollHeight(_resolvedPageHeight);

        double width = double.IsInfinity(availableSize.Width) ? SettingsLayoutHelper.DialogShellWidth : availableSize.Width;
        var constraint = new Size(width, _resolvedPageHeight);
        return base.MeasureOverride(constraint);
    }

    /// <summary>
    /// Arranges the grid and re-resolves page height using final size and dialog chrome.
    /// </summary>
    /// <param name="finalSize">The final size allocated by the parent.</param>
    /// <returns>The actual size used by the grid.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _resolvedPageHeight = ResolvePageHeightForArrange(finalSize);
        ApplyScrollHeight(_resolvedPageHeight);
        return base.ArrangeOverride(new Size(finalSize.Width, _resolvedPageHeight));
    }

    /// <summary>
    /// Resolves page height during measure from the available height constraint.
    /// </summary>
    /// <param name="availableSize">The available size proposed by the parent.</param>
    /// <returns>The page height clamped to <see cref="SettingsLayoutHelper.PageMaxHeight"/>.</returns>
    private static double ResolvePageHeight(Size availableSize)
    {
        double preferred = SettingsLayoutHelper.PageMaxHeight;

        if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
        {
            return Math.Min(preferred, availableSize.Height);
        }

        return preferred;
    }

    /// <summary>
    /// Resolves page height during arrange, falling back to dialog or window chrome estimates.
    /// </summary>
    /// <param name="finalSize">The final size allocated by the parent.</param>
    /// <returns>The page height clamped between 160 and <see cref="SettingsLayoutHelper.PageMaxHeight"/>.</returns>
    private double ResolvePageHeightForArrange(Size finalSize)
    {
        double preferred = SettingsLayoutHelper.PageMaxHeight;

        if (!double.IsInfinity(finalSize.Height) && finalSize.Height > 0)
        {
            return Math.Min(preferred, finalSize.Height);
        }

        // Estimate from the hosting dialog's actual height minus title/button chrome.
        if (_hostDialog is not null && _hostDialog.ActualHeight > 0)
        {
            const double dialogChrome = 128;
            double fromDialog = _hostDialog.ActualHeight - dialogChrome;
            if (fromDialog < preferred)
            {
                return Math.Max(160, fromDialog);
            }
        }

        // Fall back to the dialog XAML root window height minus reserved chrome.
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

    /// <summary>
    /// Applies the resolved page height to this grid and the attached scroll viewer.
    /// </summary>
    /// <param name="pageHeight">The target page height in device-independent pixels.</param>
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
