using Microsoft.UI.Xaml;
using SameGame.UI;

namespace SameGame;

public sealed partial class MainWindow : Window
{
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

    public void UpdateTitle()
    {
        Title = SameGame.I18n.Messages.Get("app.name");
        AppTitleBar.Title = Title;
    }
}
