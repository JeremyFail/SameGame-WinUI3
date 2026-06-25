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

    public MainPage()
    {
        InitializeComponent();
        _gameTimer.Tick += GameTimer_Tick;
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

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

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        PersistSessionState();
        _gameTimer.Stop();
        _soundManager.Shutdown();
    }

    public void PersistSessionState() => SettingsPersistence.Save(_settings);

    private void BuildMenus()
    {
        MainMenuBar.Items.Clear();

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

    private void InstallKeyboardShortcuts()
    {
        AddAccelerator(global::Windows.System.VirtualKey.Z, global::Windows.System.VirtualKeyModifiers.Control, () => UndoMove());
        AddAccelerator(global::Windows.System.VirtualKey.Y, global::Windows.System.VirtualKeyModifiers.Control, () => RedoMove());
        AddAccelerator(global::Windows.System.VirtualKey.F2, global::Windows.System.VirtualKeyModifiers.None, () => _ = StartNewGameAsync(true));
        AddAccelerator(global::Windows.System.VirtualKey.F1, global::Windows.System.VirtualKeyModifiers.None, ShowHelp);
    }

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

    private void UpdateObjectivesIfVisible(Board board)
    {
        if (ObjectivesPanel.Visibility == Visibility.Visible)
        {
            ObjectivesPanel.Update(board, _settings);
        }
    }

    private void WireBoardEvents()
    {
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

    private void RestartGame(bool confirmIfNeeded) =>
        _ = RestartGameAsync(confirmIfNeeded);

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

    private void RefreshUi(bool boardDimensionsChanged)
    {
        BoardPanel.Configure(_session.Board, _settings);
        SetScore(_session.Score);
        ClearSelection();
        SetMoveActionsEnabled(!BoardPanel.IsAnimating);
        UpdateObjectivesIfVisible(_session.Board);
    }

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

    private void ChangeLanguage(string code)
    {
        _settings.LanguageCode = code;
        Messages.SetLanguage(code);
        SettingsPersistence.Save(_settings);
        BuildMenus();
        UpdateMenuChecks();
        RefreshStatusLabels();
    }

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

    internal void PrepareForFatalErrorDialog()
    {
        MainMenuBar.Visibility = Visibility.Collapsed;
        _ = BoardPanel.Focus(FocusState.Programmatic);
    }

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

    private void ChangeSkin(GameSettings.Skin skin)
    {
        _settings.SkinValue = skin;
        SettingsPersistence.Save(_settings);
        BoardPanel.SettingsChanged();
        UpdateMenuChecks();
        UpdateObjectivesIfVisible(_session.Board);
    }

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

    private static bool GameplayChanged(GameSettings a, GameSettings b) =>
        a.BoardSizePresetValue != b.BoardSizePresetValue ||
        a.CustomWidth != b.CustomWidth ||
        a.CustomHeight != b.CustomHeight ||
        a.NumColors != b.NumColors ||
        a.GenerationDifficultyValue != b.GenerationDifficultyValue ||
        a.Randomness != b.Randomness ||
        a.TimerEnabled != b.TimerEnabled ||
        a.TimerSeconds != b.TimerSeconds;

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

    private void ConfigureSound()
    {
        _soundManager.Configure(
            _settings.SoundEnabled,
            _settings.SoundEffectsVolume,
            _settings.BackgroundMusicEnabled,
            _settings.BackgroundMusicVolume);
    }

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

    private void RefreshStatusLabels()
    {
        SetScore(_session?.Score ?? 0);
        ClearSelection();
        if (App.MainWindowContent is MainWindow window)
        {
            window.UpdateTitle();
        }
    }

    private void SetSelection(int count, int points)
    {
        CountLabel.Text = Messages.Format("status.count", count);
        PointsLabel.Text = Messages.Format("status.points", points);
    }

    private void ClearSelection() => SetSelection(0, 0);

    private void SetScore(int score) => ScoreLabel.Text = Messages.Format("status.score", score);

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

    private void StopTimer() => _gameTimer.Stop();

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

    private void UpdateTimerLabel()
    {
        int minutes = _remainingSeconds / 60;
        int seconds = _remainingSeconds % 60;
        TimerLabel.Text = Messages.Format("status.time", minutes, seconds);
    }

    private void ShowLoading(bool show)
    {
        LoadingRing.IsActive = show;
        LoadingRing.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetUiEnabled(bool enabled)
    {
        MainMenuBar.IsEnabled = enabled;
        BoardPanel.IsEnabled = enabled;
    }

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

    private async Task ShowErrorAsync(string message)
    {
        var dialog = DialogHelper.CreateDialog(Messages.Get("error.title"), message);
        dialog.CloseButtonText = Messages.Get("button.close");
        await dialog.ShowAsync();
    }

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
