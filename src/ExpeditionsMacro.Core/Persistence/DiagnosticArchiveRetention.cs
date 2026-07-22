using System.Globalization;

namespace ExpeditionsMacro.Core.Persistence;

public static class DiagnosticArchiveRetention
{
    private const int TimestampLength = 15;

    public static int PruneAutomaticErrorArchives(string diagnosticsDirectory, int maximumArchives)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticsDirectory);
        if (maximumArchives < 1) throw new ArgumentOutOfRangeException(nameof(maximumArchives));
        if (!Directory.Exists(diagnosticsDirectory)) return 0;

        FileInfo[] archives = new DirectoryInfo(diagnosticsDirectory)
            .EnumerateFiles("error-*.zip", SearchOption.TopDirectoryOnly)
            .Where(file => IsAutomaticErrorArchive(file.Name))
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int removeCount = Math.Max(0, archives.Length - maximumArchives);
        int removed = 0;
        foreach (FileInfo archive in archives.Take(removeCount))
        {
            try
            {
                archive.Delete();
                removed++;
            }
            catch (IOException)
            {
                // A diagnostic being viewed or uploaded can remain until the next cleanup pass.
            }
            catch (UnauthorizedAccessException)
            {
                // Retention is best effort and must not invalidate the newly written archive.
            }
        }

        return removed;
    }

    public static bool IsAutomaticErrorArchive(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith("error-", StringComparison.OrdinalIgnoreCase) ||
            name.Length <= TimestampLength)
        {
            return false;
        }

        string prefix = name[..^TimestampLength];
        string timestamp = name[^TimestampLength..];
        return prefix.EndsWith("-macro-", StringComparison.OrdinalIgnoreCase) &&
            DateTime.TryParseExact(
                timestamp,
                "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
    }
}
