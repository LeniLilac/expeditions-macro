using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class PresetRepository
{
    private readonly AppPaths _paths;

    public PresetRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<ExpeditionPreset>> ListAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        List<ExpeditionPreset> presets = [];
        foreach (string file in Directory.EnumerateFiles(_paths.Presets, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                ExpeditionPreset? preset = await JsonFileStore.ReadAsync<ExpeditionPreset>(file, cancellationToken).ConfigureAwait(false);
                preset?.Validate();
                if (preset is not null) presets.Add(preset);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                // Continue listing healthy presets.
            }
        }

        return presets.OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task<ExpeditionPreset?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(_paths.Presets, $"{ValidateId(id)}.json");
        ExpeditionPreset? preset = await JsonFileStore.ReadAsync<ExpeditionPreset>(path, cancellationToken).ConfigureAwait(false);
        preset?.Validate();
        return preset;
    }

    public Task SaveAsync(ExpeditionPreset preset, CancellationToken cancellationToken = default)
    {
        preset.Validate();
        return JsonFileStore.WriteAtomicAsync(Path.Combine(_paths.Presets, $"{ValidateId(preset.Id)}.json"), preset, cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(Path.Combine(_paths.Presets, $"{ValidateId(id)}.json"));
        return Task.CompletedTask;
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid preset id.", nameof(id));
        return id;
    }
}
