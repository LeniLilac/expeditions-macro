using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal sealed class DeepDebugArchiveTextWriter
{
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<string?> _getLogFilePath;

    public DeepDebugArchiveTextWriter(
        Func<AppSettings> getSettings,
        Func<string?> getLogFilePath)
    {
        _getSettings = getSettings;
        _getLogFilePath = getLogFilePath;
    }

    public string Redact(string text) =>
        DeepDebugSecretRedactor.Redact(text, _getSettings());

    public Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(
            path,
            Redact(JsonSerializer.Serialize(value, JsonFileStore.Options)),
            new UTF8Encoding(false),
            CancellationToken.None);
    }

    public void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (IsTextDiagnosticFile(file))
            {
                File.WriteAllText(
                    target,
                    Redact(File.ReadAllText(file)),
                    new UTF8Encoding(false));
            }
            else
            {
                File.Copy(file, target, overwrite: true);
            }
        }
    }

    public async Task CopySanitizedRunLogAsync(string staging)
    {
        string? source = _getLogFilePath();
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return;
        try
        {
            await using FileStream stream = new(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            string text = await reader
                .ReadToEndAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(
                    Path.Combine(staging, "macro-run-sanitized.log"),
                    Redact(text),
                    new UTF8Encoding(false),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException)
        {
            await File.WriteAllTextAsync(
                    Path.Combine(staging, "macro-run-copy-error.txt"),
                    Redact(error.Message),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    public void TryWriteFinalizationError(
        string directory,
        Exception error)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "finalization-error.txt"),
                Redact(error.ToString()));
        }
        catch
        {
            // The application log still receives the sanitized failure message.
        }
    }

    private static bool IsTextDiagnosticFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".json" or
            ".jsonl" or
            ".log" or
            ".md" or
            ".txt" or
            ".xml";
}
