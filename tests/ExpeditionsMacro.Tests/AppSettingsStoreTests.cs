using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task LegacySettings_DefaultStartupChecksToEnabled()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                """
                {
                  "theme": "dark"
                }
                """);

            AppSettings loaded =
                await new AppSettingsStore(paths).LoadAsync();

            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
            Assert.True(
                loaded.RestartRobloxAtMacroStart);
            Assert.Equal(
                AppSettings.DefaultCancelPlacementKey,
                loaded.CancelPlacementKey);
            Assert.Equal(
                string.Empty,
                loaded.ChangeUnitTargetingKey);
            Assert.Equal(
                string.Empty,
                loaded.UpgradeUnitKey);
            Assert.Equal(
                string.Empty,
                loaded.ToggleAutoUpgradeUnitKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Beta20Settings_DefaultToFastNoAlignWithoutLosingLegacySelections()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                """
                {
                  "selected_preset_id": "legacy-expedition",
                  "selected_challenge_preset_id": "legacy-challenge",
                  "selected_story_preset_id": "legacy-story",
                  "selected_raid_preset_id": "legacy-raid",
                  "selected_macro_plan_id": "legacy-plan"
                }
                """);

            AppSettings loaded =
                await new AppSettingsStore(paths).LoadAsync();

            Assert.True(loaded.FastNoAlignEnabled);
            Assert.Equal(
                "legacy-expedition",
                loaded.SelectedPresetId);
            Assert.Equal(
                "legacy-challenge",
                loaded.SelectedChallengePresetId);
            Assert.Equal(
                "legacy-story",
                loaded.SelectedStoryPresetId);
            Assert.Equal(
                "legacy-raid",
                loaded.SelectedRaidPresetId);
            Assert.Equal(
                "legacy-plan",
                loaded.SelectedMacroPlanId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReportingSettings_SurviveAStoreRestart()
    {
        string root = Path.Combine(Path.GetTempPath(), $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppSettingsStore firstProcess = new(new AppPaths(root));
            await firstProcess.SaveAsync(new AppSettings
            {
                SelectedMacroPlanId = "daily-plan",
                EncryptedWebhook = "dpapi-protected-test-value",
                EncryptedPrivateServerLink =
                    "dpapi-protected-private-server",
                RestartRobloxWithPrivateServer = true,
                RestartRobloxAtMacroStart = false,
                DebugModeEnabled = true,
                DiscordErrorUserId = "123456789012345678",
                ShiftLockVirtualKey = KeyboardKey.RightShift,
                AreasMenuKey = "G",
                CancelPlacementKey = "X",
                ChangeUnitTargetingKey = "T",
                UpgradeUnitKey = "Y",
                ToggleAutoUpgradeUnitKey = "V",
                ResourceRefuelDebug =
                    new ResourceRefuelDebugSettings
                    {
                        GoldForward1Milliseconds = 4321,
                        RetryCount = 4,
                    },
            });

            AppSettingsStore restartedProcess = new(new AppPaths(root));
            AppSettings loaded = await restartedProcess.LoadAsync();

            Assert.Equal("daily-plan", loaded.SelectedMacroPlanId);
            Assert.Equal("dpapi-protected-test-value", loaded.EncryptedWebhook);
            Assert.Equal(
                "dpapi-protected-private-server",
                loaded.EncryptedPrivateServerLink);
            Assert.True(loaded.RestartRobloxWithPrivateServer);
            Assert.False(loaded.RestartRobloxAtMacroStart);
            Assert.True(loaded.DebugModeEnabled);
            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
            Assert.True(loaded.FastNoAlignEnabled);
            Assert.Equal("123456789012345678", loaded.DiscordErrorUserId);
            Assert.Equal(KeyboardKey.RightShift, loaded.ShiftLockVirtualKey);
            Assert.Equal("G", loaded.AreasMenuKey);
            Assert.Equal("X", loaded.CancelPlacementKey);
            Assert.Equal(
                "T",
                loaded.ChangeUnitTargetingKey);
            Assert.Equal(
                "Y",
                loaded.UpgradeUnitKey);
            Assert.Equal(
                "V",
                loaded.ToggleAutoUpgradeUnitKey);
            Assert.Equal(
                4321,
                loaded.ResourceRefuelDebug
                    .GoldForward1Milliseconds);
            Assert.Equal(
                4,
                loaded.ResourceRefuelDebug.RetryCount);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
