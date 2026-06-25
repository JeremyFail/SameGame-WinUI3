using Windows.UI;

namespace SameGame.Model;

/// <summary>
/// Configurable game options including board size, colors, appearance, and sound.
/// </summary>
public sealed class GameSettings : ICloneable
{
    public const int MinCustomDimension = 5;
    public const int MaxCustomDimension = 50;
    public const int MinColors = 3;
    public const int MaxColors = 6;
    public const int DefaultTimerSeconds = 180;
    public const int MinSoundVolume = 0;
    public const int MaxSoundVolume = 100;
    public const int DefaultSoundEffectsVolume = 75;
    public const int DefaultBackgroundMusicVolume = 50;

    /// <summary>
    /// Preset board dimensions or a user-defined custom size.
    /// </summary>
    public enum BoardSizePreset
    {
        Small,
        Normal,
        Large,
        Custom
    }

    /// <summary>
    /// Visual tile style used on the game board.
    /// </summary>
    public enum Skin
    {
        Modern,
        Classic,
        Marbles,
        Blockcraft,
        Bricks,
        Shapes,
        Gems
    }

    /// <summary>
    /// Application light/dark appearance preference.
    /// </summary>
    public enum UiTheme
    {
        System,
        Light,
        Dark
    }

    /// <summary>
    /// Board backdrop color scheme.
    /// </summary>
    public enum Background
    {
        Black,
        Green
    }

    /// <summary>
    /// Controls how aggressively generated boards favor or avoid large groups.
    /// </summary>
    public enum GenerationDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    private BoardSizePreset _boardSizePreset = BoardSizePreset.Normal;
    private int _customWidth = 20;
    private int _customHeight = 10;
    private int _numColors = 5;
    private Color[] _tileColors = DefaultColors();
    private Skin _skin = Skin.Modern;
    private UiTheme _uiTheme = UiTheme.System;
    private Background _background = Background.Green;
    private bool _timerEnabled;
    private int _timerSeconds = DefaultTimerSeconds;
    private bool _soundEnabled = true;
    private int _soundEffectsVolume = DefaultSoundEffectsVolume;
    private bool _backgroundMusicEnabled;
    private int _backgroundMusicVolume = DefaultBackgroundMusicVolume;
    private string _languageCode = "en";
    private GenerationDifficulty _generationDifficulty = GenerationDifficulty.Medium;
    private int _randomness = 50;
    private bool _animationsEnabled = true;

    /// <summary>
    /// Parses a persisted skin name, including legacy values.
    /// </summary>
    /// <param name="name">Skin enum name or legacy identifier.</param>
    /// <returns>The corresponding <see cref="Skin"/> value.</returns>
    public static Skin ParseSkin(string name)
    {
        if (name == "LETTER_TILES")
        {
            return Skin.Classic;
        }

        return Enum.Parse<Skin>(name);
    }

    /// <summary>
    /// Parses a persisted generation difficulty name, including legacy values.
    /// </summary>
    /// <param name="name">Difficulty enum name or legacy identifier.</param>
    /// <returns>The corresponding <see cref="GenerationDifficulty"/> value.</returns>
    public static GenerationDifficulty ParseGenerationDifficulty(string name)
    {
        if (name == "NORMAL")
        {
            return GenerationDifficulty.Medium;
        }

        return Enum.Parse<GenerationDifficulty>(name);
    }

    /// <summary>
    /// Restores tile colors to the built-in default palette.
    /// </summary>
    public void ResetColorsToDefault() => _tileColors = DefaultColors();

    /// <summary>
    /// Returns the default tile color palette.
    /// </summary>
    /// <returns>An array of six default tile colors.</returns>
    public static Color[] DefaultColors() =>
    [
        Color.FromArgb(255, 0, 0, 255),
        Color.FromArgb(255, 255, 0, 0),
        Color.FromArgb(255, 255, 0, 255),
        Color.FromArgb(255, 255, 255, 0),
        Color.FromArgb(255, 0, 255, 255),
        Color.FromArgb(255, 255, 128, 0)
    ];

    /// <summary>
    /// Maps a color index to its classic letter label (A, B, C, ...).
    /// </summary>
    /// <param name="index">Zero-based color index.</param>
    /// <returns>The letter corresponding to the color index.</returns>
    public static char LetterForColorIndex(int index) => (char)('A' + index);

    /// <summary>
    /// Gets the effective board width from the current preset or custom dimensions.
    /// </summary>
    /// <returns>Board width in cells.</returns>
    public int BoardWidth() => _boardSizePreset == BoardSizePreset.Custom
        ? _customWidth
        : PresetWidth(_boardSizePreset);

    /// <summary>
    /// Gets the effective board height from the current preset or custom dimensions.
    /// </summary>
    /// <returns>Board height in cells.</returns>
    public int BoardHeight() => _boardSizePreset == BoardSizePreset.Custom
        ? _customHeight
        : PresetHeight(_boardSizePreset);

