using Windows.Storage;

namespace SameGame.Persistence;

/// <summary>
/// Resolves local application-data folder paths for SameGame persistence files.
/// </summary>
internal static class AppDataPaths
{
    /// <summary>
    /// Gets the root folder path where SameGame stores local data.
    /// </summary>
    /// <returns>
    /// The absolute path to the SameGame app-data folder, preferring the WinUI local folder
    /// and falling back to the user local application data directory when unavailable.
    /// </returns>
    public static string GetAppFolder()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "SameGame");
        }
        catch (InvalidOperationException)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SameGame");
        }
        catch (UnauthorizedAccessException)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SameGame");
        }
    }

    /// <summary>
    /// Combines the app-data folder with a file name to produce a full file path.
    /// </summary>
    /// <param name="fileName">The file name (without directory segments) to resolve.</param>
    /// <returns>The absolute path to the file inside the SameGame app-data folder.</returns>
    public static string GetFilePath(string fileName) => Path.Combine(GetAppFolder(), fileName);
}
