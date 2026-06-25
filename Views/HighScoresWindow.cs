using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SameGame.I18n;
using SameGame.Model;
using SameGame.Persistence;
using SameGame.UI;

namespace SameGame.Views;

/// <summary>
/// A secondary window that lists persisted high scores in a tabular layout.
/// </summary>
public sealed class HighScoresWindow : Window
{
    private const int DefaultWidth = 960;
    private const int DefaultHeight = 500;
    private const int MinimumWidth = 560;
    private const int MinimumHeight = 360;

    private readonly StackPanel _rowsPanel = new() { Spacing = 4 };
    private readonly TextBlock _emptyLabel = new()
    {
        Opacity = 0.75,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(16, 8, 16, 16)
    };

    /// <summary>
    /// Initializes the high-scores window with default size, table layout, and application theme.
    /// </summary>
    public HighScoresWindow()
    {
        Title = Messages.Get("highScores.title");
        WindowHelper.Configure(this, DefaultWidth, DefaultHeight, minimumWidth: MinimumWidth, minimumHeight: MinimumHeight);

        var root = new Grid();
        ThemeResources.ApplyLayerBackground(root);
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = Messages.Get("highScores.header"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(16, 16, 16, 8)
        };

        var scroll = new ScrollViewer
        {
            Margin = new Thickness(16, 0, 16, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreateTableHeader(),
                    _rowsPanel
                }
            }
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(scroll, 1);
        Grid.SetRow(_emptyLabel, 2);
        root.Children.Add(header);
        root.Children.Add(scroll);
        root.Children.Add(_emptyLabel);
        Content = root;
        WindowHelper.ApplyWindowTheme(this, App.CurrentUiTheme);
    }

    /// <summary>
    /// Reapplies the current application UI theme to this window.
    /// </summary>
    public void ApplyAppTheme()
    {
        WindowHelper.ApplyWindowTheme(this, App.CurrentUiTheme);
    }

    /// <summary>
    /// Reloads high scores from storage and rebuilds the table rows.
    /// </summary>
    public void Refresh()
    {
        var entries = HighScoreStorage.Load();
        _rowsPanel.Children.Clear();

        int rank = 1;
        foreach (var entry in entries)
        {
            _rowsPanel.Children.Add(CreateDataRow(rank++, entry));
        }

        _emptyLabel.Text = entries.Count == 0
            ? Messages.Get("highScores.empty")
            : string.Empty;
        _emptyLabel.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Creates the column header row for the high-scores table.
    /// </summary>
    /// <returns>A bordered grid containing header labels.</returns>
    private static UIElement CreateTableHeader()
    {
        var grid = CreateRowGrid();
        AddHeaderCell(grid, 0, Messages.Get("highScores.column.rank"));
        AddHeaderCell(grid, 1, Messages.Get("highScores.column.name"));
        AddHeaderCell(grid, 2, Messages.Get("highScores.column.initials"));
        AddHeaderCell(grid, 3, Messages.Get("highScores.column.score"));
        AddHeaderCell(grid, 4, Messages.Get("highScores.column.board"));
        AddHeaderCell(grid, 5, Messages.Get("highScores.column.colors"));
        AddHeaderCell(grid, 6, Messages.Get("highScores.column.date"));

        return new Border
        {
            Padding = new Thickness(0, 0, 0, 4),
            Margin = new Thickness(0, 0, 0, 4),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(64, 128, 128, 128)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    /// <summary>
    /// Creates a data row for a single high-score entry.
    /// </summary>
    /// <param name="rank">The display rank of the entry.</param>
    /// <param name="entry">The high-score entry to display.</param>
    /// <returns>A grid populated with entry values.</returns>
    private static Grid CreateDataRow(int rank, HighScoreEntry entry)
    {
        var grid = CreateRowGrid();
        AddDataCell(grid, 0, rank.ToString());
        AddDataCell(grid, 1, entry.Name, trim: true);
        AddDataCell(grid, 2, entry.Initials);
        AddDataCell(grid, 3, entry.Score.ToString());
        AddDataCell(grid, 4, entry.BoardDescription());
        AddDataCell(grid, 5, entry.NumColors.ToString());
        AddDataCell(grid, 6, entry.Date, trim: true, dim: true);
        return grid;
    }

    /// <summary>
    /// Creates an empty grid with the standard high-scores column layout.
    /// </summary>
    /// <returns>A configured column grid for a table row.</returns>
    private static Grid CreateRowGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            MinWidth = MinimumWidth - 32
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        return grid;
    }

    /// <summary>
    /// Adds a semibold header label to the specified column of a row grid.
    /// </summary>
    /// <param name="grid">The row grid to populate.</param>
    /// <param name="column">The zero-based column index.</param>
    /// <param name="text">The header label text.</param>
    private static void AddHeaderCell(Grid grid, int column, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.85
        };
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    /// <summary>
    /// Adds a data label to the specified column of a row grid.
    /// </summary>
    /// <param name="grid">The row grid to populate.</param>
    /// <param name="column">The zero-based column index.</param>
    /// <param name="text">The cell text.</param>
    /// <param name="rightAlign">Whether to right-align the cell text.</param>
    /// <param name="trim">Whether to apply character ellipsis trimming.</param>
    /// <param name="dim">Whether to reduce the cell opacity for secondary text.</param>
    private static void AddDataCell(
        Grid grid,
        int column,
        string text,
        bool rightAlign = false,
        bool trim = false,
        bool dim = false)
    {
        var label = new TextBlock
        {
            Text = text,
            HorizontalAlignment = rightAlign ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            Opacity = dim ? 0.85 : 1
        };
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }
}
