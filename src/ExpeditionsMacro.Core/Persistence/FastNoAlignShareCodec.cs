using System.IO.Compression;
using System.Text.Json;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public static class FastNoAlignShareCodec
{
    public const string Prefix = "EMFAST1:";

    private const int MaximumCodeCharacters = 1_500_000;
    private const int MaximumJsonBytes = 2_000_000;

    private static readonly JsonSerializerOptions CompactJson =
        CreateJsonOptions();

    public static string Encode(
        FastNoAlignShareBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        FastNoAlignShareBundle normalized =
            StoryHardModePolicy.Normalize(bundle);
        normalized.Validate();
        byte[] json =
            JsonSerializer.SerializeToUtf8Bytes(
                normalized,
                CompactJson);
        if (json.Length > MaximumJsonBytes)
        {
            throw new InvalidDataException(
                "The Fast no align plan and its referenced presets or placement setups are too large to share as text.");
        }

        using MemoryStream compressed = new();
        using (BrotliStream brotli = new(
                   compressed,
                   CompressionLevel.SmallestSize,
                   leaveOpen: true))
        {
            brotli.Write(json);
        }
        string code =
            Prefix +
            Convert.ToBase64String(
                compressed.GetBuffer(),
                0,
                checked((int)compressed.Length));
        if (code.Length > MaximumCodeCharacters)
        {
            throw new InvalidDataException(
                "The Fast no align plan and its referenced presets or placement setups produce a share code that is too large.");
        }
        return code;
    }

    public static FastNoAlignShareBundle Decode(
        string code)
    {
        string trimmed = code?.Trim() ?? string.Empty;
        if (!trimmed.StartsWith(
                Prefix,
                StringComparison.Ordinal) ||
            trimmed.Length > MaximumCodeCharacters)
        {
            throw new InvalidDataException(
                "Paste a valid Fast no align share code.");
        }

        byte[] compressed;
        try
        {
            compressed = Convert.FromBase64String(
                string.Concat(
                    trimmed[Prefix.Length..]
                        .Where(character =>
                            !char.IsWhiteSpace(character))));
        }
        catch (FormatException error)
        {
            throw new InvalidDataException(
                "The Fast no align share code is damaged.",
                error);
        }

        byte[] json = Decompress(compressed);
        FastNoAlignShareBundle bundle;
        try
        {
            bundle =
                JsonSerializer.Deserialize<
                    FastNoAlignShareBundle>(
                    json,
                    CompactJson)
                ?? throw new InvalidDataException(
                    "The Fast no align share code is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException(
                "The Fast no align share code contains invalid data.",
                error);
        }
        FastNoAlignShareBundle normalized =
            StoryHardModePolicy.Normalize(bundle);
        normalized.Validate();
        return normalized;
    }

    private static byte[] Decompress(byte[] compressed)
    {
        try
        {
            using MemoryStream source = new(compressed);
            using BrotliStream brotli = new(
                source,
                CompressionMode.Decompress);
            using MemoryStream target = new();
            byte[] buffer = new byte[16 * 1024];
            while (true)
            {
                int read = brotli.Read(buffer);
                if (read == 0) break;
                if (target.Length + read >
                    MaximumJsonBytes)
                {
                    throw new InvalidDataException(
                        "The Fast no align share code expands beyond the safe size limit.");
                }
                target.Write(buffer, 0, read);
            }
            return target.ToArray();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception error) when (
            error is IOException or
            NotSupportedException)
        {
            throw new InvalidDataException(
                "The Fast no align share code is damaged.",
                error);
        }
    }

    private static JsonSerializerOptions
        CreateJsonOptions()
    {
        JsonSerializerOptions options =
            new(JsonFileStore.Options)
            {
                WriteIndented = false,
            };
        return options;
    }
}
