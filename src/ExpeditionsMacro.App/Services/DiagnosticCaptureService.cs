using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Services;

public sealed record DiagnosticCaptureProgress(int Captures, string Message);

public sealed record DiagnosticCaptureResult(string ArchivePath, int Captures, string? RestoreWarning = null);

public sealed class DiagnosticCaptureService
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IRobloxAutomation _automation;
    private readonly AppPaths _paths;

    public DiagnosticCaptureService(IRobloxAutomation automation, AppPaths paths)
    {
        _automation = automation;
        _paths = paths;
    }

    public async Task<DiagnosticCaptureResult> CaptureAsync(
        string captureName,
        TimeSpan interval,
        IProgress<DiagnosticCaptureProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string safeName = SafeName(captureName);
        if (interval < TimeSpan.FromMilliseconds(100) || interval > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Capture interval must be between 0.1 and 300 seconds.");
        }

        RobloxWindow window = _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");
        WindowBounds originalBounds = _automation.GetWindowBounds(window);
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
        Exception? restoreError = null;
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
                frames.Add(new DiagnosticCaptureFrame(fileName, capturedAt));
                progress?.Report(new DiagnosticCaptureProgress(frames.Count, $"Captured {frames.Count} screenshot(s). Press F6 to stop and save."));
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
        finally
        {
            try
            {
                _automation.RestoreWindowBounds(window, originalBounds);
            }
            catch (Exception error)
            {
                // Preserve the captures even if Roblox closed before its bounds could be restored.
                restoreError = error;
            }
        }

        try
        {
            if (frames.Count == 0)
            {
                if (stopped) throw new OperationCanceledException(cancellationToken);
                throw new InvalidOperationException("No screenshots were captured.");
            }

            DiagnosticCaptureManifest manifest = new(
                safeName,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
                startedAt,
                DateTimeOffset.UtcNow,
                ClientWidth,
                ClientHeight,
                (int)Math.Round(interval.TotalMilliseconds),
                frames);
            await File.WriteAllTextAsync(
                Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonOptions),
                CancellationToken.None).ConfigureAwait(false);

            if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
            ZipFile.CreateFromDirectory(staging, temporaryArchive, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(temporaryArchive, archivePath, overwrite: true);
            completed = true;
            string? warning = restoreError is null ? null : $"Roblox bounds could not be restored: {restoreError.Message}";
            string message = $"Saved {frames.Count} screenshot(s) to {Path.GetFileName(archivePath)}.";
            if (warning is not null) message += $" {warning}";
            progress?.Report(new DiagnosticCaptureProgress(frames.Count, message));
            return new DiagnosticCaptureResult(archivePath, frames.Count, warning);
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

    private static void EnsureChildPath(string root, string path)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diagnostic output resolved outside the diagnostics folder.");
        }
    }

    private sealed record DiagnosticCaptureFrame(string File, DateTimeOffset CapturedAtUtc);

    private sealed record DiagnosticCaptureManifest(
        string Name,
        string AppVersion,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        int ClientWidth,
        int ClientHeight,
        int IntervalMilliseconds,
        IReadOnlyList<DiagnosticCaptureFrame> Frames);
}
