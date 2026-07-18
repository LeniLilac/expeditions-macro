using System.Globalization;
using System.IO;
using System.Text;

namespace ExpeditionsMacro.App.Services;

public sealed class FileLogger
{
    private readonly object _gate = new();

    public FileLogger(string directory)
    {
        Directory.CreateDirectory(directory);
        CurrentFile = Path.Combine(
            directory,
            $"session-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
    }

    public string CurrentFile { get; }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        string normalized = message.Replace("\0", string.Empty, StringComparison.Ordinal).TrimEnd();
        string line = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)} [{level}] {normalized}{Environment.NewLine}";
        lock (_gate)
        {
            File.AppendAllText(CurrentFile, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
