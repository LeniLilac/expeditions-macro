using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class ApplicationUpdateSettingsTests
{
    [Fact]
    public async Task NewSettings_EnableAutomaticChecks()
    {
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            AppSettings settings =
                await new AppSettingsStore(paths).LoadAsync();

            Assert.True(settings.AutoCheckForUpdates);
            Assert.Equal(6, settings.SchemaVersion);
            Assert.True(Directory.Exists(paths.Updates));
            Assert.Equal(
                Path.Combine(root, "updates", "stage.json"),
                paths.UpdateStageFile);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SchemaFive_EnablesOnceThenPreservesOptOut()
    {
        string root = NewRoot();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                """
                {
                  "schema_version": 5,
                  "auto_check_for_updates": false
                }
                """);
            AppSettingsStore store = new(paths);

            AppSettings migrated = await store.LoadAsync();
            Assert.True(migrated.AutoCheckForUpdates);
            Assert.Equal(6, migrated.SchemaVersion);

            await store.SaveAsync(migrated with
            {
                AutoCheckForUpdates = false,
            });
            AppSettings reloaded = await store.LoadAsync();

            Assert.False(reloaded.AutoCheckForUpdates);
            Assert.Equal(6, reloaded.SchemaVersion);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string NewRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            $"expeditions-update-settings-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
