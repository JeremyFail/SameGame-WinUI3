using SameGame.Dialogs;

namespace SameGame.UI;

/// <summary>
/// Installs global exception handlers and shows the fatal error dialog on unhandled failures.
/// </summary>
public static class FatalErrorHandler
{
    private static bool _handling;

    /// <summary>
    /// Registers unhandled exception handlers on the application and task scheduler.
    /// </summary>
    /// <param name="app">The WinUI application instance.</param>
    public static void Install(Microsoft.UI.Xaml.Application app)
    {
        app.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            _ = HandleAsync(e.Exception);
        };
    }

    /// <summary>
    /// Handles an unhandled XAML exception by marking it handled and dispatching to the async handler.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The unhandled exception event arguments.</param>
    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _ = HandleAsync(e.Exception);
    }

    /// <summary>
    /// Writes a crash log, prepares the window, shows the fatal error dialog, and exits the app.
    /// </summary>
    /// <param name="exception">The unhandled exception to report.</param>
    /// <returns>A task representing the asynchronous error handling flow.</returns>
    private static async Task HandleAsync(Exception exception)
    {
        if (_handling)
        {
            return;
        }

        _handling = true;
        try
        {
            // Best-effort crash log to local app data.
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
