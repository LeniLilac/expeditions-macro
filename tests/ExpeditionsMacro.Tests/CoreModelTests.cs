using System.Globalization;
using ExpeditionsMacro.Core.Diagnostics;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class CoreModelTests
{
    [Fact]
    public void AppSettings_NewInstallSafetyDefaultsAreEnabledAndMigrated()
    {
        AppSettings settings = new();

        Assert.True(
            settings.RestartRobloxWithPrivateServer);
        Assert.True(
            settings.RestartRobloxAtMacroStart);
        Assert.True(
            settings.AutoCheckUiScaleOnStart);
        Assert.True(
            settings.AutoCheckGameSettingsOnStart);
        Assert.Equal(
            AppSettings.CurrentSchemaVersion,
            settings.SchemaVersion);
    }

    [Fact]
    public void ScreenRegion_UsesHalfOpenBoundsAndTranslates()
    {
        ScreenRegion region = new(10, 20, 30, 40);

        Assert.True(region.Contains(10, 20));
        Assert.True(region.Contains(39, 59));
        Assert.False(region.Contains(40, 60));
        Assert.True(region.FitsWithin(40, 60));
        Assert.Equal(new ScreenRegion(3, 29, 30, 40), region.Translate(-7, 9));
    }

    [Fact]
    public void ClientBounds_ConvertsBetweenRelativeAndScreenCoordinates()
    {
        ClientBounds client = new(700, 250, 808, 611);
        ScreenRegion relative = new(116, 72, 259, 185);

        Assert.Equal(new ScreenRegion(816, 322, 259, 185), client.ToScreen(relative));
        Assert.Equal((116, 72), client.ToRelative(816, 322));
        Assert.Null(client.ToRelative(699, 322));
    }

    [Fact]
    public void ImageFrame_CropCopiesTheRequestedPixels()
    {
        byte[] pixels = Enumerable.Range(0, 4 * 3).Select(value => (byte)value).ToArray();
        ImageFrame frame = new(4, 3, PixelFormat.Gray8, pixels);

        ImageFrame crop = frame.Crop(new ScreenRegion(1, 1, 2, 2));

        Assert.Equal(2, crop.Width);
        Assert.Equal(2, crop.Height);
        Assert.Equal([5, 6, 9, 10], crop.Pixels);
    }

    [Fact]
    public void PlacementCaptures_CanUseRecordedOrDefaultDelays()
    {
        PlacementCapture[] captures =
        [
            new(1, 100, 200, 50, 100),
            new(2, 300, 400, 450, 500),
            new(3, 500, 550, 900, 950),
        ];

        IReadOnlyList<PlacementStep> recorded = PlacementModel.FromCaptures(captures, 125, useRecordedDelays: true);
        IReadOnlyList<PlacementStep> defaults = PlacementModel.FromCaptures(captures, 125, useRecordedDelays: false);

        Assert.Equal([350, 400, 125], recorded.Select(step => step.DelayAfterMilliseconds));
        Assert.All(defaults, step => Assert.Equal(125, step.DelayAfterMilliseconds));
    }

    [Fact]
    public void StableStateTracker_RequiresConsecutiveMatchesAndResetsOnNull()
    {
        StableStateTracker<string> tracker = new(2);

        Assert.Null(tracker.Update("reward"));
        Assert.Equal("reward", tracker.Update("reward"));
        Assert.Null(tracker.Update(null));
        Assert.Null(tracker.Update("reward"));
        Assert.Null(tracker.Update("continue"));
        Assert.Equal("continue", tracker.Update("continue"));
    }

    [Fact]
    public void AppSettings_DiagnosticCaptureOptionsDefaultOn()
    {
        AppSettings settings = new();

        Assert.True(settings.AutoCaptureOnMacroError);
        Assert.False(
            settings.ManualInputRecordingEnabled);
        Assert.True(settings.IncludeLogsInDiagnosticArchives);
    }

    [Fact]
    public void DiagnosticStateHistory_KeepsOnlyTheNewestFramesInOrder()
    {
        DiagnosticStateHistory history = new(3);
        for (int index = 1; index <= 5; index++)
        {
            history.Add(
                new ImageFrame(1, 1, PixelFormat.Gray8, [(byte)index]),
                DateTimeOffset.UnixEpoch.AddSeconds(index),
                $"action {index}");
        }

        IReadOnlyList<DiagnosticStateFrame> snapshot = history.Snapshot();

        Assert.Equal(["action 3", "action 4", "action 5"], snapshot.Select(frame => frame.Action));
        Assert.Equal([3, 4, 5], snapshot.Select(frame => (int)frame.Image.Pixels[0]));
    }

    [Fact]
    public void DiagnosticArchiveRetention_KeepsTenNewestAutomaticErrorsOnly()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            DateTime started = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
            for (int index = 0; index < 12; index++)
            {
                string timestamp = started.AddSeconds(index).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string path = Path.Combine(root, $"error-challenge-macro-{timestamp}.zip");
                File.WriteAllBytes(path, []);
                File.SetLastWriteTimeUtc(path, started.AddSeconds(index));
            }
            string manual = Path.Combine(root, "diagnostic-capture.zip");
            string similar = Path.Combine(root, "error-user-named-capture.zip");
            File.WriteAllBytes(manual, []);
            File.WriteAllBytes(similar, []);

            int removed = DiagnosticArchiveRetention.PruneAutomaticErrorArchives(root, 10);

            Assert.Equal(2, removed);
            Assert.Equal(10, Directory.EnumerateFiles(root, "error-*-macro-*.zip").Count());
            Assert.True(File.Exists(manual));
            Assert.True(File.Exists(similar));
            Assert.False(File.Exists(Path.Combine(root, "error-challenge-macro-20260721-120000.zip")));
            Assert.False(File.Exists(Path.Combine(root, "error-challenge-macro-20260721-120001.zip")));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void AppSettings_PlayMenuKeyDefaultsEmptyAndNormalizesLetters()
    {
        AppSettings settings = new();

        Assert.Equal(string.Empty, settings.PlayMenuKey);
        Assert.Equal('P', AppSettings.ParsePlayMenuKey(" p "));
    }

    [Fact]
    public void AppSettings_PlayMenuKeyIsRequiredBeforeMacroStart()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => AppSettings.ParsePlayMenuKey(string.Empty));

        Assert.Equal(
            "1. Open Settings in Anime Expeditions\n" +
            "2. Open the Keybinds section\n" +
            "3. Set Toggle Play Menu to an A-Z letter\n" +
            "4. Open the Dashboard in Expeditions Macro\n" +
            "5. Scroll down to Controls and set Toggle Play Menu key to the same letter",
            error.Message);
    }

    [Fact]
    public void AppSettings_PlayMenuKeyMustDifferFromMacroHotkey()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => AppSettings.ParsePlayMenuKey("p", 0x50));

        Assert.Contains("cannot both be P", error.Message, StringComparison.Ordinal);
        Assert.Equal('P', AppSettings.ParsePlayMenuKey("p", AppSettings.DefaultMacroHotkeyVirtualKey));
    }

    [Fact]
    public void AppSettings_UnitMenuKeyIsOptionalUntilATeamUsesIt_AndNormalizesLetters()
    {
        AppSettings settings = new();

        Assert.Equal(string.Empty, settings.UnitMenuKey);
        Assert.Equal('U', AppSettings.ParseUnitMenuKey(" u ", AppSettings.DefaultMacroHotkeyVirtualKey, "P"));
        Assert.Throws<InvalidDataException>(() => AppSettings.ParseUnitMenuKey(string.Empty, AppSettings.DefaultMacroHotkeyVirtualKey, "P"));
    }

    [Fact]
    public void AppSettings_UnitMenuKeyMustDifferFromBothControlKeys()
    {
        InvalidDataException macroConflict = Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseUnitMenuKey("u", 0x55, "P"));
        InvalidDataException playConflict = Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseUnitMenuKey("u", AppSettings.DefaultMacroHotkeyVirtualKey, "U"));

        Assert.Contains("start/stop hotkey", macroConflict.Message, StringComparison.Ordinal);
        Assert.Contains("Toggle Play Menu key", playConflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_AreasMenuKeyIsDebugOnlyAndNormalizesLetters()
    {
        AppSettings settings = new();

        Assert.Equal(string.Empty, settings.AreasMenuKey);
        Assert.Equal(
            'G',
            AppSettings.ParseAreasMenuKey(
                " g ",
                AppSettings.DefaultMacroHotkeyVirtualKey,
                "P",
                "U"));
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseAreasMenuKey(
                string.Empty,
                AppSettings.DefaultMacroHotkeyVirtualKey,
                "P",
                "U"));
    }

    [Theory]
    [InlineData("G", 0x47, "P", "U")]
    [InlineData("P", 0x75, "P", "U")]
    [InlineData("U", 0x75, "P", "U")]
    public void AppSettings_AreasMenuKeyMustDifferFromOtherControls(
        string areas,
        int macroKey,
        string play,
        string unit)
    {
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseAreasMenuKey(
                areas,
                macroKey,
                play,
                unit));
    }

    [Fact]
    public void AppSettings_PlacementCancelDefaultsToZAndRejectsConflicts()
    {
        AppSettings settings = new();

        Assert.Equal(
            "Z",
            settings.CancelPlacementKey);
        Assert.Equal(
            'X',
            AppSettings.ParseCancelPlacementKey(
                " x ",
                AppSettings.DefaultMacroHotkeyVirtualKey,
                "P",
                "H",
                "U",
                KeyboardKey.LeftControl));
        Assert.Throws<InvalidDataException>(
            () =>
                AppSettings.ParseCancelPlacementKey(
                    "P",
                    AppSettings.DefaultMacroHotkeyVirtualKey,
                    "P",
                    "H",
                    "U",
                    KeyboardKey.LeftControl));
    }

    [Fact]
    public void AppSettings_ExplicitlyUnsetGameBindingsStayOptionalUntilUsed()
    {
        AppSettings settings = new()
        {
            PlayMenuKey = string.Empty,
            UnitMenuKey = string.Empty,
            AreasMenuKey = string.Empty,
            CancelPlacementKey = string.Empty,
            ChangeUnitTargetingKey = string.Empty,
            UpgradeUnitKey = string.Empty,
            AutoUpgradeUnitKey = string.Empty,
            ToggleAutoUpgradePlacedUnitsKey = string.Empty,
            ShiftLockVirtualKey = 0,
        };

        AppSettings.ValidateControlKeySet(
            settings,
            requireUnitActionKeys: false);
        Assert.Null(
            AppSettings.ParseOptionalCancelPlacementKey(
                settings.CancelPlacementKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.UnitMenuKey,
                settings.AreasMenuKey,
                settings.ShiftLockVirtualKey));
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseCancelPlacementKey(
                settings.CancelPlacementKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.UnitMenuKey,
                settings.AreasMenuKey,
                settings.ShiftLockVirtualKey));
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseShiftLockKey(
                settings.ShiftLockVirtualKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.UnitMenuKey,
                settings.AreasMenuKey,
                settings.CancelPlacementKey));
    }

    [Fact]
    public void AppSettings_UnitActionParsersRequireOnlyTheRequestedAction()
    {
        AppSettings settings = new()
        {
            ChangeUnitTargetingKey = "t",
            AutoUpgradeUnitKey = string.Empty,
        };

        Assert.Equal(
            'T',
            AppSettings.ParseChangeUnitTargetingKey(settings));
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseAutoUpgradeUnitKey(
                settings));
    }

    [Fact]
    public void AppSettings_RequiredUnitActionKeysDefaultEmpty()
    {
        AppSettings settings = new();

        Assert.Equal(
            string.Empty,
            settings.ChangeUnitTargetingKey);
        Assert.Equal(
            string.Empty,
            settings.UpgradeUnitKey);
        Assert.Equal(
            string.Empty,
            settings.AutoUpgradeUnitKey);
        Assert.Equal(
            string.Empty,
            settings.ToggleAutoUpgradePlacedUnitsKey);
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => AppSettings
                    .ParseRequiredUnitActionKeys(
                        settings));
        Assert.Contains(
            "Change Unit Targeting",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_RequiredUnitActionKeysNormalizeDistinctLetters()
    {
        AppSettings settings = new()
        {
            PlayMenuKey = "P",
            UnitMenuKey = "H",
            AreasMenuKey = "U",
            ChangeUnitTargetingKey = " t ",
            UpgradeUnitKey = "y",
            AutoUpgradeUnitKey = "b",
            ToggleAutoUpgradePlacedUnitsKey = "g",
        };

        UnitActionKeys keys =
            AppSettings.ParseRequiredUnitActionKeys(
                settings);

        Assert.Equal('T', keys.ChangeTargeting);
        Assert.Equal('Y', keys.Upgrade);
        Assert.Equal('B', keys.AutoUpgrade);
        Assert.Equal(
            'G',
            keys.ToggleAutoUpgradePlacedUnits);
    }

    [Theory]
    [InlineData("P", "Y", "B", "G")]
    [InlineData("T", "T", "B", "G")]
    [InlineData("T", "Y", "Y", "G")]
    [InlineData("T", "Y", "B", "Z")]
    public void AppSettings_RequiredUnitActionKeysRejectEveryControlConflict(
        string targeting,
        string upgrade,
        string autoUpgrade,
        string toggleAutoUpgradePlacedUnits)
    {
        AppSettings settings = new()
        {
            PlayMenuKey = "P",
            UnitMenuKey = "H",
            AreasMenuKey = "U",
            ChangeUnitTargetingKey = targeting,
            UpgradeUnitKey = upgrade,
            AutoUpgradeUnitKey = autoUpgrade,
            ToggleAutoUpgradePlacedUnitsKey =
                toggleAutoUpgradePlacedUnits,
        };

        Assert.Throws<InvalidDataException>(
            () => AppSettings
                .ParseRequiredUnitActionKeys(
                    settings));
    }

    [Theory]
    [InlineData(0x54, 0xA2)]
    [InlineData(0x75, 0x54)]
    public void AppSettings_RequiredUnitActionKeysRejectMacroAndShiftLockConflicts(
        int macroHotkey,
        int shiftLockKey)
    {
        AppSettings settings = new()
        {
            MacroHotkeyVirtualKey = macroHotkey,
            ShiftLockVirtualKey = shiftLockKey,
            ChangeUnitTargetingKey = "T",
            UpgradeUnitKey = "Y",
            AutoUpgradeUnitKey = "B",
            ToggleAutoUpgradePlacedUnitsKey = "G",
        };

        Assert.Throws<InvalidDataException>(
            () => AppSettings
                .ParseRequiredUnitActionKeys(
                    settings));
    }

    [Fact]
    public void AppSettings_OptionalActionKeyValidationAllowsUnsetValuesButRejectsConflicts()
    {
        AppSettings.ValidateControlKeySet(
            new AppSettings
            {
                PlayMenuKey = "P",
            },
            requireUnitActionKeys: false);

        Assert.Throws<InvalidDataException>(
            () => AppSettings
                .ValidateControlKeySet(
                    new AppSettings
                    {
                        PlayMenuKey = "P",
                        ChangeUnitTargetingKey = "P",
                    },
                    requireUnitActionKeys: false));
    }

    [Fact]
    public void ResourceRefuelDebugSettings_ValidatesRouteTimingAndRetries()
    {
        ResourceRefuelDebugSettings settings = new();

        settings.Validate();
        Assert.Equal(
            [('W', 3000), ('A', 820), ('W', 2600)],
            settings.RouteFor(ResourceRefuelTarget.GoldMine));
        Assert.Equal(
            [('W', 3000), ('A', 750), ('W', 1000), ('A', 1600)],
            settings.RouteFor(ResourceRefuelTarget.ResourceDrill));
        Assert.Throws<InvalidDataException>(
            () => (settings with
            {
                GoldForward1Milliseconds = 49,
            }).Validate());
        Assert.Throws<InvalidDataException>(
            () => (settings with
            {
                RetryCount = 6,
            }).Validate());
    }

    [Fact]
    public void ModelId_IsReadableStableAndNameSensitive()
    {
        string first = ModelId.FromName("Expedition Map 1");
        string second = ModelId.FromName("Expedition Map 1");

        Assert.Equal(first, second);
        Assert.StartsWith("expedition-map-1-", first);
        Assert.NotEqual(first, ModelId.FromName("Expedition Map 2"));
    }

    [Fact]
    public void AppPaths_CreatesTheDiagnosticsFolder()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.Diagnostics));
            Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(paths.Diagnostics), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task PlacementRepository_OverwritesTheSameNamedModelAtomically()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            PlacementModelRepository repository = new(paths);
            string id = ModelId.FromName("Preplace");
            PlacementModel first = Model(id, 100);
            PlacementModel second = first with
            {
                Steps = [first.Steps[0] with { X = 200 }],
                UpdatedAt = first.UpdatedAt.AddMinutes(1),
            };

            await repository.SaveAsync(first);
            await repository.SaveAsync(second);

            PlacementModel loaded = Assert.IsType<PlacementModel>(await repository.LoadAsync(id));
            Assert.Equal(200, loaded.Steps[0].X);
            Assert.Single(await repository.ListAsync());
            Assert.Empty(Directory.EnumerateFiles(paths.PlacementModels, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PlacementModel Model(string id, int x) => new()
    {
        Id = id,
        Name = "Preplace",
        ClientWidth = 808,
        ClientHeight = 611,
        Steps = [new PlacementStep { UnitKey = 1, X = x, Y = 300, DelayAfterMilliseconds = 100 }],
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
