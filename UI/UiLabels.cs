using System.Text;
using SameGame.I18n;
using SameGame.Model;

namespace SameGame.UI;

public static class UiLabels
{
    public static string Label(GameSettings.BoardSizePreset preset) =>
        Messages.Get($"enum.boardSizePreset.{ToEnumKey(preset)}");

    public static string Label(GameSettings.GenerationDifficulty difficulty) =>
        Messages.Get($"enum.generationDifficulty.{ToEnumKey(difficulty)}");

    public static string Label(GameSettings.Skin skin) =>
        Messages.Get($"enum.skin.{ToEnumKey(skin)}");

    public static string Label(GameSettings.UiTheme theme) =>
        Messages.Get($"enum.uiTheme.{ToEnumKey(theme)}");

    public static string Label(GameSettings.Background background) =>
        Messages.Get($"enum.background.{ToEnumKey(background)}");

    public static string Label(Enum value) => value switch
    {
        GameSettings.BoardSizePreset preset => Label(preset),
        GameSettings.GenerationDifficulty difficulty => Label(difficulty),
        GameSettings.Skin skin => Label(skin),
        GameSettings.UiTheme theme => Label(theme),
        GameSettings.Background background => Label(background),
        _ => value.ToString()
    };

    internal static string ToEnumKey<T>(T value) where T : struct, Enum =>
        ToEnumKey(value.ToString());

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
