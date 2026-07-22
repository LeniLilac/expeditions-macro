using System.IO.Compression;
using System.Text;
using ExpeditionsMacro.DeepDebugViewer.Services;

namespace ExpeditionsMacro.DeepDebugViewer.Tests;

public sealed class DeepDebugArchiveTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEAQH/6X1O4QAAAABJRU5ErkJggg==");

    [Fact]
    public async Task ArchiveOrdersFramesBySequenceAndAssociatesNearbyEvents()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "deep-debug-ordering.zip");
        CreateArchive(
            path,
            [
                Event(30, "2026-07-22T12:00:03Z", "frame", "capture_client", "frames/frame-000000002.png", new { call_site = "second" }),
                Event(10, "2026-07-22T12:00:01Z", "workflow", "progress", null, new { state = "ready" }),
                Event(20, "2026-07-22T12:00:02Z", "frame", "capture_client", "frames/frame-000000001.png", new { call_site = "first" }),
                Event(25, "2026-07-22T12:00:02.500Z", "input", "mouse.left_down", null, new { x = 410, y = 320 }),
            ],
            "frames/frame-000000001.png",
            "frames/frame-000000002.png");

        using DeepDebugArchive archive = await DeepDebugArchive.OpenAsync(path);

        Assert.Equal([20L, 30L], archive.Frames.Select(frame => frame.Sequence));
        Assert.Equal([0, 1], archive.Frames.Select(frame => frame.Index));
        IReadOnlyList<DeepDebugTimelineEvent> nearby = archive.GetNearbyEvents(1, before: 2, after: 0);
        Assert.Equal([20L, 25L, 30L], nearby.Select(item => item.Sequence));
        Assert.Contains("x=410", nearby[1].Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArchiveSkipsMalformedEventsAndKeepsMissingFramesInspectable()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "deep-debug-missing.zip");
        using (FileStream file = File.Create(path))
        using (ZipArchive archive = new(file, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "manifest.json", "{\"operation\":\"Camera setup\",\"outcome\":\"error\",\"frames\":1}");
            WriteEntry(
                archive,
                "events.jsonl",
                string.Join('\n',
                    "not-json",
                    Event(2, "2026-07-22T12:00:02Z", "frame", "capture_client", "frames/frame-000000009.png", new { call_site = "test" })));
        }

        using DeepDebugArchive opened = await DeepDebugArchive.OpenAsync(path);

        Assert.Equal(1, opened.MalformedEventLines);
        DeepDebugFrameRecord frame = Assert.Single(opened.Frames);
        Assert.False(frame.EntryExists);
        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => opened.ReadFrameBytesAsync(frame));
        Assert.Contains("missing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArchiveFallsBackToSortedFrameNamesWhenEventLogIsMissing()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "deep-debug-fallback.zip");
        CreateArchive(path, [], "frames/frame-000000010.png", "frames/frame-000000002.png");

        using DeepDebugArchive archive = await DeepDebugArchive.OpenAsync(path);

        Assert.Equal(
            ["frames/frame-000000002.png", "frames/frame-000000010.png"],
            archive.Frames.Select(frame => frame.Path));
    }

    [Fact]
    public async Task ViewerDefensivelyRedactsSecretsPresentInAnEvent()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "deep-debug-redaction.zip");
        const string webhook = "https://canary.discord.com/api/webhooks/123456789012345678/secret-token";
        CreateArchive(
            path,
            [Event(1, "2026-07-22T12:00:01Z", "workflow", "macro_event", null, new { message = webhook })]);

        using DeepDebugArchive archive = await DeepDebugArchive.OpenAsync(path);

        DeepDebugTimelineEvent item = Assert.Single(archive.Events);
        Assert.DoesNotContain(webhook, item.Details, StringComparison.Ordinal);
        Assert.Contains("[REDACTED DISCORD WEBHOOK]", item.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecodedFrameCacheEvictsByPixelBytesAndCanBeCleared()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "deep-debug-cache.zip");
        using (FileStream file = File.Create(path))
        using (ZipArchive createdArchive = new(file, ZipArchiveMode.Create))
        {
            WriteEntry(createdArchive, "manifest.json", "{\"operation\":\"Cache test\"}");
            WriteEntry(
                createdArchive,
                "events.jsonl",
                string.Join('\n', Enumerable.Range(1, 3).Select(index => Event(
                    index,
                    $"2026-07-22T12:00:0{index}Z",
                    "frame",
                    "capture_client",
                    $"frames/frame-{index:D9}.png",
                    new { }))));
            foreach (int index in Enumerable.Range(1, 3))
            {
                WriteBytes(createdArchive, $"frames/frame-{index:D9}.png", OnePixelPng);
            }
        }

        using DeepDebugArchive archive = await DeepDebugArchive.OpenAsync(path);
        FrameBitmapCache cache = new(archive, budgetBytes: 8);
        await cache.GetAsync(0);
        await cache.GetAsync(1);
        await cache.GetAsync(2);

        Assert.Equal(2, cache.Count);
        Assert.Equal(8, cache.CurrentBytes);
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.CurrentBytes);
    }

    private static void CreateArchive(string path, IReadOnlyList<string> events, params string[] frames)
    {
        using FileStream file = File.Create(path);
        using ZipArchive archive = new(file, ZipArchiveMode.Create);
        WriteEntry(
            archive,
            "manifest.json",
            "{\"operation\":\"Macro plan\",\"outcome\":\"success\",\"app_version\":\"test\",\"runtime\":\"00:00:03\"}");
        if (events.Count > 0) WriteEntry(archive, "events.jsonl", string.Join('\n', events));
        foreach (string frame in frames) WriteBytes(archive, frame, [1, 2, 3, 4]);
    }

    private static string Event(long sequence, string timestamp, string category, string action, string? frame, object data) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            sequence,
            timestamp_utc = timestamp,
            category,
            action,
            frame,
            data,
        });

    private static void WriteEntry(ZipArchive archive, string name, string value) =>
        WriteBytes(archive, name, Encoding.UTF8.GetBytes(value));

    private static void WriteBytes(ZipArchive archive, string name, byte[] value)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using Stream stream = entry.Open();
        stream.Write(value);
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExpeditionsMacro.DeepDebugViewer.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
