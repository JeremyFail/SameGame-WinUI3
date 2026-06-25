using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SameGame.Controls;
using SameGame.Dialogs;
using SameGame.I18n;
using SameGame.Model;
using SameGame.Persistence;
using SameGame.Sound;
using SameGame.UI;
using SameGame.Views;

namespace SameGame;

/// <summary>
/// Primary game page hosting the menu bar, board, status labels, objectives panel, and game session logic.
/// </summary>
public sealed partial class MainPage : Page
{
    // Fallback value for the app name in case the messages are not loaded correctly
    public const string AppName = "SameGame";

    private GameSettings _settings = null!;
    private GameSession _session = null!;
    private readonly SoundManager _soundManager = new();
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _remainingSeconds;

    private MenuFlyoutItem? _undoItem;
    private MenuFlyoutItem? _redoItem;
    private ToggleMenuFlyoutItem? _soundMenuItem;
    private ToggleMenuFlyoutItem? _musicMenuItem;
    private ToggleMenuFlyoutItem? _animationsMenuItem;
    private ToggleMenuFlyoutItem? _objectivesMenuItem;
    private ToggleMenuFlyoutItem? _highScoresMenuItem;

    private HighScoresWindow? _highScoresWindow;
    private HelpWindow? _helpWindow;

    /// <summary>
    /// Initializes the page, wires lifecycle handlers, and attaches the countdown timer tick.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        _gameTimer.Tick += GameTimer_Tick;
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    /// <summary>
    /// Performs one-time initialization when the page loads: settings, session, menus, and first game.
    /// </summary>
    /// <param name="sender">The page that raised the event.</param>
    /// <param name="e">Loaded event arguments.</param>
    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            return;
        }

        _settings = SettingsPersistence.Load();
        Messages.SetLanguage(_settings.LanguageCode);
        ApplyTheme(_settings.UiThemeValue);
        _session = new GameSession(_settings);
        ConfigureSound();
        BuildMenus();
        WireBoardEvents();
        InstallKeyboardShortcuts();
        AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnGlobalKeyDown), true);
        UpdateMenuChecks();
        _ = StartNewGameAsync(false);
    }

    /// <summary>
    /// Persists settings and shuts down sound when the page is unloaded.
    /// </summary>
    /// <param name="sender">The page that raised the event.</param>
    /// <param name="e">Unloaded event arguments.</param>
    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        PersistSessionState();
        _gameTimer.Stop();
        _soundManager.Shutdown();
    }

    /// <summary>
    /// Saves the current game settings to persistent storage.
    /// </summary>
    public void PersistSessionState() => SettingsPersistence.Save(_settings);

    /// <summary>
    /// Rebuilds the entire menu bar from localized strings and current settings state.
    /// </summary>
    private void BuildMenus()
    {
        MainMenuBar.Items.Clear();

        // File menu: new game, restart, undo/redo, exit
        var fileMenu = new MenuBarItem { Title = Messages.Get("menu.file") };
        var newGame = new MenuFlyoutItem { Text = Messages.Get("menu.file.newGame") };
        newGame.Click += (_, _) => _ = StartNewGameAsync(true);
        var restart = new MenuFlyoutItem { Text = Messages.Get("menu.file.restart") };
        restart.Click += async (_, _) => await RestartGameAsync(true);
        _undoItem = new MenuFlyoutItem { Text = Messages.Get("menu.file.undo") };
        _undoItem.Click += (_, _) => UndoMove();
        _redoItem = new MenuFlyoutItem { Text = Messages.Get("menu.file.redo") };
        _redoItem.Click += (_, _) => RedoMove();
        var exit = new MenuFlyoutItem { Text = Messages.Get("menu.file.exit") };
        exit.Click += (_, _) => Application.Current.Exit();
        fileMenu.Items.Add(newGame);
        fileMenu.Items.Add(restart);
        fileMenu.Items.Add(new MenuFlyoutSeparator());
        fileMenu.Items.Add(_undoItem);
        fileMenu.Items.Add(_redoItem);
        fileMenu.Items.Add(new MenuFlyoutSeparator());
        fileMenu.Items.Add(exit);

        // Options menu: language, difficulty, size, sound/music, animations, advanced
        var optionsMenu = new MenuBarItem { Title = Messages.Get("menu.options") };
        optionsMenu.Items.Add(BuildLanguageSubMenu());
        optionsMenu.Items.Add(new MenuFlyoutSeparator());
        optionsMenu.Items.Add(BuildDifficultySubMenu());
        optionsMenu.Items.Add(BuildSizeSubMenu());
        optionsMenu.Items.Add(new MenuFlyoutSeparator());
        _soundMenuItem = new ToggleMenuFlyoutItem { Text = Messages.Get("menu.options.sound") };
        _soundMenuItem.Click += (_, _) =>
        {
            _settings.SoundEnabled = _soundMenuItem.IsChecked;
            ConfigureSound();
            SettingsPersistence.Save(_settings);
        };
        optionsMenu.Items.Add(_soundMenuItem);
        if (SoundManager.IsBackgroundMusicAvailable())
        {
            _musicMenuItem = new ToggleMenuFlyoutItem { Text = Messages.Get("menu.options.music") };
            _musicMenuItem.Click += (_, _) =>
            {
                _settings.BackgroundMusicEnabled = _musicMenuItem.IsChecked;
                ConfigureSound();
                SettingsPersistence.Save(_settings);
            };
            optionsMenu.Items.Add(_musicMenuItem);
        }
        _animationsMenuItem = new ToggleMenuFlyoutItem { Text = Messages.Get("menu.options.animations") };
        _animationsMenuItem.Click += (_, _) => { _settings.AnimationsEnabled = _animationsMenuItem.IsChecked; BoardPanel.SettingsChanged(); SettingsPersistence.Save(_settings); };
        optionsMenu.Items.Add(_animationsMenuItem);
        optionsMenu.Items.Add(new MenuFlyoutSeparator());
        var advanced = new MenuFlyoutItem { Text = Messages.Get("menu.options.advanced") };
        advanced.Click += async (_, _) => await OpenAdvancedOptionsAsync();
        optionsMenu.Items.Add(advanced);

        // View menu: skin, objectives panel, high scores window
        var viewMenu = new MenuBarItem { Title = Messages.Get("menu.view") };
        viewMenu.Items.Add(BuildSkinSubMenu());
        viewMenu.Items.Add(new MenuFlyoutSeparator());
        bool objectivesOpen = ObjectivesPanel.Visibility == Visibility.Visible;
        _objectivesMenuItem = new ToggleMenuFlyoutItem
        {
            Text = Messages.Get("menu.view.objectives"),
            IsChecked = objectivesOpen
        };
        _objectivesMenuItem.Click += (_, _) => ToggleObjectives();
        _highScoresMenuItem = new ToggleMenuFlyoutItem { Text = Messages.Get("menu.view.highScores") };
        _highScoresMenuItem.Click += (_, _) => ToggleHighScores();
        viewMenu.Items.Add(_objectivesMenuItem);
        viewMenu.Items.Add(_highScoresMenuItem);

        // Help menu: help window and about dialog
        var helpMenu = new MenuBarItem { Title = Messages.Get("menu.help") };
        var helpItem = new MenuFlyoutItem { Text = Messages.Get("menu.help.help") };
        helpItem.Click += (_, _) => ShowHelp();
        var about = new MenuFlyoutItem { Text = Messages.Get("menu.help.about") };
        about.Click += async (_, _) => await new AboutDialog().ShowAsync();
        helpMenu.Items.Add(helpItem);
        helpMenu.Items.Add(about);

        MainMenuBar.Items.Add(fileMenu);
        MainMenuBar.Items.Add(optionsMenu);
        MainMenuBar.Items.Add(viewMenu);
        MainMenuBar.Items.Add(helpMenu);
    }

    /// <summary>
    /// Builds the language radio submenu from available locale entries.
    /// </summary>
    /// <returns>A submenu with one radio item per supported language.</returns>
    private MenuFlyoutSubItem BuildLanguageSubMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = Messages.Get("menu.options.language") };
        foreach (var (code, label) in LocaleCatalog.AvailableLanguages())
        {
            var item = new RadioMenuFlyoutItem { Text = label, Tag = code, GroupName = "Language" };
            item.Click += (_, _) => ChangeLanguage(code);
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>
    /// Builds the board generation difficulty radio submenu.
    /// </summary>
    /// <returns>A submenu with one radio item per difficulty level.</returns>
    private MenuFlyoutSubItem BuildDifficultySubMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = Messages.Get("menu.options.difficulty") };
        foreach (GameSettings.GenerationDifficulty diff in Enum.GetValues<GameSettings.GenerationDifficulty>())
        {
            var item = new RadioMenuFlyoutItem { Text = UiLabels.Label(diff), Tag = diff, GroupName = "Difficulty" };
            item.Click += async (_, _) => await ChangeDifficultyAsync(diff);
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>
    /// Builds the board size preset radio submenu plus a custom size entry.
    /// </summary>
    /// <returns>A submenu with preset sizes and a custom-size shortcut.</returns>
    private MenuFlyoutSubItem BuildSizeSubMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = Messages.Get("menu.options.size") };
        foreach (var preset in new[] { GameSettings.BoardSizePreset.Small, GameSettings.BoardSizePreset.Normal, GameSettings.BoardSizePreset.Large })
        {
            var item = new RadioMenuFlyoutItem { Text = UiLabels.Label(preset), Tag = preset, GroupName = "Size" };
            item.Click += async (_, _) => await ChangeSizeAsync(preset);
            menu.Items.Add(item);
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        var custom = new MenuFlyoutItem { Text = UiLabels.Label(GameSettings.BoardSizePreset.Custom) + "…" };
        custom.Click += async (_, _) => await OpenAdvancedOptionsForCustomSizeAsync();
        menu.Items.Add(custom);
        return menu;
    }

    /// <summary>
    /// Builds the visual skin radio submenu.
    /// </summary>
    /// <returns>A submenu with one radio item per tile skin.</returns>
    private MenuFlyoutSubItem BuildSkinSubMenu()
    {
        var menu = new MenuFlyoutSubItem { Text = Messages.Get("menu.view.skin") };
        foreach (GameSettings.Skin skin in Enum.GetValues<GameSettings.Skin>())
        {
            var item = new RadioMenuFlyoutItem { Text = UiLabels.Label(skin), Tag = skin, GroupName = "Skin" };
            item.Click += (_, _) => ChangeSkin(skin);
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>
    /// Registers keyboard accelerators for undo, redo, new game, and help.
    /// </summary>
    private void InstallKeyboardShortcuts()
    {
        AddAccelerator(global::Windows.System.VirtualKey.Z, global::Windows.System.VirtualKeyModifiers.Control, () => UndoMove());
        AddAccelerator(global::Windows.System.VirtualKey.Y, global::Windows.System.VirtualKeyModifiers.Control, () => RedoMove());
        AddAccelerator(global::Windows.System.VirtualKey.F2, global::Windows.System.VirtualKeyModifiers.None, () => _ = StartNewGameAsync(true));
        AddAccelerator(global::Windows.System.VirtualKey.F1, global::Windows.System.VirtualKeyModifiers.None, ShowHelp);
    }

    /// <summary>
    /// Adds a keyboard accelerator that invokes the given action and marks the key as handled.
    /// </summary>
    /// <param name="key">Virtual key to bind.</param>
    /// <param name="modifiers">Required modifier keys.</param>
    /// <param name="action">Callback invoked when the accelerator fires.</param>
    private void AddAccelerator(
        global::Windows.System.VirtualKey key,
        global::Windows.System.VirtualKeyModifiers modifiers,
        Action action)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers
        };
        accelerator.Invoked += (_, e) =>
        {
            action();
            e.Handled = true;
        };
        KeyboardAccelerators.Add(accelerator);
    }

    /// <summary>
    /// Handles global key-down events as a fallback when menu focus prevents accelerators from firing.
    /// </summary>
    /// <param name="sender">The element that received the key event.</param>
    /// <param name="e">Key routing arguments including the pressed key.</param>
    private void OnGlobalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Control)
            .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrl && e.Key == global::Windows.System.VirtualKey.Z)
        {
            UndoMove();
            e.Handled = true;
        }
        else if (ctrl && e.Key == global::Windows.System.VirtualKey.Y)
        {
            RedoMove();
            e.Handled = true;
        }
        else if (e.Key == global::Windows.System.VirtualKey.F2)
        {
            _ = StartNewGameAsync(true);
            e.Handled = true;
        }
        else if (e.Key == global::Windows.System.VirtualKey.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Refreshes the objectives panel when it is currently visible.
    /// </summary>
    /// <param name="board">Board state to display in the objectives panel.</param>
    private void UpdateObjectivesIfVisible(Board board)
    {
        if (ObjectivesPanel.Visibility == Visibility.Visible)
        {
            ObjectivesPanel.Update(board, _settings);
        }
    }

    /// <summary>
    /// Subscribes to board selection and confirmation events to drive scoring and move application.
    /// </summary>
    private void WireBoardEvents()
    {
        // Highlight changed: update status labels or show locked-game prompt
        BoardPanel.SelectionChanged += () =>
        {
            if (_session.IsLocked)
            {
                _ = ShowLockedGamePromptAsync();
                BoardPanel.ClearHighlight();
                return;
            }

            var group = BoardPanel.HighlightedGroup;
            if (group is not null)
            {
                SetSelection(group.Size, Scoring.PointsForGroup(group.Size));
            }
        };

        BoardPanel.SelectionCleared += ClearSelection;

        // Second click on same group: play remove animation then apply move to session
        BoardPanel.GroupConfirmed += group =>
        {
            if (_session.IsLocked)
            {
                _ = ShowLockedGamePromptAsync();
                return;
            }

            if (BoardPanel.IsAnimating)
            {
                return;
            }

            SetMoveActionsEnabled(false);
            _soundManager.PlayRemove();

            int movePoints = Scoring.PointsForGroup(group.Size);
            var boardAfterMove = _session.Board.Copy();
            boardAfterMove.RemoveGroup(group);
            ClearSelection();
            SetScore(_session.Score + movePoints);
            UpdateObjectivesIfVisible(boardAfterMove);

            BoardPanel.AnimateRemove(group, () =>
            {
                _session.ApplyMove(group);
                RefreshUi(false);
                SetMoveActionsEnabled(true);
                if (!_session.IsLocked && !_session.Board.HasAnyMove())
                {
                    _ = HandleGameOverAsync(false);
                }
            });
        };
    }

    /// <summary>
    /// Generates a new board, resets the session, and optionally confirms abandoning the current game.
    /// </summary>
    /// <param name="confirmIfNeeded">When true, prompts if the current game has a non-zero score.</param>
    /// <returns>A task that completes when the new game is ready or the user cancels.</returns>
    private async Task StartNewGameAsync(bool confirmIfNeeded)
    {
        if (confirmIfNeeded && !await ConfirmAbandonGameAsync(Messages.Get("confirm.action.newGame")))
        {
            return;
        }

        StopTimer();
        SetUiEnabled(false);
        ShowLoading(true);

        try
        {
            // Generate board off the UI thread to keep the spinner responsive
            var generated = await Task.Run(() => BoardGenerator.Generate(_settings, DateTime.UtcNow.Ticks));
            _session = new GameSession(_settings);
            _session.NewGame(generated.Board, generated.Seed);
            RefreshUi(true);
            StartTimerIfEnabled();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(Messages.Format("error.boardGeneration", ex.Message));
        }
        finally
        {
            ShowLoading(false);
            SetUiEnabled(true);
        }
    }

    /// <summary>
    /// Restarts the current game with the same seed and optionally confirms abandoning progress.
    /// </summary>
    /// <param name="confirmIfNeeded">When true, prompts if the current game has a non-zero score.</param>
    /// <returns>A task that completes when the restart finishes or the user cancels.</returns>
    private async Task RestartGameAsync(bool confirmIfNeeded)
    {
        if (confirmIfNeeded && !await ConfirmAbandonGameAsync(Messages.Get("confirm.action.restart")))
        {
            return;
        }

        StopTimer();
        _session.Restart();
        RefreshUi(false);
        StartTimerIfEnabled();
    }

    /// <summary>
    /// Fire-and-forget wrapper around <see cref="RestartGameAsync"/>.
    /// </summary>
    /// <param name="confirmIfNeeded">When true, prompts if the current game has a non-zero score.</param>
    private void RestartGame(bool confirmIfNeeded) =>
        _ = RestartGameAsync(confirmIfNeeded);

    /// <summary>
    /// Reverts the last move when undo is available and no animation is running.
    /// </summary>
    private void UndoMove()
    {
        if (BoardPanel.IsAnimating || _session.IsLocked)
        {
            if (_session.IsLocked)
            {
                _ = ShowLockedGamePromptAsync();
            }

            return;
        }

        if (_session.Undo())
        {
            BoardPanel.ClearHighlight();
            RefreshUi(false);
        }
    }

    /// <summary>
    /// Reapplies the last undone move when redo is available and no animation is running.
    /// </summary>
    private void RedoMove()
    {
        if (BoardPanel.IsAnimating || _session.IsLocked)
        {
            if (_session.IsLocked)
            {
                _ = ShowLockedGamePromptAsync();
            }

            return;
        }

        if (_session.Redo())
        {
            BoardPanel.ClearHighlight();
            RefreshUi(false);
        }
    }

    /// <summary>
    /// Synchronizes the board control, score, selection, undo/redo state, and objectives with the session.
    /// </summary>
    /// <param name="boardDimensionsChanged">When true, reconfigures the board control for new dimensions.</param>
    private void RefreshUi(bool boardDimensionsChanged)
    {
        BoardPanel.Configure(_session.Board, _settings);
        SetScore(_session.Score);
        ClearSelection();
        SetMoveActionsEnabled(!BoardPanel.IsAnimating);
        UpdateObjectivesIfVisible(_session.Board);
    }

    /// <summary>
    /// Enables or disables undo and redo menu items based on session state and animation status.
    /// </summary>
    /// <param name="enabled">Whether move actions should be interactable.</param>
    private void SetMoveActionsEnabled(bool enabled)
    {
        if (_undoItem is not null)
        {
            _undoItem.IsEnabled = enabled && _session.CanUndo();
        }

        if (_redoItem is not null)
        {
            _redoItem.IsEnabled = enabled && _session.CanRedo();
        }
    }

    /// <summary>
    /// Locks the session, stops the timer, plays game-over sound, and shows the game-over dialog.
    /// </summary>
    /// <param name="timedOut">True when the game ended because the countdown reached zero.</param>
    /// <returns>A task that completes after the dialog is dismissed and any follow-up action runs.</returns>
    private async Task HandleGameOverAsync(bool timedOut)
    {
        _session.Lock();
        StopTimer();
        _soundManager.PlayGameOver();
        int score = _session.Score;
        var dialog = new GameOverDialog(
            score,
            _session.Board.Width,
            _session.Board.Height,
            _session.Board.NumColors);

        dialog.ScoreSaved += () => _highScoresWindow?.Refresh();
        var result = await dialog.ShowAsync();
        switch (result)
        {
            case GameOverDialog.Choice.NewGame:
                await StartNewGameAsync(false);
                break;
            case GameOverDialog.Choice.PlayAgain:
                _session.Unlock();
                RestartGame(false);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Shows a dialog when the user interacts with a locked (game-over) board.
    /// </summary>
    /// <returns>A task that completes after the user chooses play again, new game, or close.</returns>
    private async Task ShowLockedGamePromptAsync()
    {
        var dialog = new ContentDialog
        {
            Title = Messages.Get("gameOver.locked.title"),
            Content = Messages.Get("gameOver.locked.message"),
            PrimaryButtonText = Messages.Get("gameOver.locked.playAgain"),
            SecondaryButtonText = Messages.Get("gameOver.locked.newGame"),
            CloseButtonText = Messages.Get("button.close"),
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _session.Unlock();
            RestartGame(false);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await StartNewGameAsync(false);
        }
    }

    /// <summary>
    /// Switches the UI language, rebuilds menus, and refreshes visible labels.
    /// </summary>
    /// <param name="code">BCP-47 or catalog language code to activate.</param>
    private void ChangeLanguage(string code)
    {
        _settings.LanguageCode = code;
        Messages.SetLanguage(code);
        SettingsPersistence.Save(_settings);
        BuildMenus();
        UpdateMenuChecks();
        RefreshStatusLabels();
    }

    /// <summary>
    /// Changes board generation difficulty and starts a new game after confirmation.
    /// </summary>
    /// <param name="difficulty">Target generation difficulty.</param>
    /// <returns>A task that completes when the setting is applied or the user cancels.</returns>
    private async Task ChangeDifficultyAsync(GameSettings.GenerationDifficulty difficulty)
    {
        if (!await ConfirmSettingsChangeAsync(Messages.Get("confirm.action.changeDifficulty")))
        {
            UpdateMenuChecks();
            return;
        }

        _settings.GenerationDifficultyValue = difficulty;
        SettingsPersistence.Save(_settings);
        await StartNewGameAsync(false);
    }

    /// <summary>
    /// Changes the board size preset and starts a new game after confirmation.
    /// </summary>
    /// <param name="preset">Target board size preset.</param>
    /// <returns>A task that completes when the setting is applied or the user cancels.</returns>
    private async Task ChangeSizeAsync(GameSettings.BoardSizePreset preset)
    {
        if (!await ConfirmSettingsChangeAsync(Messages.Get("confirm.action.changeSize")))
        {
            UpdateMenuChecks();
            return;
        }

        _settings.BoardSizePresetValue = preset;
        SettingsPersistence.Save(_settings);
        await StartNewGameAsync(false);
    }

    /// <summary>
    /// Hides the menu bar and focuses the board before showing a fatal error dialog.
    /// </summary>
    internal void PrepareForFatalErrorDialog()
    {
        MainMenuBar.Visibility = Visibility.Collapsed;
        _ = BoardPanel.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Returns a client point suitable for programmatically dismissing an open menu flyout.
    /// </summary>
    /// <param name="clientPoint">When successful, the center-ish point in page client coordinates.</param>
    /// <returns>True when the page has a non-zero size and a point was computed.</returns>
    internal bool TryGetMenuDismissPoint(out Windows.Foundation.Point clientPoint)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            clientPoint = default;
            return false;
        }

        clientPoint = new Windows.Foundation.Point(ActualWidth / 2, Math.Max(ActualHeight / 2, 120));
        return true;
    }

    /// <summary>
    /// Applies a new tile skin and refreshes the board and objectives preview.
    /// </summary>
    /// <param name="skin">Visual skin to use for tile rendering.</param>
    private void ChangeSkin(GameSettings.Skin skin)
    {
        _settings.SkinValue = skin;
        SettingsPersistence.Save(_settings);
        BoardPanel.SettingsChanged();
        UpdateMenuChecks();
        UpdateObjectivesIfVisible(_session.Board);
    }

    /// <summary>
    /// Opens the advanced options dialog and applies saved changes to settings and gameplay.
    /// </summary>
    /// <returns>A task that completes when the dialog closes and any side effects finish.</returns>
    private async Task OpenAdvancedOptionsAsync()
    {
        var dialog = new AdvancedOptionsDialog(_settings, _session.HasScore(), _soundManager);
        var saved = await dialog.ShowAsync();
        if (!saved)
        {
            return;
        }

        var newSettings = dialog.ResultSettings!;
        bool gameplayChanged = GameplayChanged(_settings, newSettings);
        if (gameplayChanged && _session.HasScore())
        {
            var confirm = DialogHelper.CreateDialog(
                Messages.Get("advanced.warning.title"),
                Messages.Get("advanced.warning.gameplayChange"));
            confirm.PrimaryButtonText = Messages.Get("button.proceed");
            confirm.CloseButtonText = Messages.Get("button.cancel");
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _settings = newSettings;
        _session.Settings = _settings;
        Messages.SetLanguage(_settings.LanguageCode);
        ApplyTheme(_settings.UiThemeValue);
        SettingsPersistence.Save(_settings);
        BuildMenus();
        UpdateMenuChecks();
        ConfigureSound();
        if (gameplayChanged)
        {
            await StartNewGameAsync(false);
        }
        else
        {
            RefreshUi(false);
        }
    }

    /// <summary>
    /// Opens advanced options on the gameplay tab to configure custom board dimensions.
    /// </summary>
    /// <returns>A task that completes when settings are saved and a new game starts.</returns>
    private async Task OpenAdvancedOptionsForCustomSizeAsync()
    {
        var dialog = new AdvancedOptionsDialog(_settings, _session.HasScore(), _soundManager, AdvancedOptionsDialog.TabGameplay);
        var saved = await dialog.ShowAsync();
        if (!saved)
        {
            return;
        }

        _settings = dialog.ResultSettings!;
        _session.Settings = _settings;
        SettingsPersistence.Save(_settings);
        UpdateMenuChecks();
        ConfigureSound();
        await StartNewGameAsync(false);
    }

    /// <summary>
    /// Determines whether two settings snapshots differ in gameplay-affecting fields.
    /// </summary>
    /// <param name="a">First settings snapshot.</param>
    /// <param name="b">Second settings snapshot.</param>
    /// <returns>True when board size, colors, difficulty, randomness, or timer settings differ.</returns>
    private static bool GameplayChanged(GameSettings a, GameSettings b) =>
        a.BoardSizePresetValue != b.BoardSizePresetValue ||
        a.CustomWidth != b.CustomWidth ||
        a.CustomHeight != b.CustomHeight ||
        a.NumColors != b.NumColors ||
        a.GenerationDifficultyValue != b.GenerationDifficultyValue ||
        a.Randomness != b.Randomness ||
        a.TimerEnabled != b.TimerEnabled ||
        a.TimerSeconds != b.TimerSeconds;

    /// <summary>
    /// Toggles objectives panel visibility with a slide animation based on menu check state.
    /// </summary>
    private async void ToggleObjectives()
    {
        bool show = _objectivesMenuItem?.IsChecked == true;
        if (show)
        {
            ObjectivesPanel.Update(_session.Board, _settings);
            await ObjectivesPanel.ShowAnimatedAsync();
        }
        else
        {
            await ObjectivesPanel.HideAnimatedAsync();
        }
    }

    /// <summary>
    /// Opens or closes the high scores window based on menu check state.
    /// </summary>
    private void ToggleHighScores()
    {
        if (_highScoresMenuItem?.IsChecked == true)
        {
            if (_highScoresWindow is null)
            {
                _highScoresWindow = new HighScoresWindow();
                _highScoresWindow.Closed += (_, _) =>
                {
                    _highScoresWindow = null;
                    if (_highScoresMenuItem is not null)
                    {
                        _highScoresMenuItem.IsChecked = false;
                    }
                };
            }

            _highScoresWindow.Refresh();
            _highScoresWindow.ApplyAppTheme();
            _highScoresWindow.Activate();
        }
        else
        {
            _highScoresWindow?.Close();
        }
    }

    /// <summary>
    /// Applies sound and music settings to the sound manager.
    /// </summary>
    private void ConfigureSound()
    {
        _soundManager.Configure(
            _settings.SoundEnabled,
            _settings.SoundEffectsVolume,
            _settings.BackgroundMusicEnabled,
            _settings.BackgroundMusicVolume);
    }

    /// <summary>
    /// Creates or activates the help window.
    /// </summary>
    private void ShowHelp()
    {
        if (_helpWindow is null)
        {
            _helpWindow = new HelpWindow();
            _helpWindow.Closed += (_, _) => _helpWindow = null;
        }

        _helpWindow.ApplyAppTheme();
        _helpWindow.Activate();
    }

    /// <summary>
    /// Synchronizes toggle and radio menu items with current settings and panel visibility.
    /// </summary>
    private void UpdateMenuChecks()
    {
        if (_soundMenuItem is not null)
        {
            _soundMenuItem.IsChecked = _settings.SoundEnabled;
        }

        if (_musicMenuItem is not null)
        {
            _musicMenuItem.IsChecked = _settings.BackgroundMusicEnabled;
        }

        if (_animationsMenuItem is not null)
        {
            _animationsMenuItem.IsChecked = _settings.AnimationsEnabled;
        }

        if (_objectivesMenuItem is not null)
        {
            _objectivesMenuItem.IsChecked = ObjectivesPanel.Visibility == Visibility.Visible;
        }

        SetRadioChecked("Language", _settings.LanguageCode);
        SetRadioChecked("Difficulty", _settings.GenerationDifficultyValue);
        SetRadioChecked("Size", _settings.BoardSizePresetValue);
        SetRadioChecked("Skin", _settings.SkinValue);
        RefreshStatusLabels();
    }

    /// <summary>
    /// Sets the checked radio item in the named group to match the given value.
    /// </summary>
    /// <param name="groupName">Radio group name shared by submenu items.</param>
    /// <param name="value">Tag value that should be checked.</param>
    private void SetRadioChecked(string groupName, object value)
    {
        foreach (var menuItem in MainMenuBar.Items.OfType<MenuBarItem>())
        {
            foreach (var item in menuItem.Items)
            {
                if (item is MenuFlyoutSubItem sub)
                {
                    foreach (var subItem in sub.Items.OfType<RadioMenuFlyoutItem>())
                    {
                        if (subItem.GroupName == groupName)
                        {
                            subItem.IsChecked = Equals(subItem.Tag, value);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates score and selection labels and refreshes the main window title.
    /// </summary>
    private void RefreshStatusLabels()
    {
        SetScore(_session?.Score ?? 0);
        ClearSelection();
        if (App.MainWindowContent is MainWindow window)
        {
            window.UpdateTitle();
        }
    }

    /// <summary>
    /// Updates the status bar with the selected group size and potential points.
    /// </summary>
    /// <param name="count">Number of tiles in the selected group.</param>
    /// <param name="points">Points awarded if the group were removed.</param>
    private void SetSelection(int count, int points)
    {
        CountLabel.Text = Messages.Format("status.count", count);
        PointsLabel.Text = Messages.Format("status.points", points);
    }

    /// <summary>
    /// Clears the selection status labels by showing zero count and points.
    /// </summary>
    private void ClearSelection() => SetSelection(0, 0);

    /// <summary>
    /// Updates the score label with the current session score.
    /// </summary>
    /// <param name="score">Current player score.</param>
    private void SetScore(int score) => ScoreLabel.Text = Messages.Format("status.score", score);

    /// <summary>
    /// Starts the countdown timer when enabled in settings, or clears the timer label.
    /// </summary>
    private void StartTimerIfEnabled()
    {
        if (!_settings.TimerEnabled)
        {
            TimerLabel.Text = "";
            return;
        }

        _remainingSeconds = _settings.TimerSeconds;
        UpdateTimerLabel();
        _gameTimer.Start();
    }

    /// <summary>
    /// Stops the countdown timer without resetting remaining seconds.
    /// </summary>
    private void StopTimer() => _gameTimer.Stop();

    /// <summary>
    /// Decrements remaining time each second and triggers game over when time expires.
    /// </summary>
    /// <param name="sender">The dispatcher timer.</param>
    /// <param name="e">Tick event arguments.</param>
    private void GameTimer_Tick(object? sender, object e)
    {
        _remainingSeconds--;
        UpdateTimerLabel();
        if (_remainingSeconds <= 0)
        {
            StopTimer();
            _ = HandleGameOverAsync(true);
        }
    }

    /// <summary>
    /// Formats and displays the remaining time as minutes and seconds.
    /// </summary>
    private void UpdateTimerLabel()
    {
        int minutes = _remainingSeconds / 60;
        int seconds = _remainingSeconds % 60;
        TimerLabel.Text = Messages.Format("status.time", minutes, seconds);
    }

    /// <summary>
    /// Shows or hides the board generation loading ring.
    /// </summary>
    /// <param name="show">True to display the loading indicator.</param>
    private void ShowLoading(bool show)
    {
        LoadingRing.IsActive = show;
        LoadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Enables or disables menu and board interaction during long-running operations.
    /// </summary>
    /// <param name="enabled">True to allow user input.</param>
    private void SetUiEnabled(bool enabled)
    {
        MainMenuBar.IsEnabled = enabled;
        BoardPanel.IsEnabled = enabled;
    }

    /// <summary>
    /// Prompts the user before changing settings mid-game after the first move.
    /// </summary>
    /// <param name="action">Localized description of the settings change for the dialog body.</param>
    /// <returns>True when no confirmation is needed or the user chooses to proceed.</returns>
    private async Task<bool> ConfirmSettingsChangeAsync(string action)
    {
        if (!_session.HasStarted())
        {
            return true;
        }

        var dialog = DialogHelper.CreateDialog(
            Messages.Get("confirm.warning.title"),
            Messages.Format("confirm.settingsChange", action));
        dialog.PrimaryButtonText = Messages.Get("button.proceed");
        dialog.CloseButtonText = Messages.Get("button.cancel");
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Prompts the user before abandoning a game that already has a score.
    /// </summary>
    /// <param name="action">Localized description of the action for the dialog body.</param>
    /// <returns>True when no score exists or the user chooses to proceed.</returns>
    private async Task<bool> ConfirmAbandonGameAsync(string action)
    {
        if (!_session.HasScore())
        {
            return true;
        }

        var dialog = DialogHelper.CreateDialog(
            Messages.Get("confirm.title"),
            Messages.Format("confirm.abandon", action));
        dialog.PrimaryButtonText = Messages.Get("button.proceed");
        dialog.CloseButtonText = Messages.Get("button.cancel");
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Displays a modal error dialog with the given message body.
    /// </summary>
    /// <param name="message">Localized error message to show.</param>
    /// <returns>A task that completes when the dialog is dismissed.</returns>
    private async Task ShowErrorAsync(string message)
    {
        var dialog = DialogHelper.CreateDialog(Messages.Get("error.title"), message);
        dialog.CloseButtonText = Messages.Get("button.close");
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Applies the UI theme to the page, main window content, and auxiliary windows.
    /// </summary>
    /// <param name="theme">Light, dark, or system theme setting.</param>
    private void ApplyTheme(GameSettings.UiTheme theme)
    {
        App.CurrentUiTheme = theme;
        ThemeHelper.ApplyTheme(theme, this);
        if (App.MainWindowContent.Content is FrameworkElement windowContent)
        {
            ThemeHelper.ApplyTheme(theme, windowContent);
        }

        _helpWindow?.ApplyAppTheme();
        _highScoresWindow?.ApplyAppTheme();
        ObjectivesPanel.RefreshTheme();
    }
}
