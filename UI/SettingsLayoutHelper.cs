using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SameGame.UI;

/// <summary>
/// Layout constants and factory helpers for the Advanced Options dialog. Tweak these to adjust dialog sizing.
/// </summary>
internal static class SettingsLayoutHelper
{
    /// <summary>Fixed width of the Advanced Options body (nav + content).</summary>
    public const double DialogShellWidth = 485;

    /// <summary>Width of the left navigation column in Advanced Options.</summary>
    public const double NavColumnWidth = 108;

    /// <summary>Gap between nav and content columns.</summary>
    public const double NavColumnSpacing = 8;

    /// <summary>Padding inside the nav card border (each side).</summary>
    public const double NavBorderPadding = 4;

    /// <summary>
    /// Extra space between scrollable section cards and the vertical scrollbar.
    /// Lower moves the scrollbar closer to the dialog's right edge; raise to inset it.
    /// </summary>
    public const double ScrollContentRightInset = 0;

    /// <summary>Maximum height of each settings tab scroll area when space allows.</summary>
    public const double PageMaxHeight = 380;

    /// <summary>
    /// Creates a themed card border wrapping a titled section of settings controls.
    /// </summary>
    /// <param name="title">Optional section title; omitted when null or empty.</param>
    /// <param name="children">The controls to place inside the section body.</param>
    /// <returns>A styled border containing the section layout.</returns>
    public static Border CreateSection(string? title, params UIElement[] children)
    {
        var body = new StackPanel { Spacing = 10 };
        foreach (var child in children)
        {
            body.Children.Add(child);
        }

        var panel = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrEmpty(title))
        {
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.9
            });
        }

        panel.Children.Add(body);

        var border = new Border
        {
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, ScrollContentRightInset, 10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = panel
        };
        ThemeResources.ApplyCardStyle(border);
        return border;
    }

    /// <summary>
    /// Creates a vertical stack of section elements for a settings tab page.
    /// </summary>
    /// <param name="sections">The section borders or controls to stack.</param>
    /// <returns>A stretch-aligned stack panel containing all sections.</returns>
    public static UIElement CreatePage(params UIElement[] sections)
    {
        var stack = new StackPanel
        {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var section in sections)
        {
            stack.Children.Add(section);
        }

        return stack;
    }

    /// <summary>
    /// Creates a fixed-height scroll viewer sized for a settings tab page.
    /// </summary>
    /// <returns>A scroll viewer configured with <see cref="PageMaxHeight"/> bounds.</returns>
    public static ScrollViewer CreatePageScrollViewer()
    {
        return new ScrollViewer
        {
            MinHeight = PageMaxHeight,
            MaxHeight = PageMaxHeight,
            Height = PageMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    /// <summary>
    /// Creates a two-column grid row with equal star-width columns.
    /// </summary>
    /// <param name="left">The control placed in the left column.</param>
    /// <param name="right">The control placed in the right column.</param>
    /// <returns>A grid containing both controls in a 1:1 column layout.</returns>
    public static Grid CreateTwoColumnRow(FrameworkElement left, FrameworkElement right)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        left.HorizontalAlignment = HorizontalAlignment.Stretch;
        right.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }
}
