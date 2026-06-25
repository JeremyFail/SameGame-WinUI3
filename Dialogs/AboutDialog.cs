using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using SameGame.I18n;
using SameGame.UI;

namespace SameGame.Dialogs;

public sealed class AboutDialog
{
    public async Task ShowAsync()
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 400, MaxWidth = 440 };

        panel.Children.Add(new TextBlock
        {
            Text = Messages.Format("app.name", MainPage.AppName),
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Format("app.version"),
            FontSize = 16,
            Opacity = 0.85,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Get("about.by"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Get("about.inspired"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Margin = new Thickness(0, 4, 0, 0),
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Get("about.bio"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            TextAlignment = TextAlignment.Center
        });

        var websiteLink = new HyperlinkButton
        {
            Content = Messages.Get("about.websiteLink"),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        websiteLink.Click += async (_, _) =>
            await BrowserHelper.OpenUrlAsync(BrowserHelper.WebsiteUrl, App.DialogXamlRoot!);
        panel.Children.Add(websiteLink);

        panel.Children.Add(new TextBlock
        {
            Text = Messages.Get("about.copyright"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Get("about.license"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Opacity = 0.9,
            TextAlignment = TextAlignment.Center
        });

        var dialog = DialogHelper.CreateDialog(
            string.Empty,
            new ScrollViewer
            {
                MaxHeight = 460,
                Content = panel
            });
        dialog.PrimaryButtonText = Messages.Get("about.button.close");
        dialog.SecondaryButtonText = Messages.Get("about.button.license");
        dialog.DefaultButton = ContentDialogButton.Primary;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            await BrowserHelper.OpenUrlAsync(BrowserHelper.LicenseUrl, App.DialogXamlRoot!);
        }
    }
}
