using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Vision.Camera;

public sealed class CameraModelRepository : ICameraModelRepository
{
    private const int TransactionRetryAttempts = 40;
    private static readonly TimeSpan TransactionRetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly AppPaths _paths;

    public CameraModelRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<CameraModelManifest>> ListAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        List<CameraModelManifest> manifests = [];
        foreach (string directory in Directory.EnumerateDirectories(_paths.CameraModels, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal)) continue;
            string file = Path.Combine(directory, "manifest.json");
            if (!File.Exists(file)) continue;
            try
            {
                CameraModelManifest? manifest = await JsonFileStore.ReadAsync<CameraModelManifest>(file, cancellationToken).ConfigureAwait(false);
                manifest?.Validate();
                if (manifest is not null) manifests.Add(manifest);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                // A broken model is skipped while the remaining catalog loads.
            }
        }
        return manifests.OrderBy(model => model.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task<CameraModel?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        string directory = Path.Combine(_paths.CameraModels, ValidateId(id));
        CameraModelManifest? manifest = await JsonFileStore.ReadAsync<CameraModelManifest>(Path.Combine(directory, "manifest.json"), cancellationToken).ConfigureAwait(false);
        if (manifest is null) return null;
        manifest.Validate();
        ImageFrame reference = ImageCodec.Load(Path.Combine(directory, "reference.png"), PixelFormat.Gray8);
        ImageFrame overlay = ImageCodec.Load(Path.Combine(directory, "goal.png"), PixelFormat.Rgb24);
        string fineDirectory = Path.Combine(directory, "fine");
        List<ImageFrame> fineAtlas = Directory.EnumerateFiles(fineDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => ImageCodec.Load(path, PixelFormat.Gray8))
            .ToList();
        string yawDirectory = Path.Combine(directory, "yaw");
        List<ImageFrame> atlas = Directory.EnumerateFiles(yawDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => ImageCodec.Load(path, PixelFormat.Gray8))
            .ToList();
        if (atlas.Count != manifest.AtlasSampleCount) throw new InvalidDataException("Camera model yaw atlas is incomplete.");
        if (fineAtlas.Count != manifest.FineYawOffsets.Count) throw new InvalidDataException("Camera model fine yaw atlas is incomplete.");
        return new CameraModel(manifest, reference, overlay, fineAtlas, atlas);
    }

    public async Task SaveAsync(CameraModel model, CancellationToken cancellationToken = default)
    {
        model.Manifest.Validate();
        if (model.Reference.Format != PixelFormat.Gray8 || model.GoalOverlay.Format != PixelFormat.Rgb24) throw new InvalidDataException("Camera model image formats are invalid.");
        if (model.FineYawAtlas.Count != model.Manifest.FineYawOffsets.Count || model.FineYawAtlas.Any(frame => frame.Format != PixelFormat.Gray8)) throw new InvalidDataException("Camera model fine yaw atlas is invalid.");
        if (model.YawAtlas.Count != model.Manifest.AtlasSampleCount || model.YawAtlas.Any(frame => frame.Format != PixelFormat.Gray8)) throw new InvalidDataException("Camera model yaw atlas is invalid.");
        _paths.EnsureCreated();
        string id = ValidateId(model.Manifest.Id);
        string target = Path.Combine(_paths.CameraModels, id);
        string staging = Path.Combine(_paths.CameraModels, $".{id}.{Guid.NewGuid():N}.staging");
        string backup = Path.Combine(_paths.CameraModels, $".{id}.{Guid.NewGuid():N}.backup");
        try
        {
            Directory.CreateDirectory(Path.Combine(staging, "fine"));
            Directory.CreateDirectory(Path.Combine(staging, "yaw"));
            await JsonFileStore.WriteAtomicAsync(Path.Combine(staging, "manifest.json"), model.Manifest, cancellationToken).ConfigureAwait(false);
            ImageCodec.SavePng(Path.Combine(staging, "reference.png"), model.Reference);
            ImageCodec.SavePng(Path.Combine(staging, "goal.png"), model.GoalOverlay);
            for (int index = 0; index < model.FineYawAtlas.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImageCodec.SavePng(Path.Combine(staging, "fine", $"{index:D4}.png"), model.FineYawAtlas[index]);
            }
            for (int index = 0; index < model.YawAtlas.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImageCodec.SavePng(Path.Combine(staging, "yaw", $"{index:D4}.png"), model.YawAtlas[index]);
            }
            if (Directory.Exists(target)) await MoveDirectoryWithRetryAsync(target, backup, cancellationToken).ConfigureAwait(false);
            await MoveDirectoryWithRetryAsync(staging, target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception saveError)
        {
            if (!Directory.Exists(target) && Directory.Exists(backup))
            {
                try
                {
                    await MoveDirectoryWithRetryAsync(backup, target, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception restoreError)
                {
                    throw new IOException(
                        $"Camera model '{id}' could not be saved or restored. Its backup remains at '{backup}'.",
                        new AggregateException(saveError, restoreError));
                }
            }
            throw;
        }
        finally
        {
            await TryDeleteDirectoryAsync(staging).ConfigureAwait(false);
            if (Directory.Exists(target)) await TryDeleteDirectoryAsync(backup).ConfigureAwait(false);
        }
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string validId = ValidateId(id);
        string directory = Path.Combine(_paths.CameraModels, validId);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        string shortcut = Path.Combine(_paths.CameraShortcuts, $"{validId}.json");
        if (File.Exists(shortcut)) File.Delete(shortcut);
        return Task.CompletedTask;
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid camera model id.", nameof(id));
        return id;
    }

    private static async Task MoveDirectoryWithRetryAsync(string source, string destination, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException && attempt < TransactionRetryAttempts)
            {
                await Task.Delay(TransactionRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task TryDeleteDirectoryAsync(string directory)
    {
        for (int attempt = 1; attempt <= TransactionRetryAttempts; attempt++)
        {
            if (!Directory.Exists(directory)) return;
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt == TransactionRetryAttempts) return;
                await Task.Delay(TransactionRetryDelay).ConfigureAwait(false);
            }
        }
    }
}
