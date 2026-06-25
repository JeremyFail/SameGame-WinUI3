using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SameGame.Model;

namespace SameGame.UI;

/// <summary>
/// Configures window size, icon, backdrop, and theme for application windows.
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// Applies the current app UI theme to the window content and chrome.
    /// </summary>
    /// <param name="window">The window to theme.</param>
    public static void ApplyTheme(Window window)
    {
        ApplyWindowTheme(window, App.CurrentUiTheme);
    }

    /// <summary>
    /// Applies the specified UI theme to the window, clearing Mica and refreshing chrome.
    /// </summary>
    /// <param name="window">The window to theme.</param>
    /// <param name="theme">The UI theme setting to apply.</param>
    public static void ApplyWindowTheme(Window window, GameSettings.UiTheme theme)
    {
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        ThemeHelper.ApplyTheme(theme, root);

        // Mica reads as white behind auxiliary windows; use a solid themed background instead.
        window.SystemBackdrop = null;

        ThemeResources.ApplyChrome(root);
    }

    /// <summary>
    /// Applies a UI theme directly to a framework element root.
    /// </summary>
    /// <param name="theme">The UI theme setting to apply.</param>
    /// <param name="root">The root element whose theme is updated.</param>
    public static void ApplyTheme(GameSettings.UiTheme theme, FrameworkElement root) =>
        ThemeHelper.ApplyTheme(theme, root);

    /// <summary>
    /// Sets the window icon, initial size, optional minimum size, and Mica backdrop when supported.
    /// </summary>
    /// <param name="window">The window to configure.</param>
    /// <param name="width">The initial client width in pixels.</param>
    /// <param name="height">The initial client height in pixels.</param>
    /// <param name="minimumWidth">The preferred minimum width in pixels (0 to skip).</param>
    /// <param name="minimumHeight">The preferred minimum height in pixels (0 to skip).</param>
    public static void Configure(
        Window window,
        int width,
        int height,
        int minimumWidth = 0,
        int minimumHeight = 0)
    {
        ApplyIcon(window);
        window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(width, height));

        if (minimumWidth > 0 || minimumHeight > 0)
        {
            if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                if (minimumWidth > 0)
                {
                    presenter.PreferredMinimumWidth = minimumWidth;
                }

                if (minimumHeight > 0)
                {
                    presenter.PreferredMinimumHeight = minimumHeight;
                }
            }
        }

        if (window.SystemBackdrop is null && MicaController.IsSupported())
        {
            window.SystemBackdrop = new MicaBackdrop();
        }
    }

    /// <summary>
    /// Sets the window icon from the first available search path.
    /// </summary>
    /// <param name="window">The window whose icon is updated.</param>
    public static void ApplyIcon(Window window)
    {
        foreach (string path in GetIconSearchPaths())
        {
            try
            {
                window.AppWindow.SetIcon(path);
                return;
            }
            catch
            {
                // Try next path.
            }
        }
    }

    /// <summary>
    /// Yields candidate file paths for the application icon, in search order.
    /// </summary>
    /// <returns>An sequence of icon file paths to try.</returns>
    private static IEnumerable<string> GetIconSearchPaths()
    {
        yield return "Assets/AppIcon.ico";
        yield return System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        string? packagePath = TryGetPackageIconPath();
        if (packagePath is not null)
        {
            yield return packagePath;
        }
    }

    /// <summary>
    /// Attempts to resolve the packaged application icon path.
    /// </summary>
    /// <returns>The full path to AppIcon.ico in the package, or <see langword="null"/> when unavailable.</returns>
    private static string? TryGetPackageIconPath()
    {
        try
        {
            return System.IO.Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "Assets",
                "AppIcon.ico");
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
