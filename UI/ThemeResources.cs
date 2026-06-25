using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SameGame.Model;
using Windows.UI;

namespace SameGame.UI;

internal static class ThemeResources
{
    public static void ApplyCardStyle(Border border)
    {
        RefreshCardStyle(border);
        border.Loaded += (_, _) => RefreshCardStyle(border);
        border.ActualThemeChanged += (_, _) => RefreshCardStyle(border);
    }

    public static void ApplyLayerBackground(Panel panel)
    {
        RefreshLayerBackground(panel);
        panel.Loaded += (_, _) => RefreshLayerBackground(panel);
        panel.ActualThemeChanged += (_, _) => RefreshLayerBackground(panel);
    }

    public static void ApplyChrome(FrameworkElement root)
    {
        ElementTheme theme = ResolveAppTheme();
        if (root is Panel panel)
        {
            panel.Background = CreateBrush("LayerFillColorDefaultBrush", theme);
        }

        RefreshCardsInTree(root, theme);
    }

    public static ElementTheme ResolveAppTheme()
    {
        ElementTheme fromApp = ThemeHelper.ToElementTheme(App.CurrentUiTheme);
        if (fromApp is ElementTheme.Light or ElementTheme.Dark)
        {
            return fromApp;
        }

        return ThemeHelper.IsSystemDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
    }

    private static void RefreshCardStyle(Border border)
    {
        ElementTheme theme = ResolveAppTheme();
        border.Background = CreateBrush("CardBackgroundFillColorDefaultBrush", theme);
        border.BorderBrush = CreateBrush("CardStrokeColorDefaultBrush", theme);
    }

    private static void RefreshLayerBackground(Panel panel)
    {
        panel.Background = CreateBrush("LayerFillColorDefaultBrush", ResolveAppTheme());
    }

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
