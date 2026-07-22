using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class StoryPresetRepository
{
    private readonly AppPaths _paths;

    public StoryPresetRepository(AppPaths paths) => _paths = paths;

    public Task<IReadOnlyList<StoryPreset>> ListAsync(CancellationToken cancellationToken = default) =>
        NamedJsonRepository.ListAsync<StoryPreset>(_paths.StoryPresets, preset => preset.Name, preset => preset.Validate(), cancellationToken);

    public Task<StoryPreset?> LoadAsync(string id, CancellationToken cancellationToken = default) =>
        NamedJsonRepository.LoadAsync<StoryPreset>(_paths.StoryPresets, id, preset => preset.Validate(), cancellationToken);

    public Task SaveAsync(StoryPreset preset, CancellationToken cancellationToken = default)
    {
        preset.Validate();
        return NamedJsonRepository.SaveAsync(_paths.StoryPresets, preset.Id, preset, cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        NamedJsonRepository.DeleteAsync(_paths.StoryPresets, id, cancellationToken);
}

public sealed class RaidPresetRepository
{
    private readonly AppPaths _paths;

    public RaidPresetRepository(AppPaths paths) => _paths = paths;

    public Task<IReadOnlyList<RaidPreset>> ListAsync(CancellationToken cancellationToken = default) =>
        NamedJsonRepository.ListAsync<RaidPreset>(_paths.RaidPresets, preset => preset.Name, preset => preset.Validate(), cancellationToken);

    public Task<RaidPreset?> LoadAsync(string id, CancellationToken cancellationToken = default) =>
        NamedJsonRepository.LoadAsync<RaidPreset>(_paths.RaidPresets, id, preset => preset.Validate(), cancellationToken);

    public Task SaveAsync(RaidPreset preset, CancellationToken cancellationToken = default)
    {
        preset.Validate();
        return NamedJsonRepository.SaveAsync(_paths.RaidPresets, preset.Id, preset, cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        NamedJsonRepository.DeleteAsync(_paths.RaidPresets, id, cancellationToken);
}

internal static class NamedJsonRepository
{
    public static async Task<IReadOnlyList<T>> ListAsync<T>(
        string directory,
        Func<T, string> name,
        Action<T> validate,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        List<T> items = [];
        foreach (string file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                T? item = await JsonFileStore.ReadAsync<T>(file, cancellationToken).ConfigureAwait(false);
                if (item is null) continue;
                validate(item);
                items.Add(item);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                // Keep listing healthy user presets.
            }
        }

        return items.OrderBy(name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static async Task<T?> LoadAsync<T>(string directory, string id, Action<T> validate, CancellationToken cancellationToken)
    {
        string path = Path.Combine(directory, $"{ValidateId(id)}.json");
        T? item = await JsonFileStore.ReadAsync<T>(path, cancellationToken).ConfigureAwait(false);
        if (item is not null) validate(item);
        return item;
    }

    public static Task SaveAsync<T>(string directory, string id, T value, CancellationToken cancellationToken) =>
        JsonFileStore.WriteAtomicAsync(Path.Combine(directory, $"{ValidateId(id)}.json"), value, cancellationToken);

    public static Task DeleteAsync(string directory, string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(Path.Combine(directory, $"{ValidateId(id)}.json"));
        return Task.CompletedTask;
    }

    public static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid id.", nameof(id));
        return id;
    }
}
