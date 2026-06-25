using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.I18n;
using SameGame.UI;

namespace SameGame.Views;

public sealed class HelpWindow : Window
{
    private Border? _navBorder;
    private Grid? _rootGrid;
    private readonly TextBlock _body = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Padding = new Thickness(20, 16, 20, 20),
        FontSize = 14,
        LineHeight = 22
    };

    public HelpWindow()
    {
        Title = Messages.Get("help.title");
        WindowHelper.Configure(this, 820, 520, minimumWidth: 560, minimumHeight: 400);
        Content = BuildContent();
        WindowHelper.ApplyWindowTheme(this, App.CurrentUiTheme);
    }

    private Grid BuildContent()
    {
        var topics = new[]
        {
            ("howToPlay", Messages.Get("help.page.howToPlay.title")),
            ("tips", Messages.Get("help.page.tips.title")),
            ("options", Messages.Get("help.page.options.title"))
        };

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

            _body.Text = nav.SelectedIndex switch
            {
                0 => Messages.Get("help.page.howToPlay.body"),
                1 => Messages.Get("help.page.tips.body"),
                _ => Messages.Get("help.page.options.body")
            };
        };

        _navBorder = new Border
        {
            Padding = new Thickness(4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = nav
        };
        ThemeResources.ApplyCardStyle(_navBorder);

        _rootGrid = new Grid { Padding = new Thickness(16) };
        ThemeResources.ApplyLayerBackground(_rootGrid);
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _rootGrid.ColumnSpacing = 12;
        var bodyScroll = new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetColumn(_navBorder, 0);
        Grid.SetColumn(bodyScroll, 1);
        _rootGrid.Children.Add(_navBorder);
        _rootGrid.Children.Add(bodyScroll);
        nav.SelectedIndex = 0;
        return _rootGrid;
    }

    public void ApplyAppTheme()
    {
        WindowHelper.ApplyWindowTheme(this, App.CurrentUiTheme);
    }
}
