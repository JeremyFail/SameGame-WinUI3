using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.I18n;

namespace SameGame.UI;

public static class BrowserHelper
{
    public const string WebsiteUrl = "https://jeremyfail.dev";
    public const string LicenseUrl = "https://github.com/JeremyFail/SameGame-WinUI3/blob/master/LICENSE";
    public const string IssuesUrl = "https://github.com/JeremyFail/SameGame-WinUI3/issues";

    public static async Task OpenUrlAsync(string url, XamlRoot xamlRoot)
    {
        try
        {
            await global::Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = Messages.Get("about.browserErrorTitle"),
                Content = Messages.Format("about.browserError", ex.Message),
                CloseButtonText = Messages.Get("button.close"),
                XamlRoot = xamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