    /// <summary>
    /// Returns the width associated with a board size preset.
    /// </summary>
    /// <param name="preset">The preset to query.</param>
    /// <returns>Preset width in cells.</returns>
    public static int PresetWidth(BoardSizePreset preset) => preset switch
    {
        BoardSizePreset.Small => 10,
        BoardSizePreset.Normal => 20,
        BoardSizePreset.Large => 20,
        _ => 20
    };

    /// <summary>
    /// Returns the height associated with a board size preset.
    /// </summary>
    /// <param name="preset">The preset to query.</param>
    /// <returns>Preset height in cells.</returns>
    public static int PresetHeight(BoardSizePreset preset) => preset switch
    {
        BoardSizePreset.Small => 10,
        BoardSizePreset.Normal => 10,
        BoardSizePreset.Large => 20,
        _ => 10
    };

    public BoardSizePreset BoardSizePresetValue
    {
        get => _boardSizePreset;
        set
        {
            _boardSizePreset = value;
            if (value != BoardSizePreset.Custom)
            {
                _customWidth = PresetWidth(value);
                _customHeight = PresetHeight(value);
            }
        }
    }

    public int CustomWidth
    {
        get => _customWidth;
        set => _customWidth = Clamp(value, MinCustomDimension, MaxCustomDimension);
    }

    public int CustomHeight
    {
        get => _customHeight;
        set => _customHeight = Clamp(value, MinCustomDimension, MaxCustomDimension);
    }

    public int NumColors
    {
        get => _numColors;
        set => _numColors = Clamp(value, MinColors, MaxColors);
    }

    /// <summary>
    /// Returns a copy of the current tile color palette.
    /// </summary>
    /// <returns>A cloned array of tile colors.</returns>
    public Color[] TileColors() => (Color[])_tileColors.Clone();

    /// <summary>
    /// Gets the tile color at the given index.
    /// </summary>
    /// <param name="index">Zero-based color index.</param>
    /// <returns>The color at that index.</returns>
    public Color ColorAt(int index) => _tileColors[index];

    /// <summary>
    /// Sets the tile color at the given index.
    /// </summary>
    /// <param name="index">Zero-based color index.</param>
    /// <param name="color">The new color value.</param>
    public void SetColorAt(int index, Color color) => _tileColors[index] = color;

    public Skin SkinValue
    {
        get => _skin;
        set => _skin = value;
    }

    public UiTheme UiThemeValue
    {
        get => _uiTheme;
        set => _uiTheme = value;
    }

    public Background BackgroundValue
    {
        get => _background;
        set => _background = value;
    }

    public bool TimerEnabled
    {
        get => _timerEnabled;
        set => _timerEnabled = value;
    }

    public int TimerSeconds
    {
        get => _timerSeconds;
        set => _timerSeconds = Math.Max(1, value);
    }

    public bool SoundEnabled
    {
        get => _soundEnabled;
        set => _soundEnabled = value;
    }

    public int SoundEffectsVolume
    {
        get => _soundEffectsVolume;
        set => _soundEffectsVolume = Clamp(value, MinSoundVolume, MaxSoundVolume);
    }

    public bool BackgroundMusicEnabled
    {
        get => _backgroundMusicEnabled;
        set => _backgroundMusicEnabled = value;
    }

    public int BackgroundMusicVolume
    {
        get => _backgroundMusicVolume;
        set => _backgroundMusicVolume = Clamp(value, MinSoundVolume, MaxSoundVolume);
    }

    public string LanguageCode
    {
        get => _languageCode;
        set => _languageCode = string.IsNullOrWhiteSpace(value) ? "en" : value;
    }

    public GenerationDifficulty GenerationDifficultyValue
    {
        get => _generationDifficulty;
        set => _generationDifficulty = value;
    }

    public int Randomness
    {
        get => _randomness;
        set => _randomness = Clamp(value, 0, 100);
    }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set => _animationsEnabled = value;
    }

    /// <summary>
    /// Determines whether the current skin supports a selection animation.
    /// </summary>
    /// <returns><c>true</c> when the active skin is <see cref="Skin.Gems"/>; otherwise, <c>false</c>.</returns>
    public bool SkinHasSelectionAnimation() => _skin == Skin.Gems;

    /// <summary>
    /// Determines whether selection animations should play for the current settings.
    /// </summary>
    /// <returns><c>true</c> when animations are enabled and the skin supports them; otherwise, <c>false</c>.</returns>
    public bool SelectionAnimationEnabled() => _animationsEnabled && SkinHasSelectionAnimation();

    /// <summary>
    /// Returns the solid background color for the active background setting.
    /// </summary>
    /// <returns>The configured board background color.</returns>
    public Color BackgroundColor() =>
        _background == Background.Green
            ? Color.FromArgb(255, 0, 96, 0)
            : Color.FromArgb(255, 0, 0, 0);

    /// <summary>
    /// Clamps an integer to an inclusive min/max range.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <returns><paramref name="value"/> limited to the inclusive range.</returns>
    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// Creates a deep copy of these settings, including the tile color array.
    /// </summary>
    /// <returns>A new <see cref="GameSettings"/> instance with equivalent values.</returns>
    public object Clone()
    {
        var copy = (GameSettings)MemberwiseClone();
        copy._tileColors = (Color[])_tileColors.Clone();
        return copy;
    }
}
