using System.Text;
using SameGame.I18n;
using SameGame.Model;

namespace SameGame.UI;

/// <summary>
/// Resolves localized display labels for game settings enum values.
/// </summary>
public static class UiLabels
{
    /// <summary>
    /// Gets the localized label for a board size preset.
    /// </summary>
    /// <param name="preset">The board size preset value.</param>
    /// <returns>The localized display string.</returns>
    public static string Label(GameSettings.BoardSizePreset preset) =>
        Messages.Get($"enum.boardSizePreset.{ToEnumKey(preset)}");

    /// <summary>
    /// Gets the localized label for a generation difficulty setting.
    /// </summary>
    /// <param name="difficulty">The generation difficulty value.</param>
    /// <returns>The localized display string.</returns>
    public static string Label(GameSettings.GenerationDifficulty difficulty) =>
        Messages.Get($"enum.generationDifficulty.{ToEnumKey(difficulty)}");

    /// <summary>
    /// Gets the localized label for a tile skin setting.
    /// </summary>
    /// <param name="skin">The skin value.</param>
    /// <returns>The localized display string.</returns>
    public static string Label(GameSettings.Skin skin) =>
        Messages.Get($"enum.skin.{ToEnumKey(skin)}");

    /// <summary>
    /// Gets the localized label for a UI theme setting.
    /// </summary>
    /// <param name="theme">The UI theme value.</param>
    /// <returns>The localized display string.</returns>
    public static string Label(GameSettings.UiTheme theme) =>
        Messages.Get($"enum.uiTheme.{ToEnumKey(theme)}");

    /// <summary>
    /// Gets the localized label for a board background setting.
    /// </summary>
    /// <param name="background">The background value.</param>
    /// <returns>The localized display string.</returns>
    public static string Label(GameSettings.Background background) =>
        Messages.Get($"enum.background.{ToEnumKey(background)}");

    /// <summary>
    /// Gets the localized label for any supported settings enum value.
    /// </summary>
    /// <param name="value">The enum value to label.</param>
    /// <returns>The localized display string, or <see cref="object.ToString"/> for unsupported types.</returns>
    public static string Label(Enum value) => value switch
    {
        GameSettings.BoardSizePreset preset => Label(preset),
        GameSettings.GenerationDifficulty difficulty => Label(difficulty),
        GameSettings.Skin skin => Label(skin),
        GameSettings.UiTheme theme => Label(theme),
        GameSettings.Background background => Label(background),
        _ => value.ToString()
    };

    /// <summary>
    /// Converts an enum value to an uppercase snake-case message resource key segment.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The resource key segment (e.g. "BOARD_SIZE_PRESET").</returns>
    internal static string ToEnumKey<T>(T value) where T : struct, Enum =>
        ToEnumKey(value.ToString());

    /// <summary>
    /// Converts a PascalCase enum name to an uppercase snake-case message resource key segment.
    /// </summary>
    /// <param name="name">The PascalCase enum member name.</param>
    /// <returns>The resource key segment (e.g. "BOARD_SIZE_PRESET").</returns>
    internal static string ToEnumKey(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}
