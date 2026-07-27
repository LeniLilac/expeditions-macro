using System.Text.Json;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class FastNoAlignPresetShareTests
{
    [Fact]
    public async Task ExportImport_RoundTripsReferencedPresetsAndCompletePlacements()
    {
        string sourceRoot =
            TestPaths.NewTemporaryDirectory();
        string destinationRoot =
            TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths sourcePaths = new(sourceRoot);
            (
                MacroPlan plan,
                IReadOnlyList<PlacementModel> setups,
                ExpeditionPreset expedition,
                ChallengePreset challenge,
                StoryPreset story,
                RaidPreset raid) =
                BuildPresetPlan();
            PlacementModelRepository placements =
                new(sourcePaths);
            foreach (PlacementModel setup in setups)
            {
                await placements.SaveAsync(setup);
            }
            await new PresetRepository(sourcePaths)
                .SaveAsync(expedition);
            await new ChallengePresetRepository(sourcePaths)
                .SaveAsync(challenge);
            await new StoryPresetRepository(sourcePaths)
                .SaveAsync(story);
            await new RaidPresetRepository(sourcePaths)
                .SaveAsync(raid);

            FastNoAlignShareService exporter =
                Service(sourceRoot);
            FastNoAlignShareBundle bundle =
                exporter.Read(
                    await exporter.ExportAsync(plan));

            Assert.Equal(
                FastNoAlignShareBundle
                    .CurrentSchemaVersion,
                bundle.SchemaVersion);
            Assert.Single(bundle.ExpeditionPresets);
            Assert.Single(bundle.ChallengePresets);
            Assert.Single(bundle.StoryPresets);
            Assert.Single(bundle.RaidPresets);
            Assert.Equal(8, bundle.PlacementSetups.Count);
            Assert.Null(
                bundle.LegacyManualInputRecordings);

            PlacementModel exported =
                Assert.Single(
                    bundle.PlacementSetups,
                    setup =>
                        setup.Id ==
                        expedition.PlacementModelId);
            AssertCompletePlacement(exported);

            string json = JsonSerializer.Serialize(
                bundle,
                JsonFileStore.Options);
            Assert.DoesNotContain(
                "manual_input_recordings",
                json,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "webhook",
                json,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "private_server",
                json,
                StringComparison.OrdinalIgnoreCase);

            FastNoAlignShareService importer =
                Service(destinationRoot);
            await importer.ImportAsync(bundle);
            AppPaths destinationPaths =
                new(destinationRoot);

            Assert.NotNull(
                await new PresetRepository(
                        destinationPaths)
                    .LoadAsync(expedition.Id));
            Assert.NotNull(
                await new ChallengePresetRepository(
                        destinationPaths)
                    .LoadAsync(challenge.Id));
            Assert.NotNull(
                await new StoryPresetRepository(
                        destinationPaths)
                    .LoadAsync(story.Id));
            Assert.NotNull(
                await new RaidPresetRepository(
                        destinationPaths)
                    .LoadAsync(raid.Id));
            PlacementModel imported =
                await new PlacementModelRepository(
                        destinationPaths)
                    .LoadAsync(
                        expedition
                            .PlacementModelId)
                ?? throw new InvalidOperationException();
            AssertCompletePlacement(imported);
            MacroPlan importedPlan =
                await new MacroPlanRepository(
                        destinationPaths)
                    .LoadAsync(plan.Id)
                ?? throw new InvalidOperationException();
            Assert.Equal(4, importedPlan.Tasks.Count);
            Assert.Equal(2, importedPlan.Loops.Count);
            Assert.Empty(importedPlan.Progress);
            Assert.Empty(importedPlan.LoopStates);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                sourceRoot);
            TestPaths.DeleteTemporaryDirectory(
                destinationRoot);
        }
    }

    [Fact]
    public void Bundle_RejectsManualRecordingPayload()
    {
        PlacementTarget target =
            ExpeditionTarget(1);
        FastNoAlignShareBundle bundle = new()
        {
            Plan = DirectPlan(target),
            PlacementSetups =
            [
                Setup(
                    PlacementSetupCatalog
                        .IdFor(target),
                    target),
            ],
            LegacyManualInputRecordings =
            [
                Recording(),
            ],
        };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                bundle.Validate);

        Assert.Contains(
            "cannot contain manual input",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bundle_RejectsUnreferencedPreset()
    {
        PlacementTarget target =
            ExpeditionTarget(1);
        FastNoAlignShareBundle bundle = new()
        {
            Plan = DirectPlan(target),
            PlacementSetups =
            [
                Setup(
                    PlacementSetupCatalog
                        .IdFor(target),
                    target),
            ],
            ExpeditionPresets =
            [
                new ExpeditionPreset
                {
                    Id = "unused-preset",
                    Name = "Unused",
                    MapNumber = 1,
                    CameraPreparationMode =
                        CameraPreparationMode
                            .FastNoAlign,
                    PlacementModelId =
                        "unused-placement",
                },
            ],
        };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                bundle.Validate);

        Assert.Contains(
            "exactly the Expedition presets",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_RejectsCameraModelPreset()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            ExpeditionPreset preset = new()
            {
                Id = "camera-preset",
                Name = "Camera preset",
                MapNumber = 1,
                CameraPreparationMode =
                    CameraPreparationMode.CameraModel,
                CameraModelId = "camera-model",
                PlacementModelId =
                    "camera-placement",
            };
            PlacementModel placement =
                Setup(
                    preset.PlacementModelId,
                    ExpeditionTarget(1)) with
                {
                    CameraPreparationMode =
                        CameraPreparationMode
                            .CameraModel,
                    Target = null,
                };
            await new PresetRepository(paths)
                .SaveAsync(preset);
            await new PlacementModelRepository(paths)
                .SaveAsync(placement);
            MacroPlan plan = PresetPlan(
                MacroTaskKind.Expedition,
                preset.Id);

            InvalidDataException error =
                await Assert.ThrowsAsync<
                    InvalidDataException>(
                    () => Service(root)
                        .ExportAsync(plan));

            Assert.Contains(
                "Switch the preset to Fast no align",
                error.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static (
        MacroPlan Plan,
        IReadOnlyList<PlacementModel> Setups,
        ExpeditionPreset Expedition,
        ChallengePreset Challenge,
        StoryPreset Story,
        RaidPreset Raid) BuildPresetPlan()
    {
        ExpeditionPreset expedition = new()
        {
            Id = "expedition-preset",
            Name = "Expedition preset",
            MapNumber = 2,
            Difficulty = 3,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PlacementModelId =
                "legacy-expedition-placement",
            TeamSlot = 7,
        };
        ChallengePreset challenge = new()
        {
            Id = "challenge-preset",
            Name = "Challenge preset",
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Maps = ChallengePreset
                .EmptyMapProfiles()
                .Select(profile => profile with
                {
                    PrestartPlacementModelId =
                        $"legacy-challenge-{(int)profile.Map}",
                    TeamSlot = (int)profile.Map,
                })
                .ToArray(),
        };
        StoryPreset story = new()
        {
            Id = "story-preset",
            Name = "Story preset",
            Map = ChallengeMapId.RoseKingdom,
            RunKind = StoryRunKind.Infinite,
            ActNumber = 1,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PrestartPlacementModelId =
                "legacy-story-placement",
            TeamSlot = 6,
        };
        RaidPreset raid = new()
        {
            Id = "raid-preset",
            Name = "Raid preset",
            Act = RaidAct.Act2,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PrestartPlacementModelId =
                "legacy-raid-placement",
            TeamSlot = 8,
        };

        List<PlacementModel> setups =
        [
            Setup(
                expedition.PlacementModelId,
                PlacementTarget.ForExpedition(
                    expedition)),
            .. challenge.Maps.Select(map =>
                Setup(
                    map.PrestartPlacementModelId,
                    PlacementTarget.ForChallenge(
                        map.Map))),
            Setup(
                story.PrestartPlacementModelId,
                PlacementTarget.ForStory(story)),
            Setup(
                raid.PrestartPlacementModelId,
                PlacementTarget.ForRaid(raid)),
        ];
        MacroPlan plan = new()
        {
            Id = "preset-plan",
            Name = "Preset plan",
            Tasks =
            [
                Task(
                    "expedition-task",
                    MacroTaskKind.Expedition,
                    expedition.Id),
                Task(
                    "challenge-task",
                    MacroTaskKind.Challenge,
                    challenge.Id),
                Task(
                    "story-task",
                    MacroTaskKind.Story,
                    story.Id),
                Task(
                    "raid-task",
                    MacroTaskKind.Raid,
                    raid.Id),
            ],
            Loops =
            [
                new MacroPlanLoopDefinition
                {
                    StartTaskId = "expedition-task",
                    StopTaskId = "story-task",
                    TotalRuns = 3,
                },
                new MacroPlanLoopDefinition
                {
                    StartTaskId = "expedition-task",
                    StopTaskId = "raid-task",
                    Forever = true,
                },
            ],
        };
        return (
            plan,
            setups,
            expedition,
            challenge,
            story,
            raid);
    }

    private static void AssertCompletePlacement(
        PlacementModel setup)
    {
        Assert.Equal(8, setup.TeamSlot);
        Assert.Equal(731,
            setup.PlacementIntervalMilliseconds);
        Assert.Equal(
            34_567,
            setup
                .DefaultAfterStartDelayMilliseconds);
        Assert.Equal(
            17,
            setup.ImpossibilityThresholdMinutes);
        Assert.Null(setup.ManualInputRecordingId);
        Assert.NotNull(setup.Target);
        Assert.Equal(2, setup.Steps.Count);
        PlacementStep before = setup.Steps[0];
        Assert.Equal(2, before.UnitKey);
        Assert.Equal(101, before.X);
        Assert.Equal(102, before.Y);
        Assert.Equal(1_234,
            before.DelayAfterMilliseconds);
        Assert.Equal(
            PlacementPhase.BeforeStart,
            before.Phase);
        Assert.Equal(
            UnitTargetingPriority.Strongest,
            before.TargetingPriority);
        Assert.Equal(
            UnitAutoUpgradePriority.Off,
            before.AutoUpgradePriority);
        PlacementStep after = setup.Steps[1];
        Assert.Equal(4, after.UnitKey);
        Assert.Equal(607, after.X);
        Assert.Equal(463, after.Y);
        Assert.Equal(
            PlacementPhase.AfterStart,
            after.Phase);
        Assert.Equal(
            45_678,
            after.DelayAfterStartMilliseconds);
        Assert.Equal(
            UnitAutoUpgradePriority.Priority6,
            after.AutoUpgradePriority);
    }

    private static PlacementModel Setup(
        string id,
        PlacementTarget target) =>
        new()
        {
            Id = id,
            Name = $"Setup {id}",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = target,
            TeamSlot = 8,
            PlacementIntervalMilliseconds = 731,
            DefaultAfterStartDelayMilliseconds =
                34_567,
            ImpossibilityThresholdMinutes = 17,
            Steps =
            [
                new PlacementStep
                {
                    UnitKey = 2,
                    X = 101,
                    Y = 102,
                    DelayAfterMilliseconds = 1_234,
                    Phase = PlacementPhase.BeforeStart,
                    TargetingPriority =
                        UnitTargetingPriority.Strongest,
                    AutoUpgradePriority =
                        UnitAutoUpgradePriority.Off,
                },
                new PlacementStep
                {
                    UnitKey = 4,
                    X = 607,
                    Y = 463,
                    DelayAfterMilliseconds = 2_345,
                    Phase = PlacementPhase.AfterStart,
                    DelayAfterStartMilliseconds =
                        45_678,
                    TargetingPriority =
                        UnitTargetingPriority.First,
                    AutoUpgradePriority =
                        UnitAutoUpgradePriority
                            .Priority6,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static MacroPlan DirectPlan(
        PlacementTarget target) =>
        new()
        {
            Id = "direct-plan",
            Name = "Direct plan",
            Tasks =
            [
                new MacroTaskDefinition
                {
                    Id = "direct-task",
                    Kind = MacroTaskKind.Expedition,
                    Name = "Direct",
                    PlacementTarget = target,
                },
            ],
        };

    private static MacroPlan PresetPlan(
        MacroTaskKind kind,
        string presetId) =>
        new()
        {
            Id = "preset-plan",
            Name = "Preset plan",
            Tasks =
            [
                Task("preset-task", kind, presetId),
            ],
        };

    private static MacroTaskDefinition Task(
        string id,
        MacroTaskKind kind,
        string presetId) =>
        new()
        {
            Id = id,
            Kind = kind,
            PresetId = presetId,
            Name = id,
        };

    private static PlacementTarget
        ExpeditionTarget(int map) =>
        new()
        {
            Mode = PlacementTargetMode.Expedition,
            MapNumber = map,
        };

    private static ManualInputRecording Recording() =>
        new()
        {
            Id = "do-not-share",
            Name = "Do not share",
            InitialClientX = 100,
            InitialClientY = 100,
            DurationMicroseconds = 10,
            Events =
            [
                new ManualInputEvent
                {
                    OffsetMicroseconds = 10,
                    Kind =
                        ManualInputEventKind
                            .MouseMove,
                    ClientX = 101,
                    ClientY = 101,
                },
            ],
        };

    private static FastNoAlignShareService Service(
        string root)
    {
        AppPaths paths = new(root);
        return new FastNoAlignShareService(
            new MacroPlanRepository(paths),
            new PlacementModelRepository(paths),
            new PresetRepository(paths),
            new ChallengePresetRepository(paths),
            new StoryPresetRepository(paths),
            new RaidPresetRepository(paths));
    }
}
