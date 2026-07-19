using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class ChallengePresetRepository
{
    private readonly AppPaths _paths;

    public ChallengePresetRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<ChallengePreset>> ListAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        List<ChallengePreset> presets = [];
        foreach (string file in Directory.EnumerateFiles(_paths.ChallengePresets, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                ChallengePreset? preset = await JsonFileStore.ReadAsync<ChallengePreset>(file, cancellationToken).ConfigureAwait(false);
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

    public async Task<ChallengePreset?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(_paths.ChallengePresets, $"{ValidateId(id)}.json");
        ChallengePreset? preset = await JsonFileStore.ReadAsync<ChallengePreset>(path, cancellationToken).ConfigureAwait(false);
        preset?.Validate();
        return preset;
    }

    public Task SaveAsync(ChallengePreset preset, CancellationToken cancellationToken = default)
    {
        preset.Validate();
        return JsonFileStore.WriteAtomicAsync(Path.Combine(_paths.ChallengePresets, $"{ValidateId(preset.Id)}.json"), preset, cancellationToken);
    }

    private static string ValidateId(string id)
    {
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new ArgumentException("Invalid preset id.", nameof(id));
        return id;
    }
}
