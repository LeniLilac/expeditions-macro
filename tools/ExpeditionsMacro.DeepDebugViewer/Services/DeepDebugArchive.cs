using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExpeditionsMacro.DeepDebugViewer.Services;

public sealed record DeepDebugManifestSummary(
    string Operation,
    string Outcome,
    string AppVersion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    TimeSpan? Runtime,
    int DeclaredFrames,
    int DeclaredEvents,
    int DeclaredInputEvents);

public sealed record DeepDebugTimelineEvent(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Category,
    string Action,
    string? FramePath,
    string Details);

public sealed record DeepDebugFrameRecord(
    int Index,
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Path,
    int EventIndex,
    bool EntryExists);

public sealed class DeepDebugArchive : IDisposable
{
    private const long MaximumFrameBytes = 64L * 1024 * 1024;
    private const int MaximumEventDetailsCharacters = 2400;

    private static readonly Regex DiscordWebhookPattern = new(
        "https://(?:[a-z0-9-]+\\.)?(?:discord(?:app)?\\.com)/api(?:/v\\d+)?/webhooks/[^\\s\\\"'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex DiscordUserIdPattern = new(
        "(?<![0-9])[0-9]{17,20}(?![0-9])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private readonly FileStream _stream;
    private readonly ZipArchive _archive;
    private readonly IReadOnlyDictionary<string, ZipArchiveEntry> _entries;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private bool _disposed;

    private DeepDebugArchive(
        string path,
        FileStream stream,
        ZipArchive archive,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        DeepDebugManifestSummary manifest,
        IReadOnlyList<DeepDebugTimelineEvent> events,
        IReadOnlyList<DeepDebugFrameRecord> frames,
        int malformedEventLines)
    {
        Path = path;
        _stream = stream;
        _archive = archive;
        _entries = entries;
        Manifest = manifest;
        Events = events;
        Frames = frames;
        MalformedEventLines = malformedEventLines;
    }

    public string Path { get; }

    public DeepDebugManifestSummary Manifest { get; }

    public IReadOnlyList<DeepDebugTimelineEvent> Events { get; }

    public IReadOnlyList<DeepDebugFrameRecord> Frames { get; }

    public int MalformedEventLines { get; }

    public static async Task<DeepDebugArchive> OpenAsync(
        string path,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("The Deep Debug ZIP could not be found.", fullPath);
        if (!fullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Choose a Deep Debug ZIP archive.");
        }

        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileStream? stream = null;
            ZipArchive? archive = null;
            try
            {
                progress?.Report("Opening ZIP index...");
                stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.RandomAccess);
                archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entries.TryAdd(NormalizePath(entry.FullName), entry);
                }

                DeepDebugManifestSummary manifest = await ReadManifestAsync(entries, cancellationToken).ConfigureAwait(false);
                progress?.Report("Indexing events and frames...");
                ParsedTimeline timeline = await ReadTimelineAsync(entries, progress, cancellationToken).ConfigureAwait(false);
                if (timeline.Frames.Count == 0)
                {
                    timeline = timeline with { Frames = BuildFallbackFrames(entries) };
                }

                DeepDebugArchive result = new(
                    fullPath,
                    stream,
                    archive,
                    entries,
                    manifest,
                    timeline.Events,
                    timeline.Frames,
                    timeline.MalformedLines);
                stream = null;
                archive = null;
                return result;
            }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException($"The selected file is not a readable Deep Debug archive. {error.Message}", error);
            }
            finally
            {
                archive?.Dispose();
                stream?.Dispose();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<DeepDebugTimelineEvent> GetNearbyEvents(
        int frameIndex,
        int before = 16,
        int after = 24)
    {
        if (frameIndex < 0 || frameIndex >= Frames.Count) throw new ArgumentOutOfRangeException(nameof(frameIndex));
        if (before < 0) throw new ArgumentOutOfRangeException(nameof(before));
        if (after < 0) throw new ArgumentOutOfRangeException(nameof(after));
        if (Events.Count == 0) return [];

        int center = Math.Clamp(Frames[frameIndex].EventIndex, 0, Events.Count - 1);
        int start = Math.Max(0, center - before);
        int count = Math.Min(Events.Count - start, before + after + 1);
        return Events.Skip(start).Take(count).ToArray();
    }

    public async Task<byte[]> ReadFrameBytesAsync(
        DeepDebugFrameRecord frame,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_entries.TryGetValue(NormalizePath(frame.Path), out ZipArchiveEntry? entry))
        {
            throw new InvalidDataException($"Frame '{frame.Path}' is missing from the archive.");
        }
        if (entry.Length is <= 0 or > MaximumFrameBytes)
        {
            throw new InvalidDataException($"Frame '{frame.Path}' has an invalid size ({entry.Length:N0} bytes).");
        }

        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await using Stream input = entry.Open();
            using MemoryStream output = new((int)entry.Length);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return output.ToArray();
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Frame '{frame.Path}' is corrupt or incomplete. {error.Message}", error);
        }
        finally
        {
            _readGate.Release();
        }
    }

    public void Dispose()
    {
        _readGate.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _archive.Dispose();
            _stream.Dispose();
        }
        finally
        {
            _readGate.Release();
        }
    }

    private static async Task<DeepDebugManifestSummary> ReadManifestAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        CancellationToken cancellationToken)
    {
        if (!entries.TryGetValue("manifest.json", out ZipArchiveEntry? entry))
        {
            return new DeepDebugManifestSummary("Deep Debug operation", "unknown", "unknown", null, null, null, 0, 0, 0);
        }

        await using Stream stream = entry.Open();
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        return new DeepDebugManifestSummary(
            GetString(root, "operation") ?? "Deep Debug operation",
            GetString(root, "outcome") ?? "unknown",
            GetString(root, "app_version") ?? "unknown",
            GetDateTimeOffset(root, "started_at_utc"),
            GetDateTimeOffset(root, "completed_at_utc"),
            GetTimeSpan(root, "runtime"),
            GetInt32(root, "frames"),
            GetInt32(root, "events"),
            GetInt32(root, "input_events"));
    }

    private static async Task<ParsedTimeline> ReadTimelineAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!entries.TryGetValue("events.jsonl", out ZipArchiveEntry? entry))
        {
            return new ParsedTimeline([], [], 0);
        }

        List<(DeepDebugTimelineEvent Event, int SourceOrder)> parsed = [];
        int malformed = 0;
        int lineNumber = 0;
        await using Stream stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 128 * 1024, leaveOpen: false);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                long sequence = GetInt64(root, "sequence", lineNumber);
                DateTimeOffset timestamp = GetDateTimeOffset(root, "timestamp_utc") ?? DateTimeOffset.UnixEpoch.AddMilliseconds(sequence);
                string category = GetString(root, "category") ?? "unknown";
                string action = GetString(root, "action") ?? "unknown";
                string? framePath = GetString(root, "frame");
                string details = root.TryGetProperty("data", out JsonElement data)
                    ? SummarizeData(data)
                    : string.Empty;
                parsed.Add((new DeepDebugTimelineEvent(
                    sequence,
                    timestamp,
                    category,
                    action,
                    framePath is null ? null : NormalizePath(framePath),
                    details), lineNumber));
            }
            catch (JsonException)
            {
                malformed++;
            }

            if (lineNumber % 1000 == 0) progress?.Report($"Indexed {lineNumber:N0} event records...");
        }

        DeepDebugTimelineEvent[] events = parsed
            .OrderBy(value => value.Event.Sequence)
            .ThenBy(value => value.Event.TimestampUtc)
            .ThenBy(value => value.SourceOrder)
            .Select(value => value.Event)
            .ToArray();
        List<DeepDebugFrameRecord> frames = [];
        for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            DeepDebugTimelineEvent item = events[eventIndex];
            if (string.IsNullOrWhiteSpace(item.FramePath)) continue;
            frames.Add(new DeepDebugFrameRecord(
                frames.Count,
                item.Sequence,
                item.TimestampUtc,
                item.FramePath,
                eventIndex,
                entries.ContainsKey(item.FramePath)));
        }
        return new ParsedTimeline(events, frames, malformed);
    }

    private static IReadOnlyList<DeepDebugFrameRecord> BuildFallbackFrames(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries)
    {
        ZipArchiveEntry[] frameEntries = entries.Values
            .Where(entry => NormalizePath(entry.FullName).StartsWith("frames/", StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return frameEntries.Select((entry, index) => new DeepDebugFrameRecord(
            index,
            index + 1,
            entry.LastWriteTime,
            NormalizePath(entry.FullName),
            0,
            true)).ToArray();
    }

    private static string SummarizeData(JsonElement data)
    {
        string result;
        if (data.ValueKind == JsonValueKind.Object)
        {
            string[] priority =
            [
                "message", "phase", "state", "detected_state", "confidence", "action", "key", "unit_key",
                "x", "y", "delta_x", "delta_y", "direction", "hold_milliseconds", "call_site", "error",
            ];
            List<string> parts = [];
            HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
            foreach (string name in priority)
            {
                if (!data.TryGetProperty(name, out JsonElement value)) continue;
                parts.Add($"{name}={CompactValue(value)}");
                used.Add(name);
            }
            foreach (JsonProperty property in data.EnumerateObject())
            {
                if (used.Contains(property.Name)) continue;
                parts.Add($"{property.Name}={CompactValue(property.Value)}");
            }
            result = string.Join("  ", parts);
        }
        else
        {
            result = CompactValue(data);
        }

        result = Redact(result);
        return result.Length <= MaximumEventDetailsCharacters
            ? result
            : string.Concat(result.AsSpan(0, MaximumEventDetailsCharacters), "...");
    }

    private static string CompactValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText(),
    };

    private static string Redact(string value)
    {
        string redacted = DiscordWebhookPattern.Replace(value, "[REDACTED DISCORD WEBHOOK]");
        return DiscordUserIdPattern.Replace(redacted, "[REDACTED DISCORD USER ID]");
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt32(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int result) ? result : 0;

    private static long GetInt64(JsonElement element, string property, long fallback) =>
        element.TryGetProperty(property, out JsonElement value) && value.TryGetInt64(out long result) ? result : fallback;

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset result)
            ? result
            : null;

    private static TimeSpan? GetTimeSpan(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        TimeSpan.TryParse(value.GetString(), CultureInfo.InvariantCulture, out TimeSpan result)
            ? result
            : null;

    private sealed record ParsedTimeline(
        IReadOnlyList<DeepDebugTimelineEvent> Events,
        IReadOnlyList<DeepDebugFrameRecord> Frames,
        int MalformedLines);
}
