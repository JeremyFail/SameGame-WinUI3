using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace SameGame.UI;

/// <summary>
/// Renders localized help markup into a structured stack of headings, paragraphs, and bullets.
/// Markup rules: <c>##</c>/<c>###</c> headings, <c>•</c> bullets, and <c>**bold**</c> inline emphasis.
/// </summary>
internal static class HelpTextFormatter
{
    private const double BodyFontSize = 14;
    private const double BodyLineHeight = 22;

    /// <summary>
    /// Builds a vertically stacked help page from a markup string.
    /// </summary>
    /// <param name="markup">Localized help body text using simple markup conventions.</param>
    /// <returns>A panel containing the formatted help content.</returns>
    public static StackPanel Build(string markup)
    {
        var panel = new StackPanel { Spacing = 4 };
        bool hasContent = false;

        foreach (var rawLine in markup.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("## "))
            {
                panel.Children.Add(CreateHeading(line[3..], 16, hasContent ? new Thickness(0, 14, 0, 2) : new Thickness(0, 0, 0, 2)));
                hasContent = true;
                continue;
            }

            if (line.StartsWith("### "))
            {
                panel.Children.Add(CreateHeading(line[4..], 14, hasContent ? new Thickness(0, 10, 0, 0) : new Thickness(0, 0, 0, 0)));
                hasContent = true;
                continue;
            }

            if (line.StartsWith("• "))
            {
                panel.Children.Add(CreateBullet(line[2..]));
                hasContent = true;
                continue;
            }

            panel.Children.Add(CreateParagraph(line));
            hasContent = true;
        }

        return panel;
    }

    private static TextBlock CreateHeading(string text, double fontSize, Thickness margin) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Margin = margin,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = fontSize + 8
        };

    private static TextBlock CreateParagraph(string text)
    {
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = BodyFontSize,
            LineHeight = BodyLineHeight,
            Margin = new Thickness(0, 2, 0, 0)
        };
        AppendInlineText(block.Inlines, text);
        return block;
    }

    private static Grid CreateBullet(string text)
    {
        var content = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = BodyFontSize,
            LineHeight = BodyLineHeight,
            VerticalAlignment = VerticalAlignment.Top
        };
        AppendInlineText(content.Inlines, text);

        var grid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 0),
            ColumnSpacing = 8
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var bullet = new TextBlock
        {
            Text = "•",
            FontSize = BodyFontSize,
            LineHeight = BodyLineHeight,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(bullet, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(bullet);
        grid.Children.Add(content);
        return grid;
    }

    private static void AppendInlineText(InlineCollection inlines, string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            int boldStart = text.IndexOf("**", index, StringComparison.Ordinal);
            if (boldStart < 0)
            {
                inlines.Add(new Run { Text = text[index..] });
                return;
            }

            if (boldStart > index)
            {
                inlines.Add(new Run { Text = text[index..boldStart] });
            }

            int boldEnd = text.IndexOf("**", boldStart + 2, StringComparison.Ordinal);
            if (boldEnd < 0)
            {
                inlines.Add(new Run { Text = text[boldStart..] });
                return;
            }

            inlines.Add(new Run
            {
                Text = text[(boldStart + 2)..boldEnd],
                FontWeight = FontWeights.SemiBold
            });
            index = boldEnd + 2;
        }
    }
}
