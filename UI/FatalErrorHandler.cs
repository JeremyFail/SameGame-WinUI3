using SameGame.Dialogs;

namespace SameGame.UI;

public static class FatalErrorHandler
{
    private static bool _handling;

    public static void Install(Microsoft.UI.Xaml.Application app)
    {
        app.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            _ = HandleAsync(e.Exception);
        };
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _ = HandleAsync(e.Exception);
    }

    private static async Task HandleAsync(Exception exception)
    {
        if (_handling)
        {
            return;
        }

        _handling = true;
        try
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SameGame");
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, "crash.log"), CrashReportFormatter.Format(exception));
            }
            catch
            {
                // Best-effort crash log.
            }

            await DialogHelper.PrepareForModalDialogAsync();
            await FatalErrorDialog.ShowAsync(exception);
        }
        catch
        {
            // If the dialog itself fails, fall through to exit.
        }
        finally
        {
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }
}
