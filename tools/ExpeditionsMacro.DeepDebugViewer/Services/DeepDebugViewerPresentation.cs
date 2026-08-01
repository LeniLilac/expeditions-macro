using System.Globalization;
using System.IO;

namespace ExpeditionsMacro.DeepDebugViewer.Services;

internal static class DeepDebugViewerPresentation
{
    public static int FirstRetainedFrameIndex(
        DeepDebugArchive archive) =>
        archive.Frames
            .Select((frame, index) =>
                new { frame.EntryExists, Index = index })
            .FirstOrDefault(value =>
                value.EntryExists)?.Index ?? 0;

    public static string ArchiveCounts(
        DeepDebugArchive archive) =>
        archive.Manifest.UsesRollingFrameRetention
            ? $"{archive.Frames.Count:N0} frame records  ·  {archive.Manifest.RetainedFrameImages:N0} images retained  ·  {archive.Events.Count:N0} events"
            : $"{archive.Frames.Count:N0} frames  ·  {archive.Events.Count:N0} events";

    public static string DefaultDiagnosticsDirectory()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ExpeditionsMacro",
            "diagnostics");
        return Directory.Exists(path)
            ? path
            : Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
    }

    public static string FriendlyOutcome(string value) =>
        value.ToLowerInvariant() switch
        {
            "success" => "Success",
            "error" => "Error",
            "canceled" => "Canceled",
            _ => "Unknown",
        };

    public static string FormatRuntime(TimeSpan? runtime) =>
        runtime is null
            ? "unknown"
            : runtime.Value.TotalHours >= 1
                ? runtime.Value.ToString(
                    @"hh\:mm\:ss",
                    CultureInfo.InvariantCulture)
                : runtime.Value.ToString(
                    @"mm\:ss\.fff",
                    CultureInfo.InvariantCulture);

    public static string FriendlyToken(string value)
    {
        string spaced = value
            .Replace('_', ' ')
            .Replace('.', ' ');
        return CultureInfo.InvariantCulture
            .TextInfo
            .ToTitleCase(spaced);
    }

    public static string FormatBytes(long bytes)
    {
        const double gibibyte =
            1024d * 1024 * 1024;
        const double mebibyte =
            1024d * 1024;
        return bytes >= gibibyte
            ? $"{bytes / gibibyte:0.##} GB"
            : bytes >= mebibyte
                ? $"{bytes / mebibyte:0.#} MB"
                : $"{bytes / 1024d:0.#} KB";
    }
}
