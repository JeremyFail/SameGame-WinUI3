using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SameGame.I18n;
using SameGame.Model;
using SameGame.Sound;
using SameGame.UI;

namespace SameGame.Dialogs;

public sealed class AdvancedOptionsDialog
{
    public const int TabGameplay = 1;
    private const double ShellWidth = SettingsLayoutHelper.DialogShellWidth;

    private const int SectionAppearance = 0;
    private const int SectionBoard = 1;
    private const int SectionSound = 2;

    private readonly GameSettings _workingCopy;
    private readonly GameSettings _original;
    private readonly SoundManager _soundManager;
    private readonly int _initialSection;

    private ToggleSwitch? _backgroundMusicEnabledBox;
    private Slider? _backgroundMusicVolumeSlider;
    private TextBlock? _backgroundMusicVolumeLabel;
    private ToggleSwitch? _soundEnabledBox;
    private Slider? _soundEffectsVolumeSlider;
    private ListView? _navList;
    private ScrollViewer? _pageScroll;
    private StackPanel? _tilePreviewRow;
    private SettingsDialogShellGrid? _shell;

    public GameSettings? ResultSettings { get; private set; }

    public AdvancedOptionsDialog(GameSettings settings, bool gameHasScore, SoundManager soundManager, int initialSection = 0)
    {
        _workingCopy = (GameSettings)settings.Clone();
        _original = settings;
        _ = gameHasScore;
        _soundManager = soundManager;
        _initialSection = initialSection;
    }

