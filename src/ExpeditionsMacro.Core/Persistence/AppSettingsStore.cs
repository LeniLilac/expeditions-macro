using System.Text.Json;
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
        if (!File.Exists(_paths.SettingsFile))
        {
            return new AppSettings();
        }

        AppSettings settings;
        int persistedSchemaVersion;
        await using (FileStream stream = new(
                         _paths.SettingsFile,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read))
        {
            using JsonDocument document =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            settings =
                document.RootElement
                    .Deserialize<AppSettings>(
                        JsonFileStore.Options) ??
                new AppSettings();
            persistedSchemaVersion =
                PersistedSchemaVersion(
                    document.RootElement);
        }
        if (persistedSchemaVersion >=
            AppSettings.CurrentSchemaVersion)
        {
            return settings;
        }

        AppSettings migrated =
            persistedSchemaVersion < 2
                ? settings with
                {
                    SchemaVersion =
                        AppSettings.CurrentSchemaVersion,
                    RestartRobloxWithPrivateServer = true,
                    RestartRobloxAtMacroStart = true,
                    AutoCheckUiScaleOnStart = true,
                    AutoCheckGameSettingsOnStart = true,
                }
                : settings with
                {
                    SchemaVersion =
                        AppSettings.CurrentSchemaVersion,
                    AutoCheckUiScaleOnStart =
                        settings
                            .AutoCheckGameSettingsOnStart,
                };
        await SaveAsync(
            migrated,
            cancellationToken).ConfigureAwait(false);
        return migrated;
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return JsonFileStore.WriteAtomicAsync(_paths.SettingsFile, settings, cancellationToken);
    }

    private static int PersistedSchemaVersion(
        JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(
                "schema_version",
                out JsonElement value) &&
            value.TryGetInt32(out int version)
                ? version
                : 0;
    }
}
