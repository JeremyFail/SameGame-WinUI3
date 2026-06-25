using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SameGame.I18n;
using SameGame.Model;
using SameGame.UI;

namespace SameGame.Controls;

public sealed partial class ObjectivesPanel : UserControl
{
    public ObjectivesPanel()
    {
        InitializeComponent();
        HeaderLabel.Text = Messages.Get("objectives.header");
        RenderTransform = new TranslateTransform { X = 148 };
    }

    public async Task ShowAnimatedAsync()
    {
        Visibility = Visibility.Visible;
        if (RenderTransform is TranslateTransform transform)
        {
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 148,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(animation);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "X");
            storyboard.Begin();
            await Task.Delay(220);
        }
    }

    public async Task HideAnimatedAsync()
    {
        if (RenderTransform is TranslateTransform transform)
        {
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 148,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn
                }
            };
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(animation);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "X");
            storyboard.Begin();
            await Task.Delay(180);
        }

        Visibility = Visibility.Collapsed;
    }

    public void Update(Board board, GameSettings settings)
    {
        RowsPanel.Children.Clear();
        var counts = board.ColorCounts();

        for (int i = 0; i < settings.NumColors; i++)
        {
            RowsPanel.Children.Add(CreateRow(i, counts[i], settings));
        }
    }

    public void RefreshTheme()
    {
        ElementTheme theme = ThemeResources.ResolveAppTheme();
        Background = CreateRowBrush(theme);
        BorderBrush = CreateRowBorderBrush(theme);
        foreach (var border in RowsPanel.Children.OfType<Border>())
        {
            border.Background = CreateRowBrush(theme);
            border.BorderBrush = CreateRowBorderBrush(theme);
        }
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush CreateRowBrush(ElementTheme theme) =>
        theme == ElementTheme.Dark
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x2D, 0x2D, 0x2D))
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFA, 0xFA, 0xFA));

    private static Microsoft.UI.Xaml.Media.SolidColorBrush CreateRowBorderBrush(ElementTheme theme) =>
        theme == ElementTheme.Dark
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x3F, 0x3F, 0x3F))
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0xE0, 0xE0));

    private static Border CreateRow(int colorIndex, int count, GameSettings settings)
    {
        var preview = new TilePreviewControl
        {
            Width = 36,
            Height = 36,
            Margin = new Thickness(0, 0, 8, 0)
        };
        preview.Configure(settings, colorIndex);

        var countLabel = new TextBlock
        {
            Text = Messages.Format("objectives.count", count),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(preview, 0);
        Grid.SetColumn(countLabel, 1);
        row.Children.Add(preview);
        row.Children.Add(countLabel);

        var rowBorder = new Border
        {
            Padding = new Thickness(6, 4, 6, 4),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = row
        };
        ElementTheme theme = ThemeResources.ResolveAppTheme();
        rowBorder.Background = CreateRowBrush(theme);
        rowBorder.BorderBrush = CreateRowBorderBrush(theme);
        return rowBorder;
    }
}
