using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SameGame.Model;
using Windows.UI;

namespace SameGame.UI;

/// <summary>
/// Applies themed card and layer background brushes to UI elements.
/// </summary>
internal static class ThemeResources
{
    /// <summary>
    /// Subscribes a border to theme-aware card styling and applies the initial brush set.
    /// </summary>
    /// <param name="border">The border element to style as a card.</param>
    public static void ApplyCardStyle(Border border)
    {
        RefreshCardStyle(border);
        border.Loaded += (_, _) => RefreshCardStyle(border);
        border.ActualThemeChanged += (_, _) => RefreshCardStyle(border);
    }

    /// <summary>
    /// Subscribes a panel to theme-aware layer background styling and applies the initial brush.
    /// </summary>
    /// <param name="panel">The panel whose background is set to the layer fill color.</param>
    public static void ApplyLayerBackground(Panel panel)
    {
        RefreshLayerBackground(panel);
        panel.Loaded += (_, _) => RefreshLayerBackground(panel);
        panel.ActualThemeChanged += (_, _) => RefreshLayerBackground(panel);
    }

    /// <summary>
    /// Applies the layer background to a root panel and refreshes card borders in the visual tree.
    /// </summary>
    /// <param name="root">The root element whose subtree is themed.</param>
    public static void ApplyChrome(FrameworkElement root)
    {
        ElementTheme theme = ResolveAppTheme();
        if (root is Panel panel)
        {
            panel.Background = CreateBrush("LayerFillColorDefaultBrush", theme);
        }

        RefreshCardsInTree(root, theme);
    }

    /// <summary>
    /// Resolves the effective element theme from app settings and system preference.
    /// </summary>
    /// <returns>The resolved light or dark element theme.</returns>
    public static ElementTheme ResolveAppTheme()
    {
        ElementTheme fromApp = ThemeHelper.ToElementTheme(App.CurrentUiTheme);
        if (fromApp is ElementTheme.Light or ElementTheme.Dark)
        {
            return fromApp;
        }

        return ThemeHelper.IsSystemDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
    }

    /// <summary>
    /// Updates a border's background and border brush to the current card theme colors.
    /// </summary>
    /// <param name="border">The border element to refresh.</param>
    private static void RefreshCardStyle(Border border)
    {
        ElementTheme theme = ResolveAppTheme();
        border.Background = CreateBrush("CardBackgroundFillColorDefaultBrush", theme);
        border.BorderBrush = CreateBrush("CardStrokeColorDefaultBrush", theme);
    }

    /// <summary>
    /// Updates a panel's background to the current layer fill theme color.
    /// </summary>
    /// <param name="panel">The panel whose background is refreshed.</param>
    private static void RefreshLayerBackground(Panel panel)
    {
        panel.Background = CreateBrush("LayerFillColorDefaultBrush", ResolveAppTheme());
    }

    /// <summary>
    /// Walks the visual tree and applies card styling to tile previews and rounded card borders.
    /// </summary>
    /// <param name="node">The root node of the subtree to traverse.</param>
    /// <param name="theme">The resolved element theme.</param>
    private static void RefreshCardsInTree(DependencyObject node, ElementTheme theme)
    {
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Border border && border.Child is Controls.TilePreviewControl)
            {
                border.Background = CreateBrush("CardBackgroundFillColorDefaultBrush", theme);
                border.BorderBrush = CreateBrush("CardStrokeColorDefaultBrush", theme);
            }
            else if (child is Border cardBorder && cardBorder.CornerRadius.TopLeft >= 4)
            {
                cardBorder.Background = CreateBrush("CardBackgroundFillColorDefaultBrush", theme);
                cardBorder.BorderBrush = CreateBrush("CardStrokeColorDefaultBrush", theme);
            }

            RefreshCardsInTree(child, theme);
        }
    }

    /// <summary>
    /// Creates a solid color brush for a WinUI theme resource key.
    /// </summary>
    /// <param name="resourceKey">The logical brush resource name.</param>
    /// <param name="theme">The resolved element theme.</param>
    /// <returns>A solid color brush matching the resource for the given theme.</returns>
    private static SolidColorBrush CreateBrush(string resourceKey, ElementTheme theme)
    {
        bool dark = theme == ElementTheme.Dark;
        Color color = resourceKey switch
        {
            "CardStrokeColorDefaultBrush" => dark
                ? Color.FromArgb(255, 0x3F, 0x3F, 0x3F)
                : Color.FromArgb(255, 0xE0, 0xE0, 0xE0),
            "LayerFillColorDefaultBrush" => dark
                ? Color.FromArgb(255, 0x1E, 0x1E, 0x1E)
                : Color.FromArgb(255, 0xF3, 0xF3, 0xF3),
            _ => dark
                ? Color.FromArgb(255, 0x2D, 0x2D, 0x2D)
                : Color.FromArgb(255, 0xFA, 0xFA, 0xFA)
        };

        return new SolidColorBrush(color);
    }
}
