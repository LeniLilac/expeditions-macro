using System.Security.Cryptography;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Vision.Packs;

public sealed class DetectorPackRepository : IDetectorPackRepository
{
    private readonly AppPaths _paths;

    public DetectorPackRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<DetectorPackManifest>> ListAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        List<DetectorPackManifest> manifests = [];
        foreach (string path in Directory.EnumerateFiles(_paths.DetectorPacks, "manifest.json", SearchOption.AllDirectories)
                     .Where(path => string.Equals(new DirectoryInfo(Path.GetDirectoryName(path)!).Name, "current", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                DetectorPackManifest? manifest = await JsonFileStore.ReadAsync<DetectorPackManifest>(path, cancellationToken).ConfigureAwait(false);
                manifest?.Validate();
                if (manifest is not null) manifests.Add(manifest);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                // Continue listing healthy packs.
            }
        }
        return manifests.OrderBy(pack => pack.PackId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IDetectorPack?> LoadAsync(string packId, CancellationToken cancellationToken = default)
    {
        string directory = Path.Combine(_paths.DetectorPacks, ValidateId(packId), "current");
        DetectorPackManifest? manifest = await JsonFileStore.ReadAsync<DetectorPackManifest>(Path.Combine(directory, "manifest.json"), cancellationToken).ConfigureAwait(false);
        if (manifest is null) return null;
        await ValidateFilesAsync(directory, manifest, cancellationToken).ConfigureAwait(false);
        return new CompiledDetectorPack(directory, manifest);
    }

    private async Task InstallBundledDirectoryAsync(
        string sourceDirectory,
        DetectorPackManifest manifest,
        CancellationToken cancellationToken)
    {
        await ValidateFilesAsync(sourceDirectory, manifest, cancellationToken).ConfigureAwait(false);
        string staging = Path.Combine(_paths.DetectorPacks, $".copy.{Guid.NewGuid():N}");
        CopyDirectory(sourceDirectory, staging);
        try
        {
            ReplaceCurrentDirectory(staging, manifest.PackId);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public async Task<bool> EnsureBundledAsync(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        DetectorPackManifest bundled;
        try
        {
            bundled = await JsonFileStore.ReadAsync<DetectorPackManifest>(Path.Combine(sourceDirectory, "manifest.json"), cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Bundled detector pack has no manifest.");
            bundled.Validate();
            await ValidateFilesAsync(sourceDirectory, bundled, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                System.Text.Json.JsonException)
        {
            throw BundledPackDamaged(error);
        }

        DetectorPackManifest? current = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(pack => pack.PackId.Equals(bundled.PackId, StringComparison.OrdinalIgnoreCase));

        if (current is not null)
        {
            string currentDirectory = Path.Combine(_paths.DetectorPacks, ValidateId(current.PackId), "current");
            try
            {
                await ValidateFilesAsync(currentDirectory, current, cancellationToken).ConfigureAwait(false);
                string currentManifestPath = Path.Combine(currentDirectory, "manifest.json");
                if (await HasSameManifestAsync(
                    currentManifestPath,
                    Path.Combine(sourceDirectory, "manifest.json"),
                    cancellationToken).ConfigureAwait(false))
                {
                    RemoveLegacyPrevious(current.PackId);
                    return false;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Replace an installed pack whose manifest or declared payload is no longer healthy,
                // even when its version is newer than the copy bundled with the app.
            }
        }

        try
        {
            await InstallBundledDirectoryAsync(
                sourceDirectory,
                bundled,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                System.Text.Json.JsonException)
        {
            throw BundledPackDamaged(error);
        }
        return true;
    }

    private void ReplaceCurrentDirectory(
        string source,
        string packId)
    {
        string root = Path.Combine(_paths.DetectorPacks, ValidateId(packId));
        string current = Path.Combine(root, "current");
        string previous = Path.Combine(root, "previous");
        string incoming = Path.Combine(root, $".incoming.{Guid.NewGuid():N}");
        string displaced = Path.Combine(root, $".replaced.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        CopyDirectory(source, incoming);
        try
        {
            if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
            if (Directory.Exists(current)) Directory.Move(current, displaced);
            Directory.Move(incoming, current);
            if (Directory.Exists(displaced)) Directory.Delete(displaced, recursive: true);
        }
        catch
        {
            if (!Directory.Exists(current) && Directory.Exists(displaced))
            {
                Directory.Move(displaced, current);
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(incoming)) Directory.Delete(incoming, recursive: true);
            if (Directory.Exists(displaced) && Directory.Exists(current))
            {
                Directory.Delete(displaced, recursive: true);
            }
        }
    }

    private void RemoveLegacyPrevious(string packId)
    {
        string previous = Path.Combine(
            _paths.DetectorPacks,
            ValidateId(packId),
            "previous");
        if (Directory.Exists(previous))
        {
            Directory.Delete(previous, recursive: true);
        }
    }

    private static async Task ValidateFilesAsync(string root, DetectorPackManifest manifest, CancellationToken cancellationToken)
    {
        foreach (DetectorPackFile expected in manifest.Files)
        {
            string path = SafeArchivePath(root, expected.Path);
            FileInfo info = new(path);
            if (!info.Exists || info.Length != expected.Bytes) throw new InvalidDataException($"Detector pack file '{expected.Path}' is missing or has the wrong size.");
            await using FileStream stream = info.OpenRead();
            string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Detector pack file '{expected.Path}' failed its SHA-256 check.");
        }
    }

    private static async Task<bool> HasSameManifestAsync(string leftPath, string rightPath, CancellationToken cancellationToken)
    {
        await using FileStream left = File.OpenRead(leftPath);
        await using FileStream right = File.OpenRead(rightPath);
        byte[] leftHash = await SHA256.HashDataAsync(left, cancellationToken).ConfigureAwait(false);
        byte[] rightHash = await SHA256.HashDataAsync(right, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static InvalidDataException BundledPackDamaged(Exception innerException) =>
        new(
            "The detector files bundled with this copy of Expeditions Macro are incomplete or damaged. " +
            "Reinstall the app, or extract the portable ZIP into a new empty folder.",
            innerException);

    private static string SafeArchivePath(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Detector package contains an unsafe path.");
        return path;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid detector pack id.", nameof(id));
        return id;
    }
}
