using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using SameGame.I18n;
using SameGame.UI;
using Windows.ApplicationModel.DataTransfer;

namespace SameGame.Dialogs;

public static class FatalErrorDialog
{
    public static async Task ShowAsync(Exception exception)
    {
        string errorText = CrashReportFormatter.Format(exception);
        string exceptionName = exception.GetType().Name;
        if (string.IsNullOrEmpty(exceptionName))
        {
            exceptionName = exception.GetType().FullName ?? "Exception";
        }

        var summary = new TextBlock
        {
            Text = Messages.Get("startupError.summary"),
            TextWrapping = TextWrapping.Wrap
        };
        var exceptionLabel = new TextBlock
        {
            Text = Messages.Format("startupError.exception", exceptionName),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var help = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        help.Inlines.Add(new Run { Text = Messages.Get("startupError.helpPrefix") + " " });
        help.Inlines.Add(new Run
        {
            Text = Messages.Get("startupError.helpBold"),
            FontWeight = FontWeights.SemiBold
        });
        help.Inlines.Add(new Run { Text = " " + Messages.Get("startupError.helpSuffix") });

        var issuesButton = CreateAccentButton(Messages.Get("startupError.issuesLink"));
        issuesButton.Click += async (_, _) =>
            await BrowserHelper.OpenUrlAsync(BrowserHelper.IssuesUrl, App.DialogXamlRoot!);

        var detailsText = new TextBlock
        {
            Text = errorText,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            IsTextSelectionEnabled = true
        };

        var detailsPanel = new ScrollViewer
        {
            Content = detailsText,
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(8),
            BorderBrush = Application.Current.Resources["ControlStrokeColorDefaultBrush"] as Microsoft.UI.Xaml.Media.Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4)
        };

        var detailsLabel = new TextBlock
        {
            Text = Messages.Get("startupError.showDetails"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var detailsChevron = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        var toggleDetails = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { detailsLabel, detailsChevron }
            }
        };
        toggleDetails.Click += (_, _) =>
        {
            bool show = detailsPanel.Visibility != Visibility.Visible;
            detailsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            detailsLabel.Text = Messages.Get(show ? "startupError.hideDetails" : "startupError.showDetails");
            detailsChevron.Glyph = show ? "\uE70E" : "\uE70D";
        };

        var copyButton = CreateActionButton(Messages.Get("startupError.button.copyDetails"));

        var copiedNotice = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            Opacity = 0.85,
            FontSize = 13
        };

        copyButton.Click += async (_, _) =>
        {
            await CopyToClipboardAsync(errorText);
            copiedNotice.Text = Messages.Get("startupError.copied");
            copiedNotice.Visibility = Visibility.Visible;
        };

        var copyShowRow = new Grid { ColumnSpacing = 8 };
        copyShowRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        copyShowRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(copyButton, 0);
        Grid.SetColumn(toggleDetails, 1);
        copyShowRow.Children.Add(copyButton);
        copyShowRow.Children.Add(toggleDetails);

        var actionsPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        actionsPanel.Children.Add(issuesButton);
        actionsPanel.Children.Add(copyShowRow);

        var panel = new StackPanel { Spacing = 10, MinWidth = 360, MaxWidth = 480 };
        panel.Children.Add(summary);
        panel.Children.Add(exceptionLabel);
        panel.Children.Add(help);
        panel.Children.Add(actionsPanel);
        panel.Children.Add(copiedNotice);
        panel.Children.Add(detailsPanel);

        var dialog = DialogHelper.CreateDialog(Messages.Get("startupError.title"), panel);
        dialog.CloseButtonText = Messages.Get("startupError.button.cancel");
        dialog.DefaultButton = ContentDialogButton.None;
        if (Application.Current.Resources.TryGetValue("DestructiveCloseButtonStyle", out object? closeButtonStyle)
            && closeButtonStyle is Microsoft.UI.Xaml.Style destructiveStyle)
        {
            dialog.CloseButtonStyle = destructiveStyle;
        }

        await dialog.ShowAsync();
    }

    private static Button CreateActionButton(string text)
    {
        return new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static Button CreateAccentButton(string text)
    {
        var button = CreateActionButton(text);
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out object? style)
            && style is Microsoft.UI.Xaml.Style accentStyle)
        {
            button.Style = accentStyle;
        }

        return button;
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
        await Task.CompletedTask;
    }
}