    public async Task<bool> ShowAsync()
    {
        var shell = BuildShell();
        _shell = shell;
        DialogHelper.ApplyTheme(shell);
        var dialog = DialogHelper.CreateDialog(Messages.Get("advanced.title"), shell);
        shell.AttachHostDialog(dialog);
        dialog.PrimaryButtonText = Messages.Get("advanced.button.save");
        dialog.CloseButtonText = Messages.Get("advanced.button.cancel");
        dialog.DefaultButton = ContentDialogButton.Primary;

        dialog.CloseButtonClick += (_, _) => RestoreSoundSettings();

        dialog.Opened += (_, _) =>
        {
            _shell?.InvalidateMeasure();
            _shell?.InvalidateArrange();
        };

        void OnLayoutChanged(object sender, SizeChangedEventArgs e)
        {
            _shell?.InvalidateMeasure();
            _shell?.InvalidateArrange();
        }

        dialog.SizeChanged += OnLayoutChanged;
        var layoutHost = App.MainWindowContent.Content as FrameworkElement;
        if (layoutHost is not null)
        {
            layoutHost.SizeChanged += OnLayoutChanged;
        }

        dialog.Closed += (_, _) =>
        {
            dialog.SizeChanged -= OnLayoutChanged;
            if (layoutHost is not null)
            {
                layoutHost.SizeChanged -= OnLayoutChanged;
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            RestoreSoundSettings();
            return false;
        }

        ResultSettings = _workingCopy;
        return true;
    }

    private SettingsDialogShellGrid BuildShell()
    {
        _navList = new ListView
        {
            Width = SettingsLayoutHelper.NavColumnWidth,
            SelectionMode = ListViewSelectionMode.Single,
            Items =
            {
                Messages.Get("advanced.tab.appearance"),
                Messages.Get("advanced.tab.gameplay"),
                Messages.Get("advanced.tab.sound")
            }
        };

        _pageScroll = SettingsLayoutHelper.CreatePageScrollViewer();
        _navList.SelectionChanged += (_, _) =>
        {
            if (_navList.SelectedIndex >= 0)
            {
                ShowSection(_navList.SelectedIndex);
            }
        };

        var grid = new SettingsDialogShellGrid
        {
            Width = ShellWidth,
            MinWidth = ShellWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnSpacing = SettingsLayoutHelper.NavColumnSpacing;

        var navBorder = new Border
        {
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = _navList
        };
        ThemeResources.ApplyCardStyle(navBorder);

        Grid.SetRow(navBorder, 0);
        Grid.SetColumn(navBorder, 0);
        Grid.SetRow(_pageScroll, 0);
        Grid.SetColumn(_pageScroll, 1);
        grid.Children.Add(navBorder);
        grid.Children.Add(_pageScroll);
        grid.AttachPageScroll(_pageScroll);

        _navList.SelectedIndex = Math.Clamp(_initialSection, 0, 2);
        return grid;
    }

    private void ShowSection(int section)
    {
        _pageScroll!.Content = section switch
        {
            SectionAppearance => BuildAppearancePage(),
            SectionBoard => BuildBoardPage(),
            SectionSound => BuildSoundPage(),
            _ => BuildAppearancePage()
        };
        _shell?.ApplyScrollConstraintsToCurrentPage();
        _shell?.InvalidateMeasure();
    }

    private UIElement BuildAppearancePage()
    {
        var themeBox = CreateEnumComboBox<GameSettings.UiTheme>(Messages.Get("advanced.label.uiTheme"), _workingCopy.UiThemeValue);
        themeBox.SelectionChanged += (_, _) => _workingCopy.UiThemeValue = (GameSettings.UiTheme)themeBox.SelectedIndex;

        var backgroundBox = CreateEnumComboBox<GameSettings.Background>(Messages.Get("advanced.label.background"), _workingCopy.BackgroundValue);
        backgroundBox.SelectionChanged += (_, _) => _workingCopy.BackgroundValue = (GameSettings.Background)backgroundBox.SelectedIndex;

        var skinBox = CreateEnumComboBox<GameSettings.Skin>(Messages.Get("advanced.label.skin"), _workingCopy.SkinValue);
        skinBox.SelectionChanged += (_, _) =>
        {
            _workingCopy.SkinValue = (GameSettings.Skin)skinBox.SelectedIndex;
            RebuildTilePreviewRow();
        };

        var animations = new ToggleSwitch
        {
            IsOn = _workingCopy.AnimationsEnabled
        };
        animations.Toggled += (_, _) => _workingCopy.AnimationsEnabled = animations.IsOn;

        var languageBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var languageCodes = LocaleCatalog.AvailableLanguages().ToList();
        foreach (var (_, label) in languageCodes)
        {
            languageBox.Items.Add(label);
        }

        languageBox.SelectedIndex = Math.Max(0, languageCodes.FindIndex(kvp => kvp.Key == _workingCopy.LanguageCode));
        languageBox.SelectionChanged += (_, _) =>
        {
            if (languageBox.SelectedIndex >= 0)
            {
                _workingCopy.LanguageCode = languageCodes[languageBox.SelectedIndex].Key;
            }
        };

        var colorsBox = new NumberBox
        {
            Header = Messages.Get("advanced.label.numColors"),
            Value = _workingCopy.NumColors,
            Minimum = GameSettings.MinColors,
            Maximum = GameSettings.MaxColors,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        colorsBox.ValueChanged += (_, _) =>
        {
            _workingCopy.NumColors = (int)colorsBox.Value;
            RebuildTilePreviewRow();
        };

        var clickHint = new TextBlock
        {
            Text = Messages.Get("advanced.colors.clickHint"),
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        };
        _tilePreviewRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        PopulateTilePreviewRow(_tilePreviewRow);

        var resetColors = new Button
        {
            Content = Messages.Get("advanced.button.resetColors"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        resetColors.Click += (_, _) =>
        {
            _workingCopy.ResetColorsToDefault();
            RebuildTilePreviewRow();
        };

        return SettingsLayoutHelper.CreatePage(
            SettingsLayoutHelper.CreateSection(Messages.Get("advanced.tab.appearance"), themeBox, backgroundBox, skinBox),
            SettingsLayoutHelper.CreateSection(Messages.Get("advanced.label.colors"), colorsBox, clickHint, _tilePreviewRow, resetColors),
            SettingsLayoutHelper.CreateSection(Messages.Get("advanced.label.animations"), animations),
            SettingsLayoutHelper.CreateSection(Messages.Get("advanced.label.language"), languageBox));
    }

    private UIElement BuildBoardPage()
    {
        var sizeBox = CreateEnumComboBox<GameSettings.BoardSizePreset>(null, _workingCopy.BoardSizePresetValue);
        var widthBox = new NumberBox
        {
            Header = Messages.Get("advanced.label.width"),
            Value = _workingCopy.BoardWidth(),
            Minimum = GameSettings.MinCustomDimension,
            Maximum = GameSettings.MaxCustomDimension,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var heightBox = new NumberBox
        {
            Header = Messages.Get("advanced.label.height"),
            Value = _workingCopy.BoardHeight(),
            Minimum = GameSettings.MinCustomDimension,
            Maximum = GameSettings.MaxCustomDimension,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var gameplayColorsBox = new NumberBox
        {
            Header = Messages.Get("advanced.label.numColors"),
            Value = _workingCopy.NumColors,
            Minimum = GameSettings.MinColors,
            Maximum = GameSettings.MaxColors,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var difficultyBox = CreateEnumComboBox<GameSettings.GenerationDifficulty>(
            Messages.Get("advanced.label.difficulty"),
            _workingCopy.GenerationDifficultyValue);

        var randomness = new Slider
        {
            Header = Messages.Get("advanced.label.randomness"),
            Minimum = 0,
            Maximum = 100,
            Value = _workingCopy.Randomness
        };
        var randomnessLabel = new TextBlock
        {
            Text = Messages.Format("advanced.label.volumePercent", _workingCopy.Randomness),
            Opacity = 0.8
        };
        randomness.ValueChanged += (_, _) =>
        {
            _workingCopy.Randomness = (int)randomness.Value;
            randomnessLabel.Text = Messages.Format("advanced.label.volumePercent", _workingCopy.Randomness);
        };

        var timerEnabled = new ToggleSwitch
        {
            IsOn = _workingCopy.TimerEnabled
        };
        var timerSeconds = new NumberBox
        {
            Header = Messages.Get("advanced.label.timeLimit"),
            Value = _workingCopy.TimerSeconds,
            Minimum = 1,
            Maximum = 3600,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        void UpdateTimerControlsEnabled()
        {
            timerSeconds.IsEnabled = timerEnabled.IsOn;
        }

        void UpdateSizeFields()
        {
            widthBox.Value = _workingCopy.BoardWidth();
            heightBox.Value = _workingCopy.BoardHeight();
        }

        void UpdateCustomSizeEnabled()
        {
            bool custom = _workingCopy.BoardSizePresetValue == GameSettings.BoardSizePreset.Custom;
            widthBox.IsEnabled = custom;
            heightBox.IsEnabled = custom;
            UpdateSizeFields();
        }

        sizeBox.SelectionChanged += (_, _) =>
        {
            _workingCopy.BoardSizePresetValue = (GameSettings.BoardSizePreset)sizeBox.SelectedIndex;
            UpdateCustomSizeEnabled();
        };
        widthBox.ValueChanged += (_, _) =>
        {
            if (_workingCopy.BoardSizePresetValue == GameSettings.BoardSizePreset.Custom)
            {
                _workingCopy.CustomWidth = (int)widthBox.Value;
            }
        };
        heightBox.ValueChanged += (_, _) =>
        {
            if (_workingCopy.BoardSizePresetValue == GameSettings.BoardSizePreset.Custom)
            {
                _workingCopy.CustomHeight = (int)heightBox.Value;
            }
        };
        gameplayColorsBox.ValueChanged += (_, _) => _workingCopy.NumColors = (int)gameplayColorsBox.Value;
        difficultyBox.SelectionChanged += (_, _) =>
            _workingCopy.GenerationDifficultyValue = (GameSettings.GenerationDifficulty)difficultyBox.SelectedIndex;
        timerEnabled.Toggled += (_, _) =>
        {
            _workingCopy.TimerEnabled = timerEnabled.IsOn;
            UpdateTimerControlsEnabled();
        };
        timerSeconds.ValueChanged += (_, _) => _workingCopy.TimerSeconds = (int)timerSeconds.Value;
        UpdateCustomSizeEnabled();
        UpdateTimerControlsEnabled();

        return SettingsLayoutHelper.CreatePage(
            SettingsLayoutHelper.CreateSection(
                Messages.Get("advanced.section.difficulty"),
                difficultyBox,
                gameplayColorsBox,
                randomness,
                randomnessLabel),
            SettingsLayoutHelper.CreateSection(
                Messages.Get("advanced.section.size"),
                sizeBox,
                SettingsLayoutHelper.CreateTwoColumnRow(widthBox, heightBox)),
            SettingsLayoutHelper.CreateSection(Messages.Get("advanced.section.timer"), timerEnabled, timerSeconds));
    }

    private void PopulateTilePreviewRow(StackPanel row)
    {
        row.Children.Clear();
        for (int i = 0; i < _workingCopy.NumColors; i++)
        {
            row.Children.Add(CreateColorPreviewHost(i));
        }
    }

    private Border CreateColorPreviewHost(int index)
    {
        var host = new Border
        {
            Width = 46,
            Height = 46,
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = CreatePreviewCanvas(index),
            Tag = index
        };
        ThemeResources.ApplyCardStyle(host);
        host.PointerPressed += (_, _) => ShowColorPickerFlyout(index);
        return host;
    }

    private CanvasControl CreatePreviewCanvas(int index)
    {
        const double previewSize = 40;
        int colorIndex = index;
        var canvas = new CanvasControl
        {
            Width = previewSize,
            Height = previewSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        canvas.Draw += (_, args) =>
        {
            args.DrawingSession.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            TileRenderer.DrawCell(args.DrawingSession, 0, 0, (float)previewSize, colorIndex, _workingCopy, false);
        };
        return canvas;
    }

    private void UpdateColorPreviewHost(int index)
    {
        if (_tilePreviewRow is null)
        {
            return;
        }

        foreach (var host in _tilePreviewRow.Children.OfType<Border>())
        {
            if (host.Tag is int tag && tag == index)
            {
                host.Child = CreatePreviewCanvas(index);
                return;
            }
        }
    }

    private void RebuildTilePreviewRow()
    {
        if (_tilePreviewRow is null)
        {
            return;
        }

        PopulateTilePreviewRow(_tilePreviewRow);
    }

    private void ApplyPickerColor(int index, Windows.UI.Color color)
    {
        _workingCopy.SetColorAt(index, color);
        UpdateColorPreviewHost(index);
    }

    private UIElement BuildSoundPage()
    {
        _soundEnabledBox = new ToggleSwitch
        {
            IsOn = _workingCopy.SoundEnabled
        };
        _soundEffectsVolumeSlider = new Slider
        {
            Header = Messages.Get("advanced.label.soundEffectsVolume"),
            Minimum = 0,
            Maximum = 100,
            Value = _workingCopy.SoundEffectsVolume
        };
        var volumeLabel = new TextBlock
        {
            Text = Messages.Format("advanced.label.volumePercent", _workingCopy.SoundEffectsVolume),
            Opacity = 0.8
        };

        _soundEnabledBox.Toggled += (_, _) =>
        {
            _workingCopy.SoundEnabled = _soundEnabledBox.IsOn;
            UpdateEffectsControlsEnabled();
            ApplyLiveSoundSettings(_soundEnabledBox);
        };
        _soundEffectsVolumeSlider.ValueChanged += (_, args) =>
        {
            if (args.NewValue is double value)
            {
                _workingCopy.SoundEffectsVolume = (int)value;
                volumeLabel.Text = Messages.Format("advanced.label.volumePercent", _workingCopy.SoundEffectsVolume);
            }
        };
        AttachSliderReleasePreview(_soundEffectsVolumeSlider, PreviewSoundEffectsVolume);

        var effectsSection = SettingsLayoutHelper.CreateSection(
            Messages.Get("advanced.label.soundSectionHeader"),
            _soundEnabledBox,
            _soundEffectsVolumeSlider,
            volumeLabel);

        UpdateEffectsControlsEnabled();

        if (!SoundManager.IsBackgroundMusicAvailable())
        {
            return SettingsLayoutHelper.CreatePage(effectsSection);
        }

        _backgroundMusicEnabledBox = new ToggleSwitch
        {
            IsOn = _workingCopy.BackgroundMusicEnabled
        };
        _backgroundMusicVolumeSlider = new Slider
        {
            Header = Messages.Get("advanced.label.backgroundMusicVolume"),
            Minimum = 0,
            Maximum = 100,
            Value = _workingCopy.BackgroundMusicVolume
        };
        _backgroundMusicVolumeLabel = new TextBlock
        {
            Text = Messages.Format("advanced.label.volumePercent", _workingCopy.BackgroundMusicVolume),
            Opacity = 0.8
        };

        _backgroundMusicEnabledBox.Toggled += (_, _) =>
        {
            _workingCopy.BackgroundMusicEnabled = _backgroundMusicEnabledBox.IsOn;
            UpdateMusicControlsEnabled();
            ApplyLiveSoundSettings(_soundEnabledBox!);
        };
        _backgroundMusicVolumeSlider.ValueChanged += (_, args) =>
        {
            if (args.NewValue is double value)
            {
                _workingCopy.BackgroundMusicVolume = (int)value;
                _backgroundMusicVolumeLabel.Text = Messages.Format("advanced.label.volumePercent", _workingCopy.BackgroundMusicVolume);
                ApplyLiveBackgroundMusicVolume();
            }
        };

        var musicSection = SettingsLayoutHelper.CreateSection(
            Messages.Get("advanced.label.backgroundMusicSectionHeader"),
            _backgroundMusicEnabledBox,
            _backgroundMusicVolumeSlider,
            _backgroundMusicVolumeLabel);
        UpdateMusicControlsEnabled();
        return SettingsLayoutHelper.CreatePage(effectsSection, musicSection);
    }

    private static ComboBox CreateEnumComboBox<T>(string? header, T selected) where T : struct, Enum
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        if (!string.IsNullOrEmpty(header))
        {
            box.Header = header;
        }

        foreach (T value in Enum.GetValues<T>())
        {
            box.Items.Add(UiLabels.Label(value));
        }

        box.SelectedIndex = Convert.ToInt32(selected);
        return box;
    }

    private void ShowColorPickerFlyout(int index)
    {
        if (_tilePreviewRow is null)
        {
            return;
        }

        var host = _tilePreviewRow.Children
            .OfType<Border>()
            .FirstOrDefault(b => b.Tag is int tag && tag == index);
        if (host is null)
        {
            return;
        }

        var picker = new ColorPicker
        {
            Color = _workingCopy.ColorAt(index),
            IsColorSpectrumVisible = true
        };
        var saveButton = new Button
        {
            Content = Messages.Get("button.save"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var panel = new StackPanel { Spacing = 8, MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = Messages.Get("colorPicker.title"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(picker);
        panel.Children.Add(saveButton);

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };
        flyout.Content = panel;
        picker.RegisterPropertyChangedCallback(
            ColorPicker.ColorProperty,
            (_, _) => ApplyPickerColor(index, picker.Color));
        picker.ColorChanged += (_, args) => ApplyPickerColor(index, args.NewColor);
        saveButton.Click += (_, _) => flyout.Hide();
        flyout.ShowAt(host);
    }

    private void UpdateEffectsControlsEnabled()
    {
        if (_soundEffectsVolumeSlider is not null && _soundEnabledBox is not null)
        {
            _soundEffectsVolumeSlider.IsEnabled = _soundEnabledBox.IsOn;
        }
    }

    private void UpdateMusicControlsEnabled()
    {
        if (_backgroundMusicVolumeSlider is not null && _backgroundMusicEnabledBox is not null)
        {
            _backgroundMusicVolumeSlider.IsEnabled = _backgroundMusicEnabledBox.IsOn;
        }
    }

    private void ApplyLiveBackgroundMusicVolume()
    {
        if (_backgroundMusicEnabledBox?.IsOn == true && _backgroundMusicVolumeSlider is not null)
        {
            _soundManager.SetBackgroundMusicVolume((int)_backgroundMusicVolumeSlider.Value);
        }
    }

    private void ApplyLiveSoundSettings(ToggleSwitch soundEnabled)
    {
        _soundManager.Configure(
            soundEnabled.IsOn,
            _soundEffectsVolumeSlider is not null ? (int)_soundEffectsVolumeSlider.Value : _workingCopy.SoundEffectsVolume,
            _backgroundMusicEnabledBox?.IsOn == true,
            _backgroundMusicVolumeSlider is not null
                ? (int)_backgroundMusicVolumeSlider.Value
                : _workingCopy.BackgroundMusicVolume);
    }

    private static void AttachSliderReleasePreview(Slider slider, Action onRelease)
    {
        bool interacting = false;
        long lastPreviewMs = 0;

        void TryPreview()
        {
            long now = Environment.TickCount64;
            if (now - lastPreviewMs < 80)
            {
                return;
            }

            lastPreviewMs = now;
            onRelease();
        }

        void OnInteractionEnded()
        {
            if (!interacting)
            {
                return;
            }

            interacting = false;
            TryPreview();
        }

        void HookThumb()
        {
            if (FindDescendant<Thumb>(slider) is Thumb thumb)
            {
                thumb.PointerReleased += (_, _) => OnInteractionEnded();
                thumb.PointerCaptureLost += (_, _) => OnInteractionEnded();
            }
        }

        if (slider.IsLoaded)
        {
            HookThumb();
        }
        else
        {
            slider.Loaded += (_, _) => HookThumb();
        }

        slider.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler((_, _) => interacting = true),
            handledEventsToo: true);
        slider.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler((_, _) => OnInteractionEnded()),
            handledEventsToo: true);
        slider.AddHandler(
            UIElement.PointerCaptureLostEvent,
            new PointerEventHandler((_, _) => OnInteractionEnded()),
            handledEventsToo: true);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void PreviewSoundEffectsVolume()
    {
        if (_soundEffectsVolumeSlider is null)
        {
            return;
        }

        if (_soundEnabledBox?.IsOn != true)
        {
            return;
        }

        int volume = (int)_soundEffectsVolumeSlider.Value;
        _soundManager.Configure(
            true,
            volume,
            _backgroundMusicEnabledBox?.IsOn == true,
            _backgroundMusicVolumeSlider is not null
                ? (int)_backgroundMusicVolumeSlider.Value
                : _workingCopy.BackgroundMusicVolume);
        _soundManager.PreviewEffect("remove1", volume);
    }

    private void RestoreSoundSettings()
    {
        _soundManager.Configure(
            _original.SoundEnabled,
            _original.SoundEffectsVolume,
            _original.BackgroundMusicEnabled,
            _original.BackgroundMusicVolume);
    }
}
