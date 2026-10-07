namespace AnalisisSentimiento.Infrastructure.Configuration;

public static class LocalAppStorage
{
    private const string ApplicationFolderName = "EmocionesULEAM";

    public static string GetAppDataDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationFolderName
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ApplicationFolderName
            ),
            Path.Combine(Path.GetTempPath(), ApplicationFolderName),
            Path.Combine(AppContext.BaseDirectory, ".emotions-uleam"),
        };

        return candidates.FirstOrDefault(CanWriteToDirectory)
            ?? candidates[^1];
    }

    public static string GetKeysDirectory() => Path.Combine(GetAppDataDirectory(), ".keys");

    public static string GetCredentialsFilePath() =>
        Path.Combine(GetKeysDirectory(), "api-credentials.dat");

    private static bool CanWriteToDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");
            using (File.Create(probePath)) { }
            File.Delete(probePath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
