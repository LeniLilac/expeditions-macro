using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class AppSettingsStore
{
    private readonly AppPaths _paths;

    public AppSettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return await JsonFileStore.ReadAsync<AppSettings>(_paths.SettingsFile, cancellationToken).ConfigureAwait(false) ?? new AppSettings();
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return JsonFileStore.WriteAtomicAsync(_paths.SettingsFile, settings, cancellationToken);
    }
}
