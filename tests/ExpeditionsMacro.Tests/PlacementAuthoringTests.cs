using System.Text.Json;
using System.Text.Json.Nodes;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementAuthoringTests
{
    [Fact]
    public void LegacyPlacementJson_DefaultsToCameraModelBeforeStart()
    {
        PlacementModel current = Placement(
            CameraPreparationMode.CameraModel,
            target: null,
            Step(PlacementPhase.BeforeStart, 1));
        JsonObject json = SerializeObject(current);
        json.Remove("camera_preparation_mode");
        json.Remove("target");
        json.Remove("placement_interval_milliseconds");
        json.Remove("placement_attempts");
        json.Remove(
            "default_after_start_delay_milliseconds");
        json["steps"]!.AsArray()[0]!
            .AsObject()
            .Remove("phase");
        json["steps"]!.AsArray()[0]!
            .AsObject()
            .Remove("delay_after_start_milliseconds");
        json["steps"]!.AsArray()[0]!
            .AsObject()
            .Remove("auto_upgrade");

        PlacementModel legacy = Deserialize<PlacementModel>(json);

        Assert.Equal(
            CameraPreparationMode.CameraModel,
            legacy.CameraPreparationMode);
        Assert.Null(legacy.Target);
        Assert.Equal(
            PlacementPhase.BeforeStart,
            Assert.Single(legacy.Steps).Phase);
        Assert.Equal(
            0,
            Assert.Single(legacy.Steps)
                .DelayAfterStartMilliseconds);
        Assert.Equal(
            PlacementAuthoringRules
                .DefaultStepDelayMilliseconds,
            legacy.PlacementIntervalMilliseconds);
        Assert.Equal(
            PlacementAuthoringRules
                .DefaultAfterStartDelayMilliseconds,
            legacy.DefaultAfterStartDelayMilliseconds);
        Assert.Equal(
            PlacementModel.DefaultPlacementAttempts,
            legacy.PlacementAttempts);
        Assert.Equal(
            UnitAutoUpgradePriority.Off,
            Assert.Single(legacy.Steps)
                .AutoUpgradePriority);
        legacy.Validate();
    }

    [Theory]
    [InlineData(
        false,
        UnitAutoUpgradePriority.Off,
        "off")]
    [InlineData(
        true,
        UnitAutoUpgradePriority.Priority1,
        "priority_1")]
    public void LegacyAutoUpgradeBoolean_LoadsAndNormalizesOnSave(
        bool legacyValue,
        UnitAutoUpgradePriority expected,
        string normalized)
    {
        PlacementModel current = Placement(
            CameraPreparationMode.CameraModel,
            target: null,
            Step(PlacementPhase.BeforeStart, 1));
        JsonObject json = SerializeObject(current);
        json["steps"]!.AsArray()[0]!
            .AsObject()["auto_upgrade"] =
                legacyValue;

        PlacementModel legacy =
            Deserialize<PlacementModel>(json);
        JsonObject normalizedJson =
            SerializeObject(legacy);

        Assert.Equal(
            expected,
            Assert.Single(legacy.Steps)
                .AutoUpgradePriority);
        Assert.Equal(
            normalized,
            normalizedJson["steps"]!
                .AsArray()[0]!
                .AsObject()["auto_upgrade"]!
                .GetValue<string>());
        legacy.Validate();
    }

    [Fact]
    public void PlacementStep_RejectsUnknownAutoUpgradePriority()
    {
        PlacementStep invalid =
            Step(
                PlacementPhase.BeforeStart,
                1) with
            {
                AutoUpgradePriority =
                    (UnitAutoUpgradePriority)7,
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => invalid.Validate(808, 611));

        Assert.Contains(
            "Auto Upgrade priority",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyPresetJson_DefaultsToCameraModel()
    {
        ExpeditionPreset expedition = DeserializeWithoutMode(
            new ExpeditionPreset
            {
                Id = "expedition",
                Name = "Expedition",
                CameraModelId = "camera",
                PlacementModelId = "placement",
            });
        ChallengePreset challenge = DeserializeWithoutMode(
            new ChallengePreset
            {
                Id = "challenge",
                Name = "Challenge",
                Maps = ChallengePreset.EmptyMapProfiles(),
            });
        StoryPreset story = DeserializeWithoutMode(
            new StoryPreset
            {
                Id = "story",
                Name = "Story",
            });
        RaidPreset raid = DeserializeWithoutMode(
            new RaidPreset
            {
                Id = "raid",
                Name = "Raid",
            });

        Assert.Equal(
            CameraPreparationMode.CameraModel,
            expedition.CameraPreparationMode);
        Assert.Equal(
            CameraPreparationMode.CameraModel,
            challenge.CameraPreparationMode);
        Assert.Equal(
            CameraPreparationMode.CameraModel,
            story.CameraPreparationMode);
        Assert.Equal(
            CameraPreparationMode.CameraModel,
            raid.CameraPreparationMode);
    }

    [Fact]
    public void FastPlacement_RequiresAnExactRoute()
    {
        PlacementTarget schoolStory = new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = 1,
            StoryRunKind = StoryRunKind.Act,
            ActNumber = 3,
        };
        PlacementModel model = Placement(
            CameraPreparationMode.FastNoAlign,
            schoolStory,
            Step(PlacementPhase.BeforeStart, 1));

        model.ValidateCompatibility(
            CameraPreparationMode.FastNoAlign,
            schoolStory);

        PlacementTarget otherAct =
            schoolStory with { ActNumber = 4 };
        Assert.Throws<InvalidDataException>(
            () => model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                otherAct));
        Assert.Throws<InvalidDataException>(
            () => model.ValidateCompatibility(
                CameraPreparationMode.CameraModel,
                schoolStory));
    }

    [Fact]
    public void NonActStoryTarget_UsesTheSharedVariantIdentity()
    {
        StoryPreset mastery = new()
        {
            Id = "mastery",
            Name = "Mastery",
            Map = ChallengeMapId.FlowerForest,
            RunKind = StoryRunKind.Mastery,
            ActNumber = 5,
        };

        PlacementTarget target =
            PlacementTarget.ForStory(mastery);

        Assert.Equal(1, target.ActNumber);
        target.Validate();
        Assert.Throws<InvalidDataException>(
            () => (target with { ActNumber = 2 }).Validate());
    }

    [Fact]
    public void ExecutionPlan_SplitsFastPlacementPhases()
    {
        PlacementStep before =
            Step(PlacementPhase.BeforeStart, 1) with
            {
                PlacementId = "before",
            };
        PlacementStep after =
            Step(PlacementPhase.AfterStart, 2) with
            {
                PlacementId = "after",
            };
        PlacementModel fast = Placement(
            CameraPreparationMode.FastNoAlign,
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Raid,
                MapNumber = 1,
                ActNumber = 2,
            },
            before,
            after);
        Assert.Equal(
            [before],
            PlacementExecutionPlan.BeforeStart(
                fast));
        Assert.Equal(
            [after],
            PlacementExecutionPlan.AfterStart(
                fast));
    }

    [Fact]
    public void AfterStartSchedule_UsesEachPlacementOffset()
    {
        PlacementStep step =
            Step(PlacementPhase.AfterStart, 2) with
            {
                DelayAfterStartMilliseconds = 5000,
            };

        Assert.False(
            PlacementExecutionPlan.IsAfterStartDue(
                step,
                TimeSpan.FromMilliseconds(4999)));
        Assert.True(
            PlacementExecutionPlan.IsAfterStartDue(
                step,
                TimeSpan.FromSeconds(5)));
        Assert.True(
            PlacementExecutionPlan.IsAfterStartDue(
                step,
                TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void AfterStartSchedule_OrdersIndependentOffsets()
    {
        PlacementStep late =
            Step(PlacementPhase.AfterStart, 1) with
            {
                PlacementId = "late",
                DelayAfterStartMilliseconds = 9000,
            };
        PlacementStep early =
            Step(PlacementPhase.AfterStart, 2) with
            {
                PlacementId = "early",
                DelayAfterStartMilliseconds = 2000,
            };
        PlacementStep sameTime =
            Step(PlacementPhase.AfterStart, 3) with
            {
                PlacementId = "same-time",
                DelayAfterStartMilliseconds = 2000,
            };
        PlacementModel model = Placement(
            CameraPreparationMode.FastNoAlign,
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Raid,
                MapNumber = 1,
                ActNumber = 1,
            },
            late,
            early,
            sameTime);

        IReadOnlyList<PlacementStep> schedule =
            PlacementExecutionPlan.AfterStart(
                model);

        Assert.Equal(
            [early, sameTime, late],
            schedule);
    }

    [Fact]
    public void FastPlacement_RequiresSevenPixelSpacing()
    {
        PlacementModel tooClose = Placement(
            CameraPreparationMode.FastNoAlign,
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 1,
                ActNumber = 0,
            },
            Step(PlacementPhase.BeforeStart, 1) with
            {
                X = 100,
                Y = 100,
            },
            Step(PlacementPhase.AfterStart, 2) with
            {
                X = 106,
                Y = 100,
            });
        PlacementModel allowed =
            tooClose with
            {
                Steps =
                [
                    tooClose.Steps[0],
                    tooClose.Steps[1] with
                    {
                        X = 107,
                    },
                ],
            };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                tooClose.Validate);
        Assert.Contains(
            "at least 7 client pixels",
            error.Message,
            StringComparison.Ordinal);
        allowed.Validate();
    }

    [Fact]
    public void FastPlacement_DefaultTimingUsesThirtySecondAfterStartDelay()
    {
        PlacementModel model = Placement(
            CameraPreparationMode.FastNoAlign,
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Raid,
                MapNumber = 1,
                ActNumber = 1,
            },
            Step(PlacementPhase.BeforeStart, 1));

        Assert.Equal(
            900,
            PlacementAuthoringRules
                .DefaultStepDelayMilliseconds);
        Assert.Equal(
            30_000,
            PlacementAuthoringRules
                .DefaultAfterStartDelayMilliseconds);
        Assert.Equal(
            900,
            model.PlacementIntervalMilliseconds);
        Assert.Equal(
            30_000,
            model.DefaultAfterStartDelayMilliseconds);
        Assert.Equal(
            1,
            model.PlacementAttempts);
    }

    [Fact]
    public void PlacementSetup_RejectsNegativeTimingDefaults()
    {
        PlacementModel model = Placement(
            CameraPreparationMode.FastNoAlign,
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Raid,
                MapNumber = 1,
                ActNumber = 1,
            },
            Step(PlacementPhase.BeforeStart, 1));

        InvalidDataException intervalError =
            Assert.Throws<InvalidDataException>(
                () => (model with
                {
                    PlacementIntervalMilliseconds = -1,
                }).Validate());
        InvalidDataException afterStartError =
            Assert.Throws<InvalidDataException>(
                () => (model with
                {
                    DefaultAfterStartDelayMilliseconds = -1,
                }).Validate());
        InvalidDataException attemptsError =
            Assert.Throws<InvalidDataException>(
                () => (model with
                {
                    PlacementAttempts = 0,
                }).Validate());

        Assert.Contains(
            "Placement interval",
            intervalError.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Default After Start delay",
            afterStartError.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Placement attempts",
            attemptsError.Message,
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(
            () => (model with
            {
                PlacementAttempts =
                    PlacementModel
                        .MaximumPlacementAttempts +
                    1,
            }).Validate());
    }

    [Fact]
    public void AuthoringOrder_GroupsPhasesAndPreservesTheirStepOrder()
    {
        PlacementStep afterFirst =
            Step(PlacementPhase.AfterStart, 1);
        PlacementStep beforeFirst =
            Step(PlacementPhase.BeforeStart, 2);
        PlacementStep afterSecond =
            Step(PlacementPhase.AfterStart, 3);
        PlacementStep beforeSecond =
            Step(PlacementPhase.BeforeStart, 4);

        IReadOnlyList<PlacementStep> ordered =
            PlacementAuthoringRules
                .OrderForAuthoring(
                [
                    afterFirst,
                    beforeFirst,
                    afterSecond,
                    beforeSecond,
                ]);

        Assert.Equal(
            [
                beforeFirst,
                beforeSecond,
                afterFirst,
                afterSecond,
            ],
            ordered);
    }

    [Fact]
    public void FastPlacement_SharedExpeditionSetupCoversEveryExpeditionMap()
    {
        PlacementTarget shared = new()
        {
            Mode = PlacementTargetMode.Expedition,
            MapNumber =
                PlacementSetupCatalog.SharedExpeditionMapNumber,
            ActNumber = 0,
        };
        PlacementModel model = Placement(
            CameraPreparationMode.FastNoAlign,
            shared,
            Step(PlacementPhase.BeforeStart, 1));

        for (int map = 1; map <= 3; map++)
        {
            model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                shared with { MapNumber = map });
        }

        Assert.Throws<InvalidDataException>(
            () => model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Challenge,
                    MapNumber = 1,
                    ActNumber = 0,
                }));
    }

    [Fact]
    public void FastPlacement_SharedStoryMapCoversStoryAndChallengeOnlyOnThatMap()
    {
        PlacementTarget shared = new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = 2,
            StoryRunKind = StoryRunKind.Act,
            ActNumber =
                PlacementSetupCatalog.SharedStoryActNumber,
        };
        PlacementModel model = Placement(
            CameraPreparationMode.FastNoAlign,
            shared,
            Step(PlacementPhase.BeforeStart, 1));

        foreach (PlacementSetupRoute route in
                 PlacementSetupCatalog.All.Where(
                     route =>
                         route.Target.MapNumber == 2 &&
                         route.Target.Mode is
                             PlacementTargetMode.Story or
                             PlacementTargetMode.Challenge &&
                         !PlacementSetupCatalog
                             .IsSharedStoryTarget(
                                 route.Target)))
        {
            model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                route.Target);
        }

        Assert.Throws<InvalidDataException>(
            () => model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Story,
                    MapNumber = 3,
                    StoryRunKind = StoryRunKind.Act,
                    ActNumber = 1,
                }));
        Assert.Throws<InvalidDataException>(
            () => model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Raid,
                    MapNumber = 1,
                    ActNumber = 2,
                }));
    }

    [Fact]
    public void PlacementSetupCatalog_PrefersExactRouteBeforeSharedStoryMap()
    {
        PlacementTarget exact = new()
        {
            Mode = PlacementTargetMode.Challenge,
            MapNumber = 4,
        };

        PlacementSetupRoute[] candidates =
            PlacementSetupCatalog.CandidatesFor(exact)
                .ToArray();

        Assert.Equal(2, candidates.Length);
        Assert.True(
            candidates[0].Target.Matches(exact));
        Assert.True(
            PlacementSetupCatalog.IsSharedStoryTarget(
                candidates[1].Target));
        Assert.Equal(4, candidates[1].Target.MapNumber);
    }

    [Fact]
    public void PlacementSetupCatalog_CoversEverySupportedFastRoute()
    {
        IReadOnlyList<PlacementSetupRoute> routes =
            PlacementSetupCatalog.All;

        Assert.Equal(57, routes.Count);
        Assert.Equal(
            routes.Count,
            routes.Select(route => route.ModelId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            routes.Count,
            routes.Select(route => route.Target)
                .Distinct()
                .Count());
        Assert.Equal(
            4,
            routes.Count(route =>
                route.Target.Mode ==
                PlacementTargetMode.Expedition));
        Assert.Equal(
            5,
            routes.Count(route =>
                route.Target.Mode ==
                PlacementTargetMode.Challenge));
        Assert.Equal(
            40,
            routes.Count(route =>
                route.Target.Mode ==
                PlacementTargetMode.Story));
        Assert.Equal(
            5,
            routes.Count(route =>
                PlacementSetupCatalog
                    .IsSharedStoryTarget(
                        route.Target)));
        Assert.Equal(
            3,
            routes.Count(route =>
                route.Target.Mode ==
                PlacementTargetMode.Raid));
        Assert.Equal(
            5,
            routes.Count(route =>
                route.Target.Mode ==
                PlacementTargetMode.Event));
    }

    [Fact]
    public void EventActOneAngles_AreDistinctPlacementRoutes()
    {
        PlacementTarget angleOne = new()
        {
            Mode = PlacementTargetMode.Event,
            MapNumber =
                (int)EventModeId.VillainInvasion,
            ActNumber = (int)EventAct.Act1,
            SpawnRoute = EventSpawnRoute.Angle1,
        };
        PlacementTarget angleTwo = angleOne with
        {
            SpawnRoute = EventSpawnRoute.Angle2,
        };

        Assert.False(angleOne.Matches(angleTwo));
        Assert.NotEqual(
            PlacementSetupCatalog.IdFor(angleOne),
            PlacementSetupCatalog.IdFor(angleTwo));
        Assert.Contains(
            "Angle 2",
            PlacementSetupCatalog.NameFor(angleTwo),
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyEventTarget_DefaultsToActOneAngleOne()
    {
        PlacementTarget current = new()
        {
            Mode = PlacementTargetMode.Event,
            MapNumber =
                (int)EventModeId.VillainInvasion,
            ActNumber = (int)EventAct.Act1,
            SpawnRoute = EventSpawnRoute.Angle2,
        };
        JsonObject json = SerializeObject(current);
        json.Remove("spawn_route");

        PlacementTarget legacy =
            Deserialize<PlacementTarget>(json);

        Assert.Equal(
            EventSpawnRoute.Angle1,
            legacy.SpawnRoute);
        legacy.Validate();
    }

    private static PlacementModel Placement(
        CameraPreparationMode mode,
        PlacementTarget? target,
        params PlacementStep[] steps) =>
        new()
        {
            Id = $"placement-{Guid.NewGuid():N}",
            Name = "Placement",
            ClientWidth = 808,
            ClientHeight = 611,
            Steps = steps,
            CameraPreparationMode = mode,
            Target = target,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementStep Step(
        PlacementPhase phase,
        int unit) =>
        new()
        {
            UnitKey = unit,
            X = 320 + unit * 20,
            Y = 280 + unit * 20,
            DelayAfterMilliseconds = 900,
            Phase = phase,
        };

    private static T DeserializeWithoutMode<T>(T value)
    {
        JsonObject json = SerializeObject(value);
        json.Remove("camera_preparation_mode");
        return Deserialize<T>(json);
    }

    private static JsonObject SerializeObject<T>(T value) =>
        JsonNode.Parse(
            JsonSerializer.Serialize(
                value,
                JsonFileStore.Options))!
            .AsObject();

    private static T Deserialize<T>(JsonObject value) =>
        JsonSerializer.Deserialize<T>(
            value.ToJsonString(),
            JsonFileStore.Options) ??
        throw new InvalidDataException(
            $"Could not deserialize {typeof(T).Name}.");
}
