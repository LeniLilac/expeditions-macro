using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Diagnostics;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Services;

public sealed record DiagnosticCaptureProgress(int Captures, string Message);

public sealed record DiagnosticCaptureResult(string ArchivePath, int Captures, bool LogsIncluded, int ArchivesPruned = 0);

public sealed class DiagnosticCaptureService
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;
    public const int AutomaticHistoryCaptures = 10;
    public const int AutomaticPostFailureCaptures = 10;
    public const int MaximumAutomaticArchives = 10;

    public static readonly TimeSpan AutomaticPostFailureInterval = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan MinimumHistoryInterval = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IRobloxAutomation _automation;
    private readonly AppPaths _paths;
    private readonly object _historyGate = new();
    private readonly DiagnosticStateHistory _automaticHistory = new(AutomaticHistoryCaptures);
    private bool _historyActive;
    private DateTimeOffset _lastHistoryCaptureAt = DateTimeOffset.MinValue;
    private string? _lastHistoryAction;

    public DiagnosticCaptureService(IRobloxAutomation automation, AppPaths paths)
    {
        _automation = automation;
        _paths = paths;
    }

    public void BeginAutomaticHistory(string initialAction)
    {
        lock (_historyGate)
        {
            _automaticHistory.Clear();
            _historyActive = true;
            _lastHistoryCaptureAt = DateTimeOffset.MinValue;
            _lastHistoryAction = null;
        }

        RecordActionState(initialAction);
    }

    public void EndAutomaticHistory()
    {
        lock (_historyGate)
        {
            _historyActive = false;
            _automaticHistory.Clear();
            _lastHistoryCaptureAt = DateTimeOffset.MinValue;
            _lastHistoryAction = null;
        }
    }

    public void RecordActionState(string action)
    {
        string label = action.Trim();
        if (label.Length == 0) return;
        if (label.Length > 240) label = label[..240];

        lock (_historyGate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!_historyActive ||
                string.Equals(label, _lastHistoryAction, StringComparison.Ordinal) ||
                now - _lastHistoryCaptureAt < MinimumHistoryInterval)
            {
                return;
            }

            try
            {
                RobloxWindow? window = _automation.FindWindow();
                if (window is null) return;
                ClientBounds bounds = _automation.GetClientBounds(window.Value);
                if (bounds.Width != ClientWidth || bounds.Height != ClientHeight) return;

                ImageFrame frame = _automation.CaptureClient(window.Value);
                _automaticHistory.Add(frame, now, label);
                _lastHistoryAction = label;
                _lastHistoryCaptureAt = now;
            }
            catch
            {
                // History capture is best effort and must never interrupt automation.
            }
        }
    }

    public async Task<DiagnosticCaptureResult> CaptureAsync(
        string captureName,
        TimeSpan interval,
        IProgress<DiagnosticCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default,
        int? maximumCaptures = null,
        string? logFilePath = null)
    {
        string safeName = SafeName(captureName);
        if (interval < TimeSpan.FromMilliseconds(100) || interval > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Capture interval must be between 0.1 and 300 seconds.");
        }
        if (maximumCaptures is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCaptures), "Maximum captures must be 1 through 1000.");
        }

        RobloxWindow window = _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
        string diagnosticsRoot = Path.GetFullPath(_paths.Diagnostics);
        Directory.CreateDirectory(diagnosticsRoot);
        string staging = Path.Combine(diagnosticsRoot, $".capture-{Guid.NewGuid():N}");
        string archivePath = Path.GetFullPath(Path.Combine(diagnosticsRoot, $"{safeName}.zip"));
        string temporaryArchive = Path.Combine(diagnosticsRoot, $".{safeName}-{Guid.NewGuid():N}.tmp");
        EnsureChildPath(diagnosticsRoot, staging);
        EnsureChildPath(diagnosticsRoot, archivePath);
        EnsureChildPath(diagnosticsRoot, temporaryArchive);
        Directory.CreateDirectory(staging);

        List<DiagnosticCaptureFrame> frames = [];
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        bool stopped = false;
        bool completed = false;
        try
        {
            if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
            progress?.Report(new DiagnosticCaptureProgress(0, $"Resizing Roblox to {ClientWidth} by {ClientHeight}."));
            await _automation.ResizeClientAsync(window, ClientWidth, ClientHeight, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox for capture.");
                ClientBounds client = _automation.GetClientBounds(window);
                if (client.Width != ClientWidth || client.Height != ClientHeight)
                {
                    throw new InvalidOperationException($"Roblox changed size during capture (actual: {client.Width} by {client.Height}).");
                }

                DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
                ImageFrame frame = _automation.CaptureClient(window);
                string fileName = $"frame-{frames.Count + 1:D6}.png";
                SavePng(Path.Combine(staging, fileName), frame);
                frames.Add(new DiagnosticCaptureFrame(fileName, capturedAt, "manual", null));
                string progressMessage = maximumCaptures is int maximum
                    ? $"Captured {frames.Count} of {maximum} screenshot(s)."
                    : $"Captured {frames.Count} screenshot(s). Press the macro hotkey to stop and save.";
                progress?.Report(new DiagnosticCaptureProgress(frames.Count, progressMessage));
                if (maximumCaptures is int maximumCount && frames.Count >= maximumCount) break;
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopped = true;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
            throw;
        }
        try
        {
            if (frames.Count == 0)
            {
                if (stopped) throw new OperationCanceledException(cancellationToken);
                throw new InvalidOperationException("No screenshots were captured.");
            }

            bool logsIncluded = TryCopyLog(logFilePath, staging);
            string[] includedFiles = logsIncluded ? ["macro-run.log"] : [];
            DiagnosticDetectorPack? detectorPack = await TryReadDetectorPackAsync().ConfigureAwait(false);
            DiagnosticCaptureManifest manifest = new(
                safeName,
                ProductVersion.Current,
                startedAt,
                DateTimeOffset.UtcNow,
                ClientWidth,
                ClientHeight,
                (int)Math.Round(interval.TotalMilliseconds),
                detectorPack,
                includedFiles,
                frames);
            await File.WriteAllTextAsync(
                Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions),
                CancellationToken.None).ConfigureAwait(false);

            if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
            ZipFile.CreateFromDirectory(staging, temporaryArchive, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(temporaryArchive, archivePath, overwrite: true);
            completed = true;
            string message = $"Saved {frames.Count} screenshot(s) to {Path.GetFileName(archivePath)}.";
            if (logsIncluded) message += " Included the current macro run log.";
            progress?.Report(new DiagnosticCaptureProgress(frames.Count, message));
            return new DiagnosticCaptureResult(archivePath, frames.Count, logsIncluded);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (!completed && File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
        }
    }

    public async Task<DiagnosticCaptureResult> CaptureFailureAsync(
        string captureName,
        string failure,
        IProgress<DiagnosticCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? logFilePath = null,
        int postFailureCaptures = AutomaticPostFailureCaptures,
        TimeSpan? postFailureInterval = null)
    {
        string safeName = SafeName(captureName);
        TimeSpan interval = postFailureInterval ?? AutomaticPostFailureInterval;
        if (interval < TimeSpan.FromMilliseconds(100) || interval > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(postFailureInterval), "Capture interval must be between 0.1 and 300 seconds.");
        }
        if (postFailureCaptures is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(postFailureCaptures), "Post-failure captures must be 1 through 1000.");
        }

        IReadOnlyList<DiagnosticStateFrame> history;
        lock (_historyGate) history = _automaticHistory.Snapshot();

        string diagnosticsRoot = Path.GetFullPath(_paths.Diagnostics);
        Directory.CreateDirectory(diagnosticsRoot);
        string staging = Path.Combine(diagnosticsRoot, $".capture-{Guid.NewGuid():N}");
        string archivePath = Path.GetFullPath(Path.Combine(diagnosticsRoot, $"{safeName}.zip"));
        string temporaryArchive = Path.Combine(diagnosticsRoot, $".{safeName}-{Guid.NewGuid():N}.tmp");
        EnsureChildPath(diagnosticsRoot, staging);
        EnsureChildPath(diagnosticsRoot, archivePath);
        EnsureChildPath(diagnosticsRoot, temporaryArchive);
        Directory.CreateDirectory(staging);

        List<DiagnosticCaptureFrame> frames = [];
        DateTimeOffset startedAt = history.Count > 0 ? history[0].CapturedAtUtc : DateTimeOffset.UtcNow;
        bool completed = false;
        string? captureWarning = null;
        try
        {
            for (int index = 0; index < history.Count; index++)
            {
                DiagnosticStateFrame state = history[index];
                string fileName = $"before-{index + 1:D2}.png";
                SavePng(Path.Combine(staging, fileName), state.Image);
                frames.Add(new DiagnosticCaptureFrame(fileName, state.CapturedAtUtc, "before-failure", state.Action));
            }
            progress?.Report(new DiagnosticCaptureProgress(
                frames.Count,
                $"Saved {frames.Count} recent action-state frame(s); capturing {postFailureCaptures} after the failure."));

            try
            {
                RobloxWindow window = _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
                if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox for post-failure capture.");
                await _automation.ResizeClientAsync(window, ClientWidth, ClientHeight, cancellationToken).ConfigureAwait(false);
                await Task.Delay(180, cancellationToken).ConfigureAwait(false);

                for (int index = 0; index < postFailureCaptures; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (index > 0) await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                    if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox for post-failure capture.");
                    ClientBounds client = _automation.GetClientBounds(window);
                    if (client.Width != ClientWidth || client.Height != ClientHeight)
                    {
                        throw new InvalidOperationException($"Roblox changed size during capture (actual: {client.Width} by {client.Height}).");
                    }

                    DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
                    ImageFrame frame = _automation.CaptureClient(window);
                    string fileName = $"after-{index + 1:D2}.png";
                    SavePng(Path.Combine(staging, fileName), frame);
                    frames.Add(new DiagnosticCaptureFrame(fileName, capturedAt, "after-failure", failure));
                    progress?.Report(new DiagnosticCaptureProgress(
                        frames.Count,
                        $"Captured {index + 1} of {postFailureCaptures} post-failure screenshot(s)."));
                }
            }
            catch (Exception error)
            {
                captureWarning = $"Post-failure capture stopped early: {error.Message}";
                if (frames.Count == 0)
                {
                    throw new InvalidOperationException(captureWarning, error);
                }
            }

            bool logsIncluded = TryCopyLog(logFilePath, staging);
            string[] includedFiles = logsIncluded ? ["macro-run.log"] : [];
            DiagnosticDetectorPack? detectorPack = await TryReadDetectorPackAsync().ConfigureAwait(false);
            DiagnosticCaptureManifest manifest = new(
                safeName,
                ProductVersion.Current,
                startedAt,
                DateTimeOffset.UtcNow,
                ClientWidth,
                ClientHeight,
                (int)Math.Round(interval.TotalMilliseconds),
                detectorPack,
                includedFiles,
                frames,
                "automatic-failure",
                failure,
                captureWarning);
            await File.WriteAllTextAsync(
                Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions),
                CancellationToken.None).ConfigureAwait(false);

            if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
            ZipFile.CreateFromDirectory(staging, temporaryArchive, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(temporaryArchive, archivePath, overwrite: true);
            int archivesPruned = DiagnosticArchiveRetention.PruneAutomaticErrorArchives(
                diagnosticsRoot,
                MaximumAutomaticArchives);
            completed = true;
            string message = $"Saved {frames.Count} screenshot(s) to {Path.GetFileName(archivePath)}.";
            if (logsIncluded) message += " Included the current macro run log.";
            if (archivesPruned > 0) message += $" Removed {archivesPruned} older automatic error archive(s).";
            if (captureWarning is not null) message += $" {captureWarning}";
            progress?.Report(new DiagnosticCaptureProgress(frames.Count, message));
            return new DiagnosticCaptureResult(archivePath, frames.Count, logsIncluded, archivesPruned);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (!completed && File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
        }
    }

    internal static string SafeName(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0) throw new FormatException("Capture name is required.");
        HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
        string safe = new(trimmed.Select(character => invalid.Contains(character) || char.IsControl(character) ? '-' : character).ToArray());
        safe = safe.Trim().TrimEnd('.');
        if (safe.Length == 0) throw new FormatException("Capture name must contain a valid file-name character.");
        return safe.Length <= 80 ? safe : safe[..80].TrimEnd();
    }

    private static void SavePng(string path, ImageFrame frame)
    {
        BitmapSource source = BitmapSourceFactory.Create(frame);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(output);
    }

    private static bool TryCopyLog(string? logFilePath, string staging)
    {
        if (string.IsNullOrWhiteSpace(logFilePath) || !File.Exists(logFilePath)) return false;
        File.Copy(logFilePath, Path.Combine(staging, "macro-run.log"), overwrite: true);
        return true;
    }

    private async Task<DiagnosticDetectorPack?> TryReadDetectorPackAsync()
    {
        string directory = Path.Combine(_paths.DetectorPacks, AnimeExpeditionsDetectorSpec.PackId, "current");
        string manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath)) return null;
        try
        {
            DetectorPackManifest manifest = await JsonFileStore.ReadAsync<DetectorPackManifest>(manifestPath, CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidDataException("The active detector pack manifest is empty.");
            manifest.Validate();
            await using FileStream stream = File.OpenRead(manifestPath);
            string manifestHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, CancellationToken.None).ConfigureAwait(false));
            return new DiagnosticDetectorPack(
                manifest.PackId,
                manifest.Version,
                manifestHash,
                DetectorPackCapabilities.SupportsChallengeMaps(directory, manifest));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return null;
        }
    }

    private static void EnsureChildPath(string root, string path)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diagnostic output resolved outside the diagnostics folder.");
        }
    }

    private sealed record DiagnosticCaptureFrame(
        string File,
        DateTimeOffset CapturedAtUtc,
        string Phase,
        string? Action);

    private sealed record DiagnosticDetectorPack(
        string PackId,
        string Version,
        string ManifestSha256,
        bool SupportsChallengeMaps);

    private sealed record DiagnosticCaptureManifest(
        string Name,
        string AppVersion,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        int ClientWidth,
        int ClientHeight,
        int IntervalMilliseconds,
        DiagnosticDetectorPack? DetectorPack,
        IReadOnlyList<string> IncludedFiles,
        IReadOnlyList<DiagnosticCaptureFrame> Frames,
        string CaptureType = "manual",
        string? Failure = null,
        string? CaptureWarning = null);
}
