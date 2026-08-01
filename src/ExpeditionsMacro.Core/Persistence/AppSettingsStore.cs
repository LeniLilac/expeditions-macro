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

        // Rewriting removes retired settings and applies each one-time
        // product migration before user changes become authoritative again.
        AppSettings migrated = settings with
        {
            SchemaVersion =
                AppSettings.CurrentSchemaVersion,
        };
        if (persistedSchemaVersion < 2)
        {
            migrated = migrated with
            {
                RestartRobloxWithPrivateServer = true,
                RestartRobloxAtMacroStart = true,
                AutoCheckUiScaleOnStart = true,
                AutoCheckGameSettingsOnStart = true,
            };
        }
        else if (persistedSchemaVersion < 3)
        {
            migrated = migrated with
            {
                AutoCheckUiScaleOnStart =
                    settings
                        .AutoCheckGameSettingsOnStart,
            };
        }
        if (persistedSchemaVersion < 5)
        {
            migrated = migrated with
            {
                ResourceRefuelDebug = new(),
            };
        }
        if (persistedSchemaVersion < 6)
        {
            migrated = migrated with
            {
                AutoCheckForUpdates = true,
            };
        }
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
