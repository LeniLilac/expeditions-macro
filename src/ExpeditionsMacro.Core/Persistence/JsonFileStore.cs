using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Persistence;

public static class JsonFileStore
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return default;
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        string directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The target has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
