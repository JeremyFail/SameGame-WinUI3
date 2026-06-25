using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SameGame.Model;

namespace SameGame.UI;

public static class WindowHelper
{
    public static void ApplyTheme(Window window)
    {
        ApplyWindowTheme(window, App.CurrentUiTheme);
    }

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

    public static void ApplyTheme(GameSettings.UiTheme theme, FrameworkElement root) =>
        ThemeHelper.ApplyTheme(theme, root);

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
