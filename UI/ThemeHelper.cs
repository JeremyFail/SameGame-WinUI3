using Microsoft.UI.Xaml;
using SameGame.Model;
using Windows.UI.ViewManagement;

namespace SameGame.UI;

/// <summary>
/// Converts and applies UI theme settings to WinUI elements and the application.
/// </summary>
public static class ThemeHelper
{
    private static UISettings? _uiSettings;

    /// <summary>
    /// Applies the requested theme to a root element and, when possible, the application.
    /// </summary>
    /// <param name="theme">The UI theme setting to apply.</param>
    /// <param name="root">The root framework element whose <see cref="FrameworkElement.RequestedTheme"/> is set.</param>
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

    /// <summary>
    /// Maps a game UI theme setting to a WinUI element theme.
    /// </summary>
    /// <param name="theme">The UI theme setting.</param>
    /// <returns>The corresponding <see cref="ElementTheme"/> value.</returns>
    public static ElementTheme ToElementTheme(GameSettings.UiTheme theme) => theme switch
    {
        GameSettings.UiTheme.Light => ElementTheme.Light,
        GameSettings.UiTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    /// <summary>
    /// Resolves a game UI theme setting to a concrete application theme.
    /// </summary>
    /// <param name="theme">The UI theme setting.</param>
    /// <returns>The resolved <see cref="ApplicationTheme"/>; system theme follows OS preference.</returns>
    public static ApplicationTheme ResolveApplicationTheme(GameSettings.UiTheme theme) => theme switch
    {
        GameSettings.UiTheme.Light => ApplicationTheme.Light,
        GameSettings.UiTheme.Dark => ApplicationTheme.Dark,
        _ => IsSystemDarkTheme() ? ApplicationTheme.Dark : ApplicationTheme.Light
    };

    /// <summary>
    /// Detects whether the operating system is currently using a dark theme.
    /// </summary>
    /// <returns><see langword="true"/> if the system foreground color indicates a dark theme.</returns>
    public static bool IsSystemDarkTheme()
    {
        _uiSettings ??= new UISettings();
        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        return foreground.R > 200 && foreground.G > 200 && foreground.B > 200;
    }

    /// <summary>
    /// Parses a persisted or legacy theme string into a <see cref="GameSettings.UiTheme"/> value.
    /// </summary>
    /// <param name="value">The raw theme string, or <see langword="null"/> for system default.</param>
    /// <returns>The parsed theme; unrecognized values fall back to system.</returns>
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
