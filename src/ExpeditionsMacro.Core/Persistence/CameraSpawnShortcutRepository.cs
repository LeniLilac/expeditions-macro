using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class CameraSpawnShortcutRepository : ICameraSpawnShortcutRepository
{
    private readonly AppPaths _paths;

    public CameraSpawnShortcutRepository(AppPaths paths) => _paths = paths;

    public async Task<CameraSpawnShortcut?> LoadAsync(string cameraModelId, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(_paths.CameraShortcuts, $"{ValidateId(cameraModelId)}.json");
        CameraSpawnShortcut? shortcut = await JsonFileStore.ReadAsync<CameraSpawnShortcut>(path, cancellationToken).ConfigureAwait(false);
        shortcut?.Validate();
        return shortcut;
    }

    public Task SaveAsync(CameraSpawnShortcut shortcut, CancellationToken cancellationToken = default)
    {
        shortcut.Validate();
        _paths.EnsureCreated();
        string path = Path.Combine(_paths.CameraShortcuts, $"{ValidateId(shortcut.CameraModelId)}.json");
        return JsonFileStore.WriteAtomicAsync(path, shortcut, cancellationToken);
    }

    public Task DeleteAsync(string cameraModelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.Combine(_paths.CameraShortcuts, $"{ValidateId(cameraModelId)}.json");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid camera model id.", nameof(id));
        return id;
    }
}
