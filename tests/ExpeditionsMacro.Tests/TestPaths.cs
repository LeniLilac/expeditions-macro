namespace ExpeditionsMacro.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string DetectorPack => Path.Combine(
        RepositoryRoot,
        "detector-packs",
        "anime-expeditions-expeditions",
        "1.0.1");

    public static string Datasets => Path.Combine(
        RepositoryRoot,
        "datasets",
        "anime-expeditions",
        "expeditions");

    public static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "ExpeditionsMacro.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
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
