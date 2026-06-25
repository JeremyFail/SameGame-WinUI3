using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.I18n;

namespace SameGame.UI;

/// <summary>
/// Opens external URLs in the default browser with a fallback error dialog.
/// </summary>
public static class BrowserHelper
{
    /// <summary>The project website URL.</summary>
    public const string WebsiteUrl = "https://jeremyfail.dev";

    /// <summary>The open-source license URL on GitHub.</summary>
    public const string LicenseUrl = "https://github.com/JeremyFail/SameGame-WinUI3/blob/master/LICENSE";

    /// <summary>The GitHub issues page URL.</summary>
    public const string IssuesUrl = "https://github.com/JeremyFail/SameGame-WinUI3/issues";

    /// <summary>
    /// Launches the given URL in the default browser, showing an error dialog on failure.
    /// </summary>
    /// <param name="url">The absolute URL to open.</param>
    /// <param name="xamlRoot">The XAML root used to host the error dialog if launch fails.</param>
    /// <returns>A task that completes when the URL is launched or the error dialog is dismissed.</returns>
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
