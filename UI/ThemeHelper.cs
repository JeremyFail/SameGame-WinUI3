using Microsoft.UI.Xaml;
using SameGame.Model;
using Windows.UI.ViewManagement;

namespace SameGame.UI;

public static class ThemeHelper
{
    private static UISettings? _uiSettings;

    public static void ApplyTheme(GameSettings.UiTheme theme, FrameworkElement root)
    {
        root.RequestedTheme = ToElementTheme(theme);

        if (Application.Current is null)
        {
            return;
        }

        try
        {
            Application.Current.RequestedTheme = ResolveApplicationTheme(theme);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Application-level theme is not always mutable.
        }
    }

    public static ElementTheme ToElementTheme(GameSettings.UiTheme theme) => theme switch
    {
        GameSettings.UiTheme.Light => ElementTheme.Light,
        GameSettings.UiTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    public static ApplicationTheme ResolveApplicationTheme(GameSettings.UiTheme theme) => theme switch
    {
        GameSettings.UiTheme.Light => ApplicationTheme.Light,
        GameSettings.UiTheme.Dark => ApplicationTheme.Dark,
        _ => IsSystemDarkTheme() ? ApplicationTheme.Dark : ApplicationTheme.Light
    };

    public static bool IsSystemDarkTheme()
    {
        _uiSettings ??= new UISettings();
        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        return foreground.R > 200 && foreground.G > 200 && foreground.B > 200;
    }

    public static GameSettings.UiTheme ParseUiTheme(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GameSettings.UiTheme.System;
        }

        return value.ToUpperInvariant() switch
        {
            "FLATLIGHT" or "FLAT_LIGHT" => GameSettings.UiTheme.Light,
            "FLATDARK" or "FLAT_DARK" => GameSettings.UiTheme.Dark,
            "SYSTEM" => GameSettings.UiTheme.System,
            "LIGHT" => GameSettings.UiTheme.Light,
            "DARK" => GameSettings.UiTheme.Dark,
            _ => Enum.TryParse<GameSettings.UiTheme>(value, out var parsed)
                ? parsed
                : GameSettings.UiTheme.System
        };
    }
}
