using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class QuickPlacementSettingsTests
{
    [Fact]
    public void QuickPlacementKey_DefaultsUnsetAndIsRequiredWhenParsed()
    {
        AppSettings settings = new();

        Assert.Equal(
            AppSettings.DefaultQuickPlacementVirtualKey,
            settings.QuickPlacementVirtualKey);
        Assert.Null(
            AppSettings.ParseOptionalQuickPlacementKey(
                settings));
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => AppSettings
                    .ParseQuickPlacementKey(
                        settings));
        Assert.Contains(
            "Controls on the Dashboard",
            error.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(KeyboardKey.LeftShift)]
    [InlineData(KeyboardKey.RightShift)]
    [InlineData(KeyboardKey.LeftControl)]
    [InlineData(KeyboardKey.RightControl)]
    [InlineData(0x51)]
    public void QuickPlacementKey_PreservesSupportedPhysicalIdentity(
        int virtualKey)
    {
        AppSettings settings = new()
        {
            QuickPlacementVirtualKey = virtualKey,
            ShiftLockVirtualKey =
                virtualKey == KeyboardKey.LeftControl
                    ? KeyboardKey.RightControl
                    : KeyboardKey.LeftControl,
        };

        Assert.Equal(
            virtualKey,
            AppSettings.ParseQuickPlacementKey(
                settings));
    }

    [Fact]
    public void QuickPlacementKey_RejectsEveryControlConflict()
    {
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseQuickPlacementKey(
                new AppSettings
                {
                    MacroHotkeyVirtualKey = 0x51,
                    QuickPlacementVirtualKey = 0x51,
                }));
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseQuickPlacementKey(
                new AppSettings
                {
                    ShiftLockVirtualKey =
                        KeyboardKey.LeftShift,
                    QuickPlacementVirtualKey =
                        KeyboardKey.LeftShift,
                }));
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseQuickPlacementKey(
                new AppSettings
                {
                    PlayMenuKey = "Q",
                    QuickPlacementVirtualKey = 0x51,
                }));
    }

    [Fact]
    public void QuickPlacementKey_RejectsUnsupportedPhysicalKey()
    {
        Assert.Throws<InvalidDataException>(
            () => AppSettings.ParseQuickPlacementKey(
                new AppSettings
                {
                    QuickPlacementVirtualKey = 0x25,
                }));
    }

    [Fact]
    public void QuickPlacementKey_RejectsUnitSlotDigits()
    {
        foreach (int virtualKey in Enumerable.Range(
                     0x30,
                     10))
        {
            Assert.Throws<InvalidDataException>(
                () => AppSettings.ParseQuickPlacementKey(
                    new AppSettings
                    {
                        QuickPlacementVirtualKey =
                            virtualKey,
                    }));
        }
    }

    [Fact]
    public void PlacementRequirements_RequireOnlyStepModeRows()
    {
        PlacementModel stepMode = Placement(
            recordingId: null,
            steps: [Step()]);
        PlacementModel recordingMode = Placement(
            recordingId: "recording-1",
            steps: [Step()]);
        PlacementModel emptyStepMode = Placement(
            recordingId: null,
            steps: []);

        Assert.True(
            PlacementControlRequirements
                .RequiresQuickPlacementKey(
                    stepMode));
        Assert.False(
            PlacementControlRequirements
                .RequiresQuickPlacementKey(
                    recordingMode));
        Assert.False(
            PlacementControlRequirements
                .RequiresQuickPlacementKey(
                    emptyStepMode));
    }

    [Fact]
    public void PlacementTestPlaybackPreflight_BlocksInvalidStepModeBeforeOperationCallback()
    {
        PlacementModel stepMode = Placement(
            recordingId: null,
            steps: [Step()]);
        int coordinatorArms = 0;
        int cameraPreparations = 0;
        int robloxInputs = 0;

        foreach (AppSettings settings in new[]
                 {
                     new AppSettings(),
                     new AppSettings
                     {
                         PlayMenuKey = "Q",
                         QuickPlacementVirtualKey = 0x51,
                     },
                 })
        {
            Assert.Throws<InvalidDataException>(
                () =>
                {
                    PlacementControlRequirements
                        .ValidateQuickPlacementForPlayback(
                            stepMode,
                            settings);
                    coordinatorArms++;
                    cameraPreparations++;
                    robloxInputs++;
                });
        }

        Assert.Equal(0, coordinatorArms);
        Assert.Equal(0, cameraPreparations);
        Assert.Equal(0, robloxInputs);
    }

    [Fact]
    public void PlacementTestPlaybackPreflight_ExemptsRecordingAndEmptyStepModes()
    {
        AppSettings invalidQuickPlacement =
            new()
            {
                PlayMenuKey = "Q",
                QuickPlacementVirtualKey = 0x51,
            };
        int operationStarts = 0;

        PlacementControlRequirements
            .ValidateQuickPlacementForPlayback(
                Placement(
                    recordingId: "recording-1",
                    steps: [Step()]),
                invalidQuickPlacement);
        operationStarts++;
        PlacementControlRequirements
            .ValidateQuickPlacementForPlayback(
                Placement(
                    recordingId: null,
                    steps: []),
                invalidQuickPlacement);
        operationStarts++;

        Assert.Equal(2, operationStarts);
    }

    [Fact]
    public async Task QuickPlacementKey_PersistsAndLegacySettingsDefaultUnset()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-quick-placement-{Guid.NewGuid():N}");
        try
        {
            AppPaths paths = new(root);
            AppSettingsStore store = new(paths);
            await store.SaveAsync(
                new AppSettings
                {
                    QuickPlacementVirtualKey =
                        KeyboardKey.LeftShift,
                });

            AppSettings reloaded =
                await store.LoadAsync();
            Assert.Equal(
                KeyboardKey.LeftShift,
                reloaded.QuickPlacementVirtualKey);
            Assert.Contains(
                "\"quick_placement_virtual_key\": 160",
                await File.ReadAllTextAsync(
                    paths.SettingsFile),
                StringComparison.Ordinal);

            await File.WriteAllTextAsync(
                paths.SettingsFile,
                """
                {
                  "schema_version": 4,
                  "theme": "dark"
                }
                """);
            AppSettings legacy =
                await store.LoadAsync();
            Assert.Equal(
                AppSettings
                    .DefaultQuickPlacementVirtualKey,
                legacy.QuickPlacementVirtualKey);
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

    private static PlacementModel Placement(
        string? recordingId,
        IReadOnlyList<PlacementStep> steps) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Test placement",
            ClientWidth = 808,
            ClientHeight = 611,
            Steps = steps,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            ManualInputRecordingId = recordingId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementStep Step() => new()
    {
        UnitKey = 1,
        X = 300,
        Y = 300,
        DelayAfterMilliseconds = 0,
    };
}
