using System.Text.Json;
using SameGame.Model;
using SameGame.UI;

namespace SameGame.Persistence;

/// <summary>
/// Loads and saves game settings and window bounds to a JSON file in local app data.
/// </summary>
public static class SettingsPersistence
{
    private const string FileName = "settings.json";

    /// <summary>
    /// Loads persisted game settings from disk.
    /// </summary>
    /// <returns>
    /// The stored <see cref="GameSettings"/>, or default settings when the file is missing,
    /// invalid, or cannot be read.
    /// </returns>
    public static GameSettings Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return new GameSettings();
            }

            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json);
            return dto?.ToSettings() ?? new GameSettings();
        }
        catch
        {
            return new GameSettings();
        }
    }

    /// <summary>
    /// Persists the current game settings to disk.
    /// </summary>
    /// <param name="settings">The settings snapshot to save.</param>
    public static void Save(GameSettings settings)
    {
        try
        {
            var dto = SettingsDto.FromSettings(settings);
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(GetFolderPath());
            File.WriteAllText(GetFilePath(), json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    /// <summary>
    /// Loads persisted main-window bounds from the settings file.
    /// </summary>
    /// <returns>
    /// The stored window position and size, or <see langword="null"/> when bounds are missing,
    /// invalid, or cannot be read.
    /// </returns>
    public static WindowBounds? LoadWindowBounds()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json);
            if (dto?.WindowWidth is null or < 200 || dto.WindowHeight is null or < 200)
            {
                return null;
            }

            return new WindowBounds(dto.WindowX ?? 100, dto.WindowY ?? 100, dto.WindowWidth.Value, dto.WindowHeight.Value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Updates and persists the main-window bounds while preserving other settings.
    /// </summary>
    /// <param name="x">The window left position in pixels.</param>
    /// <param name="y">The window top position in pixels.</param>
    /// <param name="width">The window width in pixels.</param>
    /// <param name="height">The window height in pixels.</param>
    public static void SaveWindowBounds(int x, int y, int width, int height)
    {
        var settings = Load();
        var dto = SettingsDto.FromSettings(settings)
            with
            {
                WindowX = x,
                WindowY = y,
                WindowWidth = width,
                WindowHeight = height
            };
        try
        {
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(GetFolderPath());
            File.WriteAllText(GetFilePath(), json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    /// <summary>
    /// Deletes the persisted settings file, if it exists.
    /// </summary>
    public static void ClearAll()
    {
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    /// <summary>
    /// Gets the folder path used for settings persistence.
    /// </summary>
    /// <returns>The absolute path to the SameGame app-data folder.</returns>
    private static string GetFolderPath() => AppDataPaths.GetAppFolder();

    /// <summary>
    /// Gets the full path to the settings JSON file.
    /// </summary>
    /// <returns>The absolute path to <c>settings.json</c>.</returns>
    private static string GetFilePath() => AppDataPaths.GetFilePath(FileName);

    /// <summary>
    /// Represents the persisted position and size of the main application window.
    /// </summary>
    /// <param name="X">The window left position in pixels.</param>
    /// <param name="Y">The window top position in pixels.</param>
    /// <param name="Width">The window width in pixels.</param>
    /// <param name="Height">The window height in pixels.</param>
    public readonly record struct WindowBounds(int X, int Y, int Width, int Height);

    /// <summary>
    /// JSON-serializable snapshot of persisted game settings and optional window bounds.
    /// </summary>
    private sealed record SettingsDto
    {
        public string? BoardSizePreset { get; init; }
        public int? CustomWidth { get; init; }
        public int? CustomHeight { get; init; }
        public int? NumColors { get; init; }
        public int[]? TileColorsArgb { get; init; }
        public string? Skin { get; init; }
        public string? UiTheme { get; init; }
        public string? Background { get; init; }
        public bool? TimerEnabled { get; init; }
        public int? TimerSeconds { get; init; }
        public bool? SoundEnabled { get; init; }
        public int? SoundEffectsVolume { get; init; }
        public bool? BackgroundMusicEnabled { get; init; }
        public int? BackgroundMusicVolume { get; init; }
        public string? LanguageCode { get; init; }
        public string? GenerationDifficulty { get; init; }
        public int? Randomness { get; init; }
        public bool? AnimationsEnabled { get; init; }
        public int? WindowX { get; init; }
        public int? WindowY { get; init; }
        public int? WindowWidth { get; init; }
        public int? WindowHeight { get; init; }

        /// <summary>
        /// Creates a DTO from the current in-memory game settings.
        /// </summary>
        /// <param name="settings">The settings instance to serialize.</param>
        /// <returns>A DTO containing all persistable setting values.</returns>
        public static SettingsDto FromSettings(GameSettings settings)
        {
            var colors = settings.TileColors();
            return new SettingsDto
            {
                BoardSizePreset = settings.BoardSizePresetValue.ToString(),
                CustomWidth = settings.CustomWidth,
                CustomHeight = settings.CustomHeight,
                NumColors = settings.NumColors,
                TileColorsArgb = colors.Select(c => (int)c.A << 24 | (int)c.R << 16 | (int)c.G << 8 | (int)c.B).ToArray(),
                Skin = settings.SkinValue.ToString(),
                UiTheme = settings.UiThemeValue.ToString(),
                Background = settings.BackgroundValue.ToString(),
                TimerEnabled = settings.TimerEnabled,
                TimerSeconds = settings.TimerSeconds,
                SoundEnabled = settings.SoundEnabled,
                SoundEffectsVolume = settings.SoundEffectsVolume,
                BackgroundMusicEnabled = settings.BackgroundMusicEnabled,
                BackgroundMusicVolume = settings.BackgroundMusicVolume,
                LanguageCode = settings.LanguageCode,
                GenerationDifficulty = settings.GenerationDifficultyValue.ToString(),
                Randomness = settings.Randomness,
                AnimationsEnabled = settings.AnimationsEnabled,
            };
        }

        /// <summary>
        /// Reconstructs game settings from persisted DTO values.
        /// </summary>
        /// <returns>
        /// A <see cref="GameSettings"/> instance with fields populated from any non-null
        /// or non-empty DTO members.
        /// </returns>
        public GameSettings ToSettings()
        {
            var settings = new GameSettings();

            // Board layout and tile appearance.
            if (!string.IsNullOrEmpty(BoardSizePreset))
            {
                settings.BoardSizePresetValue = Enum.Parse<GameSettings.BoardSizePreset>(BoardSizePreset);
            }

            if (CustomWidth.HasValue)
            {
                settings.CustomWidth = CustomWidth.Value;
            }

            if (CustomHeight.HasValue)
            {
                settings.CustomHeight = CustomHeight.Value;
            }

            if (NumColors.HasValue)
            {
                settings.NumColors = NumColors.Value;
            }

            if (TileColorsArgb is { Length: > 0 })
            {
                for (int i = 0; i < Math.Min(TileColorsArgb.Length, 6); i++)
                {
                    int argb = TileColorsArgb[i];
                    settings.SetColorAt(i, global::Windows.UI.Color.FromArgb(
                        (byte)((argb >> 24) & 0xFF),
                        (byte)((argb >> 16) & 0xFF),
                        (byte)((argb >> 8) & 0xFF),
                        (byte)(argb & 0xFF)));
                }
            }

            // Visual theme and background.
            if (!string.IsNullOrEmpty(Skin))
            {
                settings.SkinValue = GameSettings.ParseSkin(Skin);
            }

            if (!string.IsNullOrEmpty(UiTheme))
            {
                settings.UiThemeValue = ThemeHelper.ParseUiTheme(UiTheme);
            }

            if (!string.IsNullOrEmpty(Background))
            {
                settings.BackgroundValue = Enum.Parse<GameSettings.Background>(Background);
            }

            // Timer options.
            if (TimerEnabled.HasValue)
            {
                settings.TimerEnabled = TimerEnabled.Value;
            }

            if (TimerSeconds.HasValue)
            {
                settings.TimerSeconds = TimerSeconds.Value;
            }

            // Audio settings.
            if (SoundEnabled.HasValue)
            {
                settings.SoundEnabled = SoundEnabled.Value;
            }

            if (SoundEffectsVolume.HasValue)
            {
                settings.SoundEffectsVolume = SoundEffectsVolume.Value;
            }

            if (BackgroundMusicEnabled.HasValue)
            {
                settings.BackgroundMusicEnabled = BackgroundMusicEnabled.Value;
            }

            if (BackgroundMusicVolume.HasValue)
            {
                settings.BackgroundMusicVolume = BackgroundMusicVolume.Value;
            }

            // Localization and board generation.
            if (!string.IsNullOrEmpty(LanguageCode))
            {
                settings.LanguageCode = LanguageCode;
            }

            if (!string.IsNullOrEmpty(GenerationDifficulty))
            {
                settings.GenerationDifficultyValue = GameSettings.ParseGenerationDifficulty(GenerationDifficulty);
            }

            if (Randomness.HasValue)
            {
                settings.Randomness = Randomness.Value;
            }

            if (AnimationsEnabled.HasValue)
            {
                settings.AnimationsEnabled = AnimationsEnabled.Value;
            }

            return settings;
        }
    }
}
