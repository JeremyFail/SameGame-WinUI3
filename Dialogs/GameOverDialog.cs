using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SameGame.I18n;
using SameGame.Model;
using SameGame.Persistence;
using SameGame.UI;

namespace SameGame.Dialogs;

/// <summary>
/// Displays the game-over dialog with score summary, optional high-score entry, and next-action choices.
/// </summary>
public sealed class GameOverDialog
{
    /// <summary>
    /// Identifies the action the user chose when dismissing the game-over dialog.
    /// </summary>
    public enum Choice
    {
        /// <summary>Start a new game with default settings.</summary>
        NewGame,

        /// <summary>Replay the current board configuration.</summary>
        PlayAgain,

        /// <summary>Close the dialog without starting a new game.</summary>
        Close
    }

    /// <summary>
    /// Raised after a qualifying high score is saved to persistent storage.
    /// </summary>
    public event Action? ScoreSaved;

    private readonly int _score;
    private readonly int _width;
    private readonly int _height;
    private readonly int _numColors;

    /// <summary>
    /// Initializes a new game-over dialog for the given score and board parameters.
    /// </summary>
    /// <param name="score">The player's final score.</param>
    /// <param name="width">The board width in cells.</param>
    /// <param name="height">The board height in cells.</param>
    /// <param name="numColors">The number of tile colors used in the game.</param>
    public GameOverDialog(int score, int width, int height, int numColors)
    {
        _score = score;
        _width = width;
        _height = height;
        _numColors = numColors;
    }

    /// <summary>
    /// Shows the game-over dialog and returns the user's chosen next action.
    /// </summary>
    /// <returns>The <see cref="Choice"/> selected by the user.</returns>
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

        // High-score entry fields when the score qualifies
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

    /// <summary>
    /// Persists a high-score entry when both name and initials fields were provided and non-empty.
    /// </summary>
    /// <param name="nameBox">The name input field, or <c>null</c> if high-score entry was not shown.</param>
    /// <param name="initialsBox">The initials input field, or <c>null</c> if high-score entry was not shown.</param>
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
