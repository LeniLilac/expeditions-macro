using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class DeepDebugFrameRetentionTests
{
    [Fact]
    public async Task RollingWindowPrunesOnlyFrameImagesAndKeepsFullTextHistory()
    {
        using TestDirectory directory = new();
        AppPaths paths = new(directory.Path);
        paths.EnsureCreated();
        string log = Path.Combine(
            paths.Logs,
            "macro-run.log");
        await File.WriteAllTextAsync(
            log,
            "full-run text before first frame\n");
        AppSettings settings = new()
        {
            DeepDebugEnabled = true,
            DeepDebugFrameRetentionMinutes = 15,
        };
        DateTimeOffset now = new(
            2026,
            7,
            31,
            12,
            0,
            0,
            TimeSpan.Zero);
        DeepDebugSessionService service = new(
            paths,
            () => settings,
            () => log,
            _ => { },
            _ => { },
            () => now);

        await service.RunOperationAsync(
            "Rolling frames",
            null,
            async _ =>
            {
                service.RecordFrame(
                    Frame(),
                    "old_frame");
                service.RecordEvent(
                    "workflow",
                    "old_text_event",
                    new { Message = "kept from start" });
                await File.AppendAllTextAsync(
                    log,
                    "full-run text after first frame\n");
                now = now.AddMinutes(16);
                service.RecordFrame(
                    Frame(),
                    "retained_frame");
                service.RecordEvent(
                    "workflow",
                    "new_text_event",
                    new { Message = "kept near end" });
            },
            CancellationToken.None);

        string archivePath = Assert.Single(
            Directory.EnumerateFiles(
                paths.Diagnostics,
                "deep-debug-*.zip"));
        using ZipArchive archive =
            ZipFile.OpenRead(archivePath);
        Assert.Null(
            archive.GetEntry(
                "frames/frame-000000001.png"));
        Assert.NotNull(
            archive.GetEntry(
                "frames/frame-000000002.png"));

        string events = await ReadEntryAsync(
            archive,
            "events.jsonl");
        Assert.Contains(
            "frames/frame-000000001.png",
            events,
            StringComparison.Ordinal);
        Assert.Contains(
            "frames/frame-000000002.png",
            events,
            StringComparison.Ordinal);
        Assert.Contains(
            "old_text_event",
            events,
            StringComparison.Ordinal);
        Assert.Contains(
            "new_text_event",
            events,
            StringComparison.Ordinal);

        string archivedLog = await ReadEntryAsync(
            archive,
            "macro-run-sanitized.log");
        Assert.Contains(
            "before first frame",
            archivedLog,
            StringComparison.Ordinal);
        Assert.Contains(
            "after first frame",
            archivedLog,
            StringComparison.Ordinal);

        using JsonDocument manifest = JsonDocument.Parse(
            await ReadEntryAsync(
                archive,
                "manifest.json"));
        Assert.Equal(
            2,
            manifest.RootElement
                .GetProperty("frames")
                .GetInt32());
        Assert.Equal(
            15,
            manifest.RootElement
                .GetProperty(
                    "frame_retention_minutes")
                .GetInt32());
        Assert.Equal(
            1,
            manifest.RootElement
                .GetProperty(
                    "retained_frame_images")
                .GetInt32());
        Assert.Equal(
            1,
            manifest.RootElement
                .GetProperty(
                    "discarded_frame_images")
                .GetInt32());

        using JsonDocument sanitized = JsonDocument.Parse(
            await ReadEntryAsync(
                archive,
                "configuration/start/settings-sanitized.json"));
        Assert.Equal(
            15,
            sanitized.RootElement
                .GetProperty(
                    "deep_debug_frame_retention_minutes")
                .GetInt32());
    }

    [Fact]
    public async Task CompletionPrunesStaleImageAfterLongCaptureGap()
    {
        using TestDirectory directory = new();
        AppPaths paths = new(directory.Path);
        paths.EnsureCreated();
        AppSettings settings = new()
        {
            DeepDebugEnabled = true,
            DeepDebugFrameRetentionMinutes = 10,
        };
        DateTimeOffset now = new(
            2026,
            7,
            31,
            12,
            0,
            0,
            TimeSpan.Zero);
        DeepDebugSessionService service = new(
            paths,
            () => settings,
            () => null,
            _ => { },
            _ => { },
            () => now);

        await service.RunOperationAsync(
            "Capture gap",
            null,
            _ =>
            {
                service.RecordFrame(
                    Frame(),
                    "old_frame");
                service.RecordEvent(
                    "workflow",
                    "text_after_frame");
                now = now.AddMinutes(11);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        string archivePath = Assert.Single(
            Directory.EnumerateFiles(
                paths.Diagnostics,
                "deep-debug-*.zip"));
        using ZipArchive archive =
            ZipFile.OpenRead(archivePath);
        Assert.Null(
            archive.GetEntry(
                "frames/frame-000000001.png"));
        Assert.Contains(
            "text_after_frame",
            await ReadEntryAsync(
                archive,
                "events.jsonl"),
            StringComparison.Ordinal);

        using JsonDocument manifest = JsonDocument.Parse(
            await ReadEntryAsync(
                archive,
                "manifest.json"));
        Assert.Equal(
            0,
            manifest.RootElement
                .GetProperty(
                    "retained_frame_images")
                .GetInt32());
        Assert.Equal(
            1,
            manifest.RootElement
                .GetProperty(
                    "discarded_frame_images")
                .GetInt32());
    }

    [Fact]
    public void FrameRetentionDefaultsToFifteenMinutes()
    {
        Assert.Equal(
            15,
            new AppSettings()
                .DeepDebugFrameRetentionMinutes);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(15, 15)]
    [InlineData(999, 120)]
    public void FrameRetentionNormalizesToSupportedBounds(
        int configured,
        int expected)
    {
        Assert.Equal(
            expected,
            AppSettings.NormalizeDeepDebugFrameRetentionMinutes(
                configured));
    }

    private static ImageFrame Frame() =>
        new(
            1,
            1,
            PixelFormat.Rgb24,
            new byte[3],
            takeOwnership: true);

    private static async Task<string> ReadEntryAsync(
        ZipArchive archive,
        string path)
    {
        ZipArchiveEntry entry =
            archive.GetEntry(path) ??
            throw new InvalidDataException(
                $"Missing archive entry '{path}'.");
        await using Stream stream = entry.Open();
        using StreamReader reader = new(
            stream,
            Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory() =>
            Path = TestPaths.NewTemporaryDirectory();

        public string Path { get; }

        public void Dispose() =>
            TestPaths.DeleteTemporaryDirectory(Path);
    }
}
