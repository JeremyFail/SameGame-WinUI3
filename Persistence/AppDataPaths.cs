using Windows.Storage;

namespace SameGame.Persistence;

internal static class AppDataPaths
{
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

    public static string GetFilePath(string fileName) => Path.Combine(GetAppFolder(), fileName);
}
