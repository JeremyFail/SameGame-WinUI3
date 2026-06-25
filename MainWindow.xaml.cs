using Microsoft.UI.Xaml;
using SameGame.UI;

namespace SameGame;

/// <summary>
/// The primary application window hosting navigation to the main game page.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main window, configures the title bar, and navigates to the game page.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        WindowHelper.Configure(this, 900, 700, minimumWidth: 720, minimumHeight: 560);

        RootFrame.Navigate(typeof(MainPage));
        RootFrame.Navigated += (_, _) => UpdateTitle();
        Closed += (_, _) =>
        {
            if (RootFrame.Content is MainPage page)
            {
                page.PersistSessionState();
            }
        };
    }

    /// <summary>
    /// Updates the window and custom title bar text from the localized application name.
    /// </summary>
    public void UpdateTitle()
    {
        Title = SameGame.I18n.Messages.Get("app.name");
        AppTitleBar.Title = Title;
    }
}
