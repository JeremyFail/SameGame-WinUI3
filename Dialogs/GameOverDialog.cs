using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.I18n;
using SameGame.Model;
using SameGame.Persistence;
using SameGame.UI;

namespace SameGame.Dialogs;

public sealed class GameOverDialog
{
    public enum Choice
    {
        NewGame,
        PlayAgain,
        Close
    }

    public event Action? ScoreSaved;

    private readonly int _score;
    private readonly int _width;
    private readonly int _height;
    private readonly int _numColors;

    public GameOverDialog(int score, int width, int height, int numColors)
    {
        _score = score;
        _width = width;
        _height = height;
        _numColors = numColors;
    }

    public async Task<Choice> ShowAsync()
    {
        bool qualifies = HighScoreStorage.Qualifies(_score);
        TextBox? nameBox = null;
        TextBox? initialsBox = null;

        var panel = new StackPanel { Spacing = 8, MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Format("gameOver.finalScore", _score),
            TextWrapping = TextWrapping.Wrap
        });

        if (qualifies)
        {
            panel.Children.Add(new TextBlock { Text = Messages.Get("gameOver.newHighScore") });
            nameBox = new TextBox { Header = Messages.Get("gameOver.label.name") };
            initialsBox = new TextBox { Header = Messages.Get("gameOver.label.initials"), MaxLength = 4 };
            panel.Children.Add(nameBox);
            panel.Children.Add(initialsBox);
        }

        var dialog = DialogHelper.CreateDialog(Messages.Get("gameOver.title"), panel);
        dialog.PrimaryButtonText = Messages.Get("gameOver.button.newGame");
        dialog.SecondaryButtonText = Messages.Get("gameOver.button.playAgain");
        dialog.CloseButtonText = Messages.Get("gameOver.button.close");
        dialog.DefaultButton = ContentDialogButton.Primary;

        var result = await dialog.ShowAsync();
        SaveIfNeeded(nameBox, initialsBox);

        return result switch
        {
            ContentDialogResult.Primary => Choice.NewGame,
            ContentDialogResult.Secondary => Choice.PlayAgain,
            _ => Choice.Close
        };
    }

    private void SaveIfNeeded(TextBox? nameBox, TextBox? initialsBox)
    {
        if (nameBox is null || initialsBox is null)
        {
            return;
        }

        string name = nameBox.Text.Trim();
        string initials = initialsBox.Text.Trim();
        if (name.Length == 0 || initials.Length == 0)
        {
            return;
        }

        HighScoreStorage.Add(new HighScoreEntry(name, initials, _score, _width, _height, _numColors));
        ScoreSaved?.Invoke();
    }
}
