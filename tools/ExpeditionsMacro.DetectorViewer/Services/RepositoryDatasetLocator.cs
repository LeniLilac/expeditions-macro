using System.IO;

namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed record RepositoryDatasetLocation(
    string RepositoryRoot,
    string DatasetRoot);

public static class RepositoryDatasetLocator
{
    public static RepositoryDatasetLocation? Find() =>
        Find(
            [
                Environment.CurrentDirectory,
                AppContext.BaseDirectory,
            ]);

    internal static RepositoryDatasetLocation? Find(
        IEnumerable<string> startPaths)
    {
        ArgumentNullException.ThrowIfNull(
            startPaths);
        HashSet<string> visited =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string startPath in startPaths)
        {
            if (string.IsNullOrWhiteSpace(
                    startPath))
            {
                continue;
            }
            DirectoryInfo? current =
                new(Path.GetFullPath(startPath));
            if (!current.Exists &&
                current.Parent is not null)
            {
                current = current.Parent;
            }
            while (current is not null &&
                   visited.Add(current.FullName))
            {
                string solution = Path.Combine(
                    current.FullName,
                    "ExpeditionsMacro.slnx");
                string datasets = Path.Combine(
                    current.FullName,
                    "datasets");
                if (File.Exists(solution) &&
                    Directory.Exists(datasets))
                {
                    return new RepositoryDatasetLocation(
                        current.FullName,
                        datasets);
                }
                current = current.Parent;
            }
        }
        return null;
    }
}
