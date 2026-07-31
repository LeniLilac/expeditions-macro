using System.Text.Json;
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
                loaded.AutoCheckUiScaleOnStart);
            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
            Assert.True(
                loaded.RestartRobloxAtMacroStart);
            Assert.True(
                loaded.RestartRobloxWithPrivateServer);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                loaded.SchemaVersion);
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
                loaded.SellUnitKey);
            Assert.Equal(
                string.Empty,
                loaded.AutoUpgradeUnitKey);
            Assert.Equal(
                string.Empty,
                loaded.ToggleAutoUpgradePlacedUnitsKey);
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
    public async Task LegacySafetyDefaults_AreForcedOnOnceThenRespectUserChanges()
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
                  "schema_version": 1,
                  "restart_roblox_with_private_server": false,
                  "restart_roblox_at_macro_start": false,
                  "auto_check_game_settings_on_start": false
                }
                """);
            AppSettingsStore store = new(paths);

            AppSettings migrated = await store.LoadAsync();

            Assert.True(
                migrated.RestartRobloxWithPrivateServer);
            Assert.True(
                migrated.RestartRobloxAtMacroStart);
            Assert.True(
                migrated.AutoCheckUiScaleOnStart);
            Assert.True(
                migrated.AutoCheckGameSettingsOnStart);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                migrated.SchemaVersion);
            string normalized =
                await File.ReadAllTextAsync(
                    paths.SettingsFile);
            Assert.Contains(
                "\"schema_version\": 5",
                normalized,
                StringComparison.Ordinal);

            await store.SaveAsync(
                migrated with
                {
                    RestartRobloxWithPrivateServer = false,
                    RestartRobloxAtMacroStart = false,
                    AutoCheckUiScaleOnStart = false,
                    AutoCheckGameSettingsOnStart = false,
                });
            AppSettings reloaded = await store.LoadAsync();

            Assert.False(
                reloaded.RestartRobloxWithPrivateServer);
            Assert.False(
                reloaded.RestartRobloxAtMacroStart);
            Assert.False(
                reloaded.AutoCheckUiScaleOnStart);
            Assert.False(
                reloaded.AutoCheckGameSettingsOnStart);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                reloaded.SchemaVersion);
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
    public async Task
        SchemaTwoCombinedPreparationChoice_MigratesToBothChecks()
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
                  "schema_version": 2,
                  "auto_check_game_settings_on_start": false
                }
                """);

            AppSettings migrated =
                await new AppSettingsStore(paths).LoadAsync();

            Assert.False(
                migrated.AutoCheckUiScaleOnStart);
            Assert.False(
                migrated.AutoCheckGameSettingsOnStart);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                migrated.SchemaVersion);
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
    public async Task
        SchemaThreeFastNoAlignChoice_IsRetiredWithoutLosingSettings()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-settings-{Guid.NewGuid():N}");
        const string schemaThree =
            """
            {
              "schema_version": 3,
              "theme": "light",
              "selected_preset_id": "legacy-expedition",
              "selected_challenge_preset_id": "legacy-challenge",
              "selected_story_preset_id": "legacy-story",
              "selected_raid_preset_id": "legacy-raid",
              "selected_macro_plan_id": "legacy-plan",
              "encrypted_webhook": "protected-webhook",
              "encrypted_private_server_link": "protected-server",
              "restart_roblox_with_private_server": false,
              "restart_roblox_at_macro_start": false,
              "discord_error_user_id": "123456789012345678",
              "auto_capture_on_macro_error": false,
              "include_logs_in_diagnostic_archives": false,
              "deep_debug_enabled": true,
              "debug_mode_enabled": true,
              "auto_check_ui_scale_on_start": false,
              "auto_check_game_settings_on_start": true,
              "fast_no_align_enabled": false,
              "manual_input_recording_enabled": true,
              "minimize_during_automation": true,
              "macro_hotkey_virtual_key": 119,
              "shift_lock_virtual_key": 160,
              "play_menu_key": "P",
              "unit_menu_key": "H",
              "areas_menu_key": "U",
              "cancel_placement_key": "Z",
              "change_unit_targeting_key": "R",
              "upgrade_unit_key": "T",
              "auto_upgrade_unit_key": "K",
              "toggle_auto_upgrade_unit_key": "L"
            }
            """;
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                schemaThree);

            AppSettings loaded =
                await new AppSettingsStore(paths).LoadAsync();
            string normalized =
                await File.ReadAllTextAsync(
                    paths.SettingsFile);

            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                loaded.SchemaVersion);
            Assert.Equal(AppTheme.Light, loaded.Theme);
            Assert.False(
                loaded.RestartRobloxWithPrivateServer);
            Assert.False(
                loaded.RestartRobloxAtMacroStart);
            Assert.False(
                loaded.AutoCheckUiScaleOnStart);
            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
            Assert.True(
                loaded.ManualInputRecordingEnabled);
            Assert.True(loaded.MinimizeDuringAutomation);
            Assert.DoesNotContain(
                "fast_no_align_enabled",
                normalized,
                StringComparison.Ordinal);

            using JsonDocument before =
                JsonDocument.Parse(schemaThree);
            using JsonDocument after =
                JsonDocument.Parse(normalized);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                after.RootElement
                    .GetProperty("schema_version")
                    .GetInt32());
            foreach (JsonProperty property in
                     before.RootElement
                         .EnumerateObject()
                         .Where(property =>
                             property.Name is not
                                 ("schema_version" or
                                  "fast_no_align_enabled")))
            {
                Assert.True(
                    after.RootElement.TryGetProperty(
                        property.Name,
                        out JsonElement migrated),
                    $"The migrated settings lost {property.Name}.");
                Assert.True(
                    JsonElement.DeepEquals(
                        property.Value,
                        migrated),
                    $"The migrated settings changed {property.Name}.");
            }
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
    public async Task
        SchemaFourRefuelRoute_UsesFieldDefaultsOnceThenPreservesEdits()
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
                  "schema_version": 4,
                  "resource_refuel_debug": {
                    "retry_count": 4,
                    "gold_forward1_milliseconds": 1200,
                    "gold_left_milliseconds": 700,
                    "gold_forward2_milliseconds": 900,
                    "drill_forward1_milliseconds": 1200,
                    "drill_left1_milliseconds": 700,
                    "drill_forward2_milliseconds": 900,
                    "drill_left2_milliseconds": 700
                  }
                }
                """);
            AppSettingsStore store = new(paths);

            AppSettings migrated =
                await store.LoadAsync();

            Assert.Equal(
                new ResourceRefuelDebugSettings(),
                migrated.ResourceRefuelDebug);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                migrated.SchemaVersion);

            ResourceRefuelDebugSettings edited =
                migrated.ResourceRefuelDebug with
                {
                    GoldForward1Milliseconds = 4321,
                    RetryCount = 4,
                };
            await store.SaveAsync(
                migrated with
                {
                    ResourceRefuelDebug = edited,
                });

            AppSettings reloaded =
                await store.LoadAsync();

            Assert.Equal(
                edited,
                reloaded.ResourceRefuelDebug);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task IndependentPreparationChoices_PersistSeparately()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppSettingsStore store =
                new(new AppPaths(root));
            await store.SaveAsync(
                new AppSettings
                {
                    AutoCheckUiScaleOnStart = false,
                    AutoCheckGameSettingsOnStart = true,
                });

            AppSettings loaded = await store.LoadAsync();

            Assert.False(
                loaded.AutoCheckUiScaleOnStart);
            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
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
    public async Task NewSettings_StartEnabledAndAlreadyMigrated()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppSettings loaded =
                await new AppSettingsStore(
                    new AppPaths(root)).LoadAsync();

            Assert.True(
                loaded.RestartRobloxWithPrivateServer);
            Assert.True(
                loaded.RestartRobloxAtMacroStart);
            Assert.True(
                loaded.AutoCheckUiScaleOnStart);
            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
            Assert.Equal(
                AppSettings.CurrentSchemaVersion,
                loaded.SchemaVersion);
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
    public async Task RetiredDetectorUpdateSettings_AreIgnoredAndOmitted()
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
                  "check_detector_updates": true,
                  "last_detector_update_check": "2026-07-27T08:00:00+00:00"
                }
                """);
            AppSettingsStore store = new(paths);

            AppSettings loaded = await store.LoadAsync();
            await store.SaveAsync(loaded);
            string normalized =
                await File.ReadAllTextAsync(
                    paths.SettingsFile);

            Assert.DoesNotContain(
                "check_detector_updates",
                normalized,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "last_detector_update_check",
                normalized,
                StringComparison.Ordinal);
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
    public async Task LegacyAutoUpgradeToggleKey_LoadsAsPlacedUnitsBinding()
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
                  "toggle_auto_upgrade_unit_key": "K"
                }
                """);

            AppSettings loaded =
                await new AppSettingsStore(paths).LoadAsync();

            Assert.Equal(
                "K",
                loaded.ToggleAutoUpgradePlacedUnitsKey);
            Assert.Equal(
                string.Empty,
                loaded.AutoUpgradeUnitKey);
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
    public async Task Beta20Settings_PreserveLegacySelections()
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
                ManualInputRecordingEnabled = true,
                DiscordErrorUserId = "123456789012345678",
                ShiftLockVirtualKey = KeyboardKey.RightShift,
                AreasMenuKey = "G",
                CancelPlacementKey = "X",
                ChangeUnitTargetingKey = "T",
                UpgradeUnitKey = "Y",
                SellUnitKey = "S",
                AutoUpgradeUnitKey = "B",
                ToggleAutoUpgradePlacedUnitsKey = "V",
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
                loaded.ManualInputRecordingEnabled);
            Assert.True(
                loaded.AutoCheckUiScaleOnStart);
            Assert.True(
                loaded.AutoCheckGameSettingsOnStart);
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
                "S",
                loaded.SellUnitKey);
            Assert.Equal(
                "B",
                loaded.AutoUpgradeUnitKey);
            Assert.Equal(
                "V",
                loaded.ToggleAutoUpgradePlacedUnitsKey);
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

    [Fact]
    public async Task ExplicitlyUnsetGameBindingsPersistAcrossStoreRestart()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppSettingsStore firstProcess =
                new(new AppPaths(root));
            await firstProcess.SaveAsync(
                new AppSettings
                {
                    PlayMenuKey = string.Empty,
                    UnitMenuKey = string.Empty,
                    AreasMenuKey = string.Empty,
                    CancelPlacementKey = string.Empty,
                    ChangeUnitTargetingKey = string.Empty,
                    UpgradeUnitKey = string.Empty,
                    AutoUpgradeUnitKey = string.Empty,
                    ToggleAutoUpgradePlacedUnitsKey =
                        string.Empty,
                    ShiftLockVirtualKey = 0,
                });

            AppSettingsStore restartedProcess =
                new(new AppPaths(root));
            AppSettings loaded =
                await restartedProcess.LoadAsync();

            Assert.Equal(string.Empty, loaded.PlayMenuKey);
            Assert.Equal(string.Empty, loaded.UnitMenuKey);
            Assert.Equal(string.Empty, loaded.AreasMenuKey);
            Assert.Equal(
                string.Empty,
                loaded.CancelPlacementKey);
            Assert.Equal(
                string.Empty,
                loaded.ChangeUnitTargetingKey);
            Assert.Equal(string.Empty, loaded.UpgradeUnitKey);
            Assert.Equal(
                string.Empty,
                loaded.AutoUpgradeUnitKey);
            Assert.Equal(
                string.Empty,
                loaded.ToggleAutoUpgradePlacedUnitsKey);
            Assert.Equal(0, loaded.ShiftLockVirtualKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
