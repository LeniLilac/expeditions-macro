namespace ExpeditionsMacro.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string DetectorPack => Path.Combine(
        RepositoryRoot,
        "detector-packs",
        "anime-expeditions-expeditions",
        "1.0.2");

    public static string LegacyDetectorPack => Path.Combine(
        RepositoryRoot,
        "detector-packs",
        "anime-expeditions-expeditions",
        "1.0.0");

    public static string Datasets => Path.Combine(
        RepositoryRoot,
        "datasets",
        "anime-expeditions",
        "expeditions");

    public static string ChallengeDatasets => Path.Combine(
        RepositoryRoot,
        "datasets",
        "anime-expeditions",
        "challenges");

    public static string StageDatasets => Path.Combine(
        RepositoryRoot,
        "datasets",
        "anime-expeditions",
        "stages");

    public static string NavigationVariantDatasets => Path.Combine(
        RepositoryRoot,
        "datasets",
        "anime-expeditions",
        "navigation-variants");

    public static string CameraRotations => Path.Combine(
        RepositoryRoot,
        "datasets",
        "anime-expeditions",
        "camera-rotations");

    public static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "ExpeditionsMacro.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteTemporaryDirectory(string path)
    {
        const int maximumAttempts = 5;
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException &&
                attempt < maximumAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ExpeditionsMacro.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Expeditions Macro repository root.");
    }
}
