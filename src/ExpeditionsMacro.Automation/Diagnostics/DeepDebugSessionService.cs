using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Automation.Diagnostics;

public sealed class DeepDebugSessionService
{
    private static readonly JsonSerializerOptions CompactJson = CreateCompactJson();
    private readonly AppPaths _paths;
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<string> _info;
    private readonly Action<string> _warning;
    private readonly DeepDebugArchiveTextWriter _archiveText;
    private readonly object _gate = new();
    private DeepDebugSession? _active;

    public DeepDebugSessionService(
        AppPaths paths,
        Func<AppSettings> getSettings,
        Func<string?> getLogFilePath,
        Action<string> info,
        Action<string> warning)
    {
        _paths = paths;
        _getSettings = getSettings;
        _info = info;
        _warning = warning;
        _archiveText = new DeepDebugArchiveTextWriter(
            getSettings,
            getLogFilePath);
    }

    public bool IsActive
    {
        get
        {
            lock (_gate) return _active is not null;
        }
    }

    public string? LastArchivePath { get; private set; }

    public event Action<string>? ArchiveSaved;

    public async Task RunOperationAsync(
        string operation,
        DeepDebugOperationContext? context,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(action);
        if (!_getSettings().DeepDebugEnabled)
        {
            await action(cancellationToken).ConfigureAwait(false);
            return;
        }

        DeepDebugSession session = await StartAsync(operation, context ?? new DeepDebugOperationContext()).ConfigureAwait(false);
        string outcome = "success";
        Exception? terminalError = null;
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException error) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "canceled";
            terminalError = error;
            throw;
        }
        catch (Exception error)
        {
            outcome = "error";
            terminalError = error;
            throw;
        }
        finally
        {
            try
            {
                string archive = await CompleteAsync(session, outcome, terminalError).ConfigureAwait(false);
                LastArchivePath = archive;
                _info($"Deep debug archive saved to {Path.GetFileName(archive)} ({outcome}).");
                try
                {
                    ArchiveSaved?.Invoke(archive);
                }
                catch (Exception observerError)
                {
                    _warning($"A deep debug completion observer failed: {observerError.Message}");
                }
            }
            catch (Exception finalizationError)
            {
                string preserved = session.StagingDirectory;
                _archiveText.TryWriteFinalizationError(
                    preserved,
                    finalizationError);
                _warning(RedactText(
                    $"Deep debug ZIP creation failed. The uncompressed session was preserved at '{preserved}': {finalizationError.Message}"));
            }
        }
    }

    public void RecordFrame(ImageFrame frame, string source, object? data = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        DeepDebugSession? session = ActiveSession();
        if (session is null) return;
        long sequence = Interlocked.Increment(ref session.Sequence);
        int frameIndex = Interlocked.Increment(ref session.FrameCount);
        string relativePath = $"frames/frame-{frameIndex:D9}.png";
        Enqueue(session, new DeepDebugWriteItem(
            sequence,
            DateTimeOffset.UtcNow,
            "frame",
            source,
            data,
            relativePath,
            frame.Clone()));
    }

    public void RecordEvent(string category, string action, object? data = null)
    {
        DeepDebugSession? session = ActiveSession();
        if (session is null) return;
        long sequence = Interlocked.Increment(ref session.Sequence);
        Interlocked.Increment(ref session.EventCount);
        Enqueue(session, new DeepDebugWriteItem(
            sequence,
            DateTimeOffset.UtcNow,
            category,
            action,
            data,
            null,
            null));
    }

    public void RecordProgress(MacroProgress progress) => RecordEvent(
        "workflow",
        "progress",
        new
        {
            progress.Phase,
            progress.Percent,
            Message = RedactText(progress.Message),
            progress.DetectedState,
            progress.Confidence,
        });

    public void RecordMacroEvent(MacroEvent entry) => RecordEvent(
        "workflow",
        "macro_event",
        new
        {
            entry.Timestamp,
            entry.Level,
            Message = RedactText(entry.Message),
            entry.State,
            entry.Confidence,
        });

    public void RecordWindowsTrace(WindowsAutomationTrace trace)
    {
        DeepDebugSession? session = ActiveSession();
        if (session is null) return;
        Interlocked.Increment(ref session.InputEventCount);
        RecordEvent("input", $"{trace.Device}.{trace.Action}", trace);
    }

    public void RecordPlacementInput(PlacementInputTrace trace)
    {
        DeepDebugSession? session = ActiveSession();
        if (session is null) return;
        Interlocked.Increment(ref session.InputEventCount);
        RecordEvent("input", $"placement_recorder.{trace.Action}", trace);
    }

    public void RecordVisionTrace(VisionDetectionTrace trace) => RecordEvent(
        "detector",
        trace.Detector,
        new
        {
            trace.State,
            trace.Confidence,
            trace.Data,
            trace.TimestampUtc,
        });

    private async Task<DeepDebugSession> StartAsync(string operation, DeepDebugOperationContext context)
    {
        string diagnosticsRoot = Path.GetFullPath(_paths.Diagnostics);
        Directory.CreateDirectory(diagnosticsRoot);
        string id = Guid.NewGuid().ToString("N");
        string staging = Path.Combine(diagnosticsRoot, $".deep-debug-{id}");
        EnsureChildPath(diagnosticsRoot, staging);
        Directory.CreateDirectory(Path.Combine(staging, "frames"));

        Channel<DeepDebugWriteItem> channel = Channel.CreateBounded<DeepDebugWriteItem>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        DeepDebugSession session = new(
            operation,
            context,
            DateTimeOffset.UtcNow,
            staging,
            channel);
        session.WriterTask = Task.Run(() => WriteItemsAsync(session));

        lock (_gate)
        {
            if (_active is not null) throw new InvalidOperationException("A deep debug session is already active.");
            _active = session;
        }

        RecordEvent("session", "started", new { Operation = operation, StagingDirectory = staging });
        try
        {
            await SnapshotConfigurationAsync(session, "start", includeModels: true).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            RecordEvent("configuration", "snapshot_failed", new { Error = RedactText(error.ToString()) });
        }
        return session;
    }

    private async Task<string> CompleteAsync(DeepDebugSession session, string outcome, Exception? error)
    {
        RecordEvent("session", "finished", new
        {
            Outcome = outcome,
            Error = error is null ? null : RedactText(error.ToString()),
        });

        lock (_gate)
        {
            if (ReferenceEquals(_active, session)) _active = null;
        }
        session.Channel.Writer.TryComplete();
        try
        {
            await session.WriterTask.ConfigureAwait(false);
        }
        catch (Exception writerError)
        {
            session.WriterFailure ??= writerError;
        }

        try
        {
            await SnapshotConfigurationAsync(
                session,
                "end",
                includeModels: session.Context.RefreshReferencedModelsAfterOperation).ConfigureAwait(false);
        }
        catch (Exception snapshotError)
        {
            await File.WriteAllTextAsync(
                Path.Combine(session.StagingDirectory, "configuration-end-error.txt"),
                RedactText(snapshotError.ToString()),
                CancellationToken.None).ConfigureAwait(false);
        }
        await _archiveText
            .CopySanitizedRunLogAsync(session.StagingDirectory)
            .ConfigureAwait(false);

        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        DeepDebugManifest manifest = new(
            session.Operation,
            outcome,
            ProductVersion.Current,
            session.StartedAtUtc,
            completedAt,
            completedAt - session.StartedAtUtc,
            Volatile.Read(ref session.FrameCount),
            Volatile.Read(ref session.EventCount),
            Volatile.Read(ref session.InputEventCount),
            session.WriterFailure is null ? null : RedactText(session.WriterFailure.ToString()),
            error is null ? null : RedactText(error.ToString()),
            "Discord webhook values, protected webhook material, Discord user IDs, and the active Windows username/profile path are excluded.");
        await WriteJsonAsync(Path.Combine(session.StagingDirectory, "manifest.json"), manifest).ConfigureAwait(false);

        string safeOperation = SafeName(session.Operation);
        string archiveCandidate = $"deep-debug-{safeOperation}-{session.StartedAtUtc.ToLocalTime():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        string archiveName = archiveCandidate[..Math.Min(110, archiveCandidate.Length)];
        string archive = Path.Combine(_paths.Diagnostics, $"{archiveName}.zip");
        string temporary = Path.Combine(_paths.Diagnostics, $".{archiveName}.tmp");
        EnsureChildPath(Path.GetFullPath(_paths.Diagnostics), Path.GetFullPath(archive));
        EnsureChildPath(Path.GetFullPath(_paths.Diagnostics), Path.GetFullPath(temporary));
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            ZipFile.CreateFromDirectory(session.StagingDirectory, temporary, CompressionLevel.NoCompression, includeBaseDirectory: false);
            File.Move(temporary, archive, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        try
        {
            Directory.Delete(session.StagingDirectory, recursive: true);
        }
        catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
        {
            _warning($"Deep debug archive was saved, but its staging folder could not be removed: {cleanupError.Message}");
        }
        return archive;
    }

    private async Task WriteItemsAsync(DeepDebugSession session)
    {
        string eventsPath = Path.Combine(session.StagingDirectory, "events.jsonl");
        try
        {
            await using FileStream stream = new(eventsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await foreach (DeepDebugWriteItem item in session.Channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (item.Frame is not null && item.FramePath is not null)
                {
                    string framePath = Path.Combine(session.StagingDirectory, item.FramePath.Replace('/', Path.DirectorySeparatorChar));
                    ImageCodec.SavePng(framePath, item.Frame, compression: 1);
                }

                DeepDebugEventRecord record = new(
                    item.Sequence,
                    item.TimestampUtc,
                    item.Category,
                    item.Action,
                    item.FramePath,
                    item.Data);
                string json;
                try
                {
                    json = RedactText(
                        JsonSerializer.Serialize(record, CompactJson));
                }
                catch (Exception serializationError)
                {
                    json = RedactText(
                        JsonSerializer.Serialize(
                            record with
                            {
                                Data = new
                                {
                                    SerializationError =
                                        serializationError.Message,
                                },
                            },
                            CompactJson));
                }
                await writer.WriteLineAsync(json).ConfigureAwait(false);
            }
            await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            session.WriterFailure = error;
            session.Channel.Writer.TryComplete(error);
            throw;
        }
    }

    private void Enqueue(DeepDebugSession session, DeepDebugWriteItem item)
    {
        if (session.WriterFailure is not null) return;
        try
        {
            session.Channel.Writer.WriteAsync(item).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is ChannelClosedException or AggregateException or InvalidOperationException)
        {
            session.WriterFailure ??= error;
        }
    }

    private async Task SnapshotConfigurationAsync(DeepDebugSession session, string phase, bool includeModels)
    {
        string root = Path.Combine(session.StagingDirectory, "configuration", phase);
        Directory.CreateDirectory(root);
        AppSettings settings = _getSettings();
        await WriteJsonAsync(
            Path.Combine(root, "settings-sanitized.json"),
            DeepDebugSanitizedSettings.From(settings))
            .ConfigureAwait(false);
        await WriteJsonAsync(Path.Combine(root, "operation-context.json"), session.Context).ConfigureAwait(false);
        await WriteJsonAsync(Path.Combine(root, "environment.json"), new
        {
            AppVersion = ProductVersion.Current,
            OperatingSystem = Environment.OSVersion.VersionString,
            Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            CapturedAtUtc = DateTimeOffset.UtcNow,
        }).ConfigureAwait(false);

        ResolvedArtifacts artifacts = await ResolveArtifactsAsync(session.Context, root).ConfigureAwait(false);
        foreach (string id in session.Context.CameraModelIds) artifacts.CameraModelIds.Add(ValidateId(id));
        foreach (string id in session.Context.PlacementModelIds) artifacts.PlacementModelIds.Add(ValidateId(id));
        if (phase == "start")
        {
            session.ReferencedCameraModelIds.UnionWith(artifacts.CameraModelIds);
            session.ReferencedPlacementModelIds.UnionWith(artifacts.PlacementModelIds);
            session.ReferencedDetectorPackIds.UnionWith(artifacts.DetectorPackIds);
        }
        else
        {
            artifacts.CameraModelIds.UnionWith(session.ReferencedCameraModelIds);
            artifacts.PlacementModelIds.UnionWith(session.ReferencedPlacementModelIds);
            artifacts.DetectorPackIds.UnionWith(session.ReferencedDetectorPackIds);
        }

        if (!includeModels) return;
        string modelsRoot = Path.Combine(session.StagingDirectory, "models", phase);
        foreach (string id in artifacts.CameraModelIds)
        {
            _archiveText.CopyDirectory(
                Path.Combine(_paths.CameraModels, id),
                Path.Combine(modelsRoot, "camera", id));
        }
        foreach (string id in artifacts.PlacementModelIds)
        {
            _archiveText.CopyDirectory(
                Path.Combine(_paths.PlacementModels, id),
                Path.Combine(modelsRoot, "placement", id));
        }
        foreach (string id in artifacts.DetectorPackIds)
        {
            _archiveText.CopyDirectory(
                Path.Combine(_paths.DetectorPacks, id, "current"),
                Path.Combine(modelsRoot, "detector-packs", id));
        }
    }

    private async Task<ResolvedArtifacts> ResolveArtifactsAsync(DeepDebugOperationContext context, string snapshotRoot)
    {
        ResolvedArtifacts resolved = new();
        if (!string.IsNullOrWhiteSpace(context.MacroPlanId))
        {
            MacroPlan? plan = await ReadAndCopyAsync<MacroPlan>(
                Path.Combine(_paths.MacroPlans, $"{ValidateId(context.MacroPlanId)}.json"),
                Path.Combine(snapshotRoot, "macro-plan.json")).ConfigureAwait(false);
            if (plan is not null)
            {
                foreach (MacroTaskDefinition task in plan.Tasks)
                {
                    switch (task.Kind)
                    {
                        case MacroTaskKind.Expedition:
                            await ResolveExpeditionAsync(task.PresetId, snapshotRoot, resolved).ConfigureAwait(false);
                            break;
                        case MacroTaskKind.Challenge:
                            await ResolveChallengeAsync(task.PresetId, snapshotRoot, resolved).ConfigureAwait(false);
                            break;
                        case MacroTaskKind.Story:
                            await ResolveStoryAsync(task.PresetId, snapshotRoot, resolved).ConfigureAwait(false);
                            break;
                        case MacroTaskKind.Raid:
                            await ResolveRaidAsync(task.PresetId, snapshotRoot, resolved).ConfigureAwait(false);
                            break;
                    }
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(context.ExpeditionPresetId)) await ResolveExpeditionAsync(context.ExpeditionPresetId, snapshotRoot, resolved).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(context.ChallengePresetId)) await ResolveChallengeAsync(context.ChallengePresetId, snapshotRoot, resolved).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(context.StoryPresetId)) await ResolveStoryAsync(context.StoryPresetId, snapshotRoot, resolved).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(context.RaidPresetId)) await ResolveRaidAsync(context.RaidPresetId, snapshotRoot, resolved).ConfigureAwait(false);
        return resolved;
    }

    private async Task ResolveExpeditionAsync(string id, string root, ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.ExpeditionPresetIds.Add(safeId)) return;
        ExpeditionPreset? preset = await ReadAndCopyAsync<ExpeditionPreset>(
            Path.Combine(_paths.Presets, $"{safeId}.json"),
            Path.Combine(root, "presets", "expeditions", $"{safeId}.json")).ConfigureAwait(false);
        if (preset is null) return;
        AddId(resolved.CameraModelIds, preset.CameraModelId);
        AddId(resolved.PlacementModelIds, preset.PlacementModelId);
        AddId(resolved.DetectorPackIds, preset.DetectorPackId);
    }

    private async Task ResolveChallengeAsync(string id, string root, ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.ChallengePresetIds.Add(safeId)) return;
        ChallengePreset? preset = await ReadAndCopyAsync<ChallengePreset>(
            Path.Combine(_paths.ChallengePresets, $"{safeId}.json"),
            Path.Combine(root, "presets", "challenges", $"{safeId}.json")).ConfigureAwait(false);
        if (preset is null) return;
        AddId(resolved.DetectorPackIds, preset.DetectorPackId);
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            AddId(resolved.CameraModelIds, profile.CameraModelId);
            AddId(resolved.PlacementModelIds, profile.PrestartPlacementModelId);
            AddId(resolved.PlacementModelIds, profile.DelayedPlacementModelId);
        }
    }

    private async Task ResolveStoryAsync(string id, string root, ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.StoryPresetIds.Add(safeId)) return;
        StoryPreset? preset = await ReadAndCopyAsync<StoryPreset>(
            Path.Combine(_paths.StoryPresets, $"{safeId}.json"),
            Path.Combine(root, "presets", "story", $"{safeId}.json")).ConfigureAwait(false);
        if (preset is null) return;
        AddId(resolved.CameraModelIds, preset.CameraModelId);
        AddId(resolved.PlacementModelIds, preset.PrestartPlacementModelId);
        AddId(resolved.PlacementModelIds, preset.DelayedPlacementModelId);
        resolved.DetectorPackIds.Add("anime-expeditions-expeditions");
    }

    private async Task ResolveRaidAsync(string id, string root, ResolvedArtifacts resolved)
    {
        string safeId = ValidateId(id);
        if (!resolved.RaidPresetIds.Add(safeId)) return;
        RaidPreset? preset = await ReadAndCopyAsync<RaidPreset>(
            Path.Combine(_paths.RaidPresets, $"{safeId}.json"),
            Path.Combine(root, "presets", "raid", $"{safeId}.json")).ConfigureAwait(false);
        if (preset is null) return;
        AddId(resolved.CameraModelIds, preset.CameraModelId);
        AddId(resolved.PlacementModelIds, preset.PrestartPlacementModelId);
        AddId(resolved.PlacementModelIds, preset.DelayedPlacementModelId);
        resolved.DetectorPackIds.Add("anime-expeditions-expeditions");
    }

    private async Task<T?> ReadAndCopyAsync<T>(string source, string destination)
    {
        if (!File.Exists(source)) return default;
        T? value = await JsonFileStore.ReadAsync<T>(source, CancellationToken.None).ConfigureAwait(false);
        if (value is not null) await WriteJsonAsync(destination, value).ConfigureAwait(false);
        return value;
    }

    private string RedactText(string text) =>
        _archiveText.Redact(text);

    private DeepDebugSession? ActiveSession()
    {
        lock (_gate) return _active;
    }

    private static void AddId(HashSet<string> target, string id)
    {
        if (!string.IsNullOrWhiteSpace(id)) target.Add(ValidateId(id));
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new InvalidDataException("A referenced model, preset, or detector id is invalid.");
        return id;
    }

    private static string SafeName(string value)
    {
        HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
        string safe = new(value.Trim().ToLowerInvariant().Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character).ToArray());
        while (safe.Contains("--", StringComparison.Ordinal)) safe = safe.Replace("--", "-", StringComparison.Ordinal);
        safe = safe.Trim('-', '.');
        return safe.Length == 0 ? "operation" : safe[..Math.Min(safe.Length, 48)];
    }

    private Task WriteJsonAsync<T>(string path, T value)
        => _archiveText.WriteJsonAsync(path, value);

    private static void EnsureChildPath(string root, string path)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Deep debug output resolved outside the diagnostics folder.");
        }
    }

    private static JsonSerializerOptions CreateCompactJson()
    {
        JsonSerializerOptions options = new(JsonFileStore.Options)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        return options;
    }

    private sealed class DeepDebugSession
    {
        public DeepDebugSession(
            string operation,
            DeepDebugOperationContext context,
            DateTimeOffset startedAtUtc,
            string stagingDirectory,
            Channel<DeepDebugWriteItem> channel)
        {
            Operation = operation;
            Context = context;
            StartedAtUtc = startedAtUtc;
            StagingDirectory = stagingDirectory;
            Channel = channel;
        }

        public string Operation { get; }
        public DeepDebugOperationContext Context { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public string StagingDirectory { get; }
        public Channel<DeepDebugWriteItem> Channel { get; }
        public Task WriterTask { get; set; } = Task.CompletedTask;
        public Exception? WriterFailure { get; set; }
        public long Sequence;
        public int FrameCount;
        public int EventCount;
        public int InputEventCount;
        public HashSet<string> ReferencedCameraModelIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ReferencedPlacementModelIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ReferencedDetectorPackIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record DeepDebugWriteItem(
        long Sequence,
        DateTimeOffset TimestampUtc,
        string Category,
        string Action,
        object? Data,
        string? FramePath,
        ImageFrame? Frame);

    private sealed record DeepDebugEventRecord(
        long Sequence,
        DateTimeOffset TimestampUtc,
        string Category,
        string Action,
        string? Frame,
        object? Data);

    private sealed record DeepDebugManifest(
        string Operation,
        string Outcome,
        string AppVersion,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        TimeSpan Runtime,
        int Frames,
        int Events,
        int InputEvents,
        string? WriterFailure,
        string? OperationError,
        string SecretPolicy);
    private sealed class ResolvedArtifacts
    {
        public HashSet<string> ExpeditionPresetIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ChallengePresetIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> StoryPresetIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RaidPresetIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CameraModelIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PlacementModelIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DetectorPackIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
