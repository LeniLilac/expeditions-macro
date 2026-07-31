using System.IO;
using System.Security.Cryptography;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.DetectorViewer.Services;

public static class BundledDetectorPackLoader
{
    public static async Task<IDetectorPack> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "DetectorPacks",
                AnimeExpeditionsDetectorSpec.PackId,
                AnimeExpeditionsDetectorSpec
                    .BundledPackVersion));
        string manifestPath =
            Path.Combine(root, "manifest.json");
        DetectorPackManifest manifest =
            await JsonFileStore
                .ReadAsync<DetectorPackManifest>(
                    manifestPath,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidDataException(
                $"The bundled detector manifest is missing at '{manifestPath}'.");
        manifest.Validate();
        if (!manifest.PackId.Equals(
                AnimeExpeditionsDetectorSpec.PackId,
                StringComparison.Ordinal) ||
            !manifest.Version.Equals(
                AnimeExpeditionsDetectorSpec
                    .BundledPackVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The bundled detector pack identity does not match the production detector specification.");
        }

        foreach (DetectorPackFile file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Resolve(root, file.Path);
            FileInfo info = new(path);
            if (!info.Exists ||
                info.Length != file.Bytes)
            {
                throw new InvalidDataException(
                    $"Bundled detector payload '{file.Path}' is missing or has the wrong size.");
            }
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            byte[] hash =
                await SHA256.HashDataAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);
            string actual =
                Convert.ToHexString(hash)
                    .ToLowerInvariant();
            if (!actual.Equals(
                    file.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Bundled detector payload '{file.Path}' failed its SHA-256 check.");
            }
        }
        return new CompiledDetectorPack(root, manifest);
    }

    private static string Resolve(
        string root,
        string relative)
    {
        string normalized =
            relative.Replace(
                '/',
                Path.DirectorySeparatorChar);
        string path = Path.GetFullPath(
            Path.Combine(root, normalized));
        string prefix =
            root.EndsWith(
                Path.DirectorySeparatorChar)
                ? root
                : root +
                  Path.DirectorySeparatorChar;
        if (!path.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Detector payload path '{relative}' leaves the bundled pack directory.");
        }
        return path;
    }
}
