using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.I18n;
using SameGame.UI;

namespace SameGame.Views;

/// <summary>
/// A secondary window that displays help topics in a navigable two-column layout.
/// </summary>
public sealed class HelpWindow : Window
{
    private Border? _navBorder;
    private Grid? _rootGrid;
    private readonly ScrollViewer _bodyScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    private readonly StackPanel _bodyHost = new()
    {
        Padding = new Thickness(20, 16, 20, 20),
        Spacing = 4
    };

    /// <summary>
    /// Initializes the help window with default size, content, and application theme.
    /// </summary>
    public HelpWindow()
    {
        Title = Messages.Get("help.title");
        WindowHelper.Configure(this, 820, 520, minimumWidth: 560, minimumHeight: 400);
        _bodyScroll.Content = _bodyHost;
        Content = BuildContent();
        WindowHelper.ApplyWindowTheme(this, App.CurrentUiTheme);
    }

    /// <summary>
    /// Builds the two-column help layout with a topic navigation list and scrollable body text.
    /// </summary>
    /// <returns>The root grid containing navigation and help content.</returns>
    private Grid BuildContent()
    {
        var topics = new[]
        {
            ("howToPlay", Messages.Get("help.page.howToPlay.title")),
            ("tips", Messages.Get("help.page.tips.title")),
            ("options", Messages.Get("help.page.options.title"))
        };

        // Left navigation list
        var nav = new ListView
        {
            Width = 168,
            SelectionMode = ListViewSelectionMode.Single,
            Items = { topics[0].Item2, topics[1].Item2, topics[2].Item2 }
        };

        nav.SelectionChanged += (_, _) =>
        {
            if (nav.SelectedIndex < 0)
            {
                return;
            }

            SetBody(nav.SelectedIndex switch
            {
                0 => Messages.Get("help.page.howToPlay.body"),
                1 => Messages.Get("help.page.tips.body"),
                _ => Messages.Get("help.page.options.body")
            });
        };

        _navBorder = new Border
        {
            Padding = new Thickness(4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = nav
        };
        ThemeResources.ApplyCardStyle(_navBorder);

        // Root grid with nav column and scrollable body
        _rootGrid = new Grid { Padding = new Thickness(16) };
        ThemeResources.ApplyLayerBackground(_rootGrid);
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _rootGrid.ColumnSpacing = 12;
        Grid.SetColumn(_navBorder, 0);
        Grid.SetColumn(_bodyScroll, 1);
        _rootGrid.Children.Add(_navBorder);
        _rootGrid.Children.Add(_bodyScroll);
        nav.SelectedIndex = 0;
        return _rootGrid;
    }

    /// <summary>
    /// Replaces the scrollable help body with formatted content for the selected topic.
    /// </summary>
    /// <param name="markup">Localized help markup for the active page.</param>
    private void SetBody(string markup)
    {
        _bodyHost.Children.Clear();
        _bodyHost.Children.Add(HelpTextFormatter.Build(markup));
        _bodyScroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    /// <summary>
    /// Reapplies the current application UI theme to this window.
    /// </summary>
    public void ApplyAppTheme()
    {
        WindowHelper.ApplyWindowTheme(this, App.CurrentUiTheme);
    }
}
