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

    public enum BoardSizePreset
    {
        Small,
        Normal,
        Large,
        Custom
    }

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

    public enum UiTheme
    {
        System,
        Light,
        Dark
    }

    public enum Background
    {
        Black,
        Green
    }

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

    public static Skin ParseSkin(string name)
    {
        if (name == "LETTER_TILES")
        {
            return Skin.Classic;
        }

        return Enum.Parse<Skin>(name);
    }

    public static GenerationDifficulty ParseGenerationDifficulty(string name)
    {
        if (name == "NORMAL")
        {
            return GenerationDifficulty.Medium;
        }

        return Enum.Parse<GenerationDifficulty>(name);
    }

    public void ResetColorsToDefault() => _tileColors = DefaultColors();

    public static Color[] DefaultColors() =>
    [
        Color.FromArgb(255, 0, 0, 255),
        Color.FromArgb(255, 255, 0, 0),
        Color.FromArgb(255, 255, 0, 255),
        Color.FromArgb(255, 255, 255, 0),
        Color.FromArgb(255, 0, 255, 255),
        Color.FromArgb(255, 255, 128, 0)
    ];

    public static char LetterForColorIndex(int index) => (char)('A' + index);

    public int BoardWidth() => _boardSizePreset == BoardSizePreset.Custom
        ? _customWidth
        : PresetWidth(_boardSizePreset);

    public int BoardHeight() => _boardSizePreset == BoardSizePreset.Custom
        ? _customHeight
        : PresetHeight(_boardSizePreset);

    public static int PresetWidth(BoardSizePreset preset) => preset switch
    {
        BoardSizePreset.Small => 10,
        BoardSizePreset.Normal => 20,
        BoardSizePreset.Large => 20,
        _ => 20
    };

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

    public Color[] TileColors() => (Color[])_tileColors.Clone();

    public Color ColorAt(int index) => _tileColors[index];

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

    public bool SkinHasSelectionAnimation() => _skin == Skin.Gems;

    public bool SelectionAnimationEnabled() => _animationsEnabled && SkinHasSelectionAnimation();

    public Color BackgroundColor() =>
        _background == Background.Green
            ? Color.FromArgb(255, 0, 96, 0)
            : Color.FromArgb(255, 0, 0, 0);

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    public object Clone()
    {
        var copy = (GameSettings)MemberwiseClone();
        copy._tileColors = (Color[])_tileColors.Clone();
        return copy;
    }
}
