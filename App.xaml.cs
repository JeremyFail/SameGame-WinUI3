using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using SameGame.Model;
using SameGame.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SameGame;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Gets the main application window used as the host for dialogs and navigation.
    /// </summary>
    public static Window MainWindowContent { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="XamlRoot"/> of the main window content, used to anchor modal dialogs.
    /// </summary>
    public static XamlRoot? DialogXamlRoot =>
        (MainWindowContent.Content as FrameworkElement)?.XamlRoot;

    /// <summary>
    /// Gets or sets the current UI theme applied across application windows.
    /// </summary>
    public static GameSettings.UiTheme CurrentUiTheme { get; set; } = GameSettings.UiTheme.System;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        FatalErrorHandler.Install(this);
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindowContent = _window;
        _window.Activate();
    }
}
