using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class FastNoAlignShareTests
{
    [Fact]
    public async Task ExportImport_RoundTripsPlanSetupsAndTeamsWithoutProgress()
    {
        string sourceRoot =
            TestPaths.NewTemporaryDirectory();
        string destinationRoot =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementTarget target = new()
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 2,
            };
            PlacementModel setup =
                Setup(target, team: 7) with
                {
                    PlacementIntervalMilliseconds = 1250,
                    DefaultAfterStartDelayMilliseconds =
                        42_000,
                };
            MacroPlanLoopDefinition foreverLoop = new()
            {
                StartTaskId = "expedition-task",
                StopTaskId = "expedition-task",
                Forever = true,
            };
            MacroPlanLoopDefinition finiteLoop = new()
            {
                StartTaskId = "expedition-task",
                StopTaskId = "expedition-task",
                TotalRuns = 4,
            };
            MacroPlan plan = Plan(
                new MacroTaskDefinition
                {
                    Id = "expedition-task",
                    Kind = MacroTaskKind.Expedition,
                    Name = "Flower Forest",
                    PlacementTarget = target,
                    TargetVictories = 10,
                    Difficulty = 3,
                    ExtractAtCheckpoint = true,
                    BossesBeforeExtract = 2,
                }) with
            {
                Progress =
                [
                    new MacroTaskProgress
                    {
                        TaskId = "expedition-task",
                        Victories = 6,
                        Defeats = 1,
                        RuntimeSeconds = 720,
                    },
                ],
                Loops =
                [
                    foreverLoop,
                    finiteLoop,
                ],
                LoopStates =
                [
                    new MacroPlanLoopProgress
                    {
                        ConfigurationSignature =
                            finiteLoop
                                .ConfigurationSignature,
                        Phase =
                            MacroPlanLoopPhase.Loop,
                        CompletedRuns = 2,
                    },
                ],
            };

            FastNoAlignShareService exporter =
                Service(sourceRoot);
            AppPaths sourcePaths =
                new(sourceRoot);
            await new PlacementModelRepository(
                    sourcePaths)
                .SaveAsync(setup);
            await new MacroPlanRepository(sourcePaths)
                .SaveAsync(plan);

            string code =
                await exporter.ExportAsync(plan);
            Assert.StartsWith(
                FastNoAlignShareCodec.Prefix,
                code,
                StringComparison.Ordinal);

            FastNoAlignShareBundle bundle =
                exporter.Read(code);
            Assert.Empty(bundle.Plan.Progress);
            Assert.True(
                bundle.Plan.LoopProgress.IsEmpty);
            Assert.Empty(bundle.Plan.LoopStates);
            Assert.Equal(
                4,
                Assert.Single(
                    bundle.Plan.Loops,
                    loop => !loop.Forever)
                    .TotalRuns);
            Assert.Single(
                bundle.Plan.Loops,
                loop => loop.Forever);
            Assert.Equal(10,
                bundle.Plan.Tasks[0].TargetVictories);
            Assert.Equal(3,
                bundle.Plan.Tasks[0].Difficulty);
            Assert.Equal(2,
                bundle.Plan.Tasks[0]
                    .BossesBeforeExtract);
            Assert.Equal(7,
                Assert.Single(
                    bundle.PlacementSetups)
                    .TeamSlot);
            Assert.Equal(
                1250,
                Assert.Single(
                    bundle.PlacementSetups)
                    .PlacementIntervalMilliseconds);
            Assert.Equal(
                42_000,
                Assert.Single(
                    bundle.PlacementSetups)
                    .DefaultAfterStartDelayMilliseconds);
            Assert.Equal(
                UnitTargetingPriority.Strongest,
                Assert.Single(
                    Assert.Single(
                        bundle.PlacementSetups)
                        .Steps)
                    .TargetingPriority);

            FastNoAlignShareService importer =
                Service(destinationRoot);
            await importer.ImportAsync(bundle);
            AppPaths destinationPaths =
                new(destinationRoot);
            MacroPlan importedPlan =
                await new MacroPlanRepository(
                        destinationPaths)
                    .LoadAsync(plan.Id)
                ?? throw new InvalidOperationException();
            PlacementModel importedSetup =
                await new PlacementModelRepository(
                        destinationPaths)
                    .LoadAsync(setup.Id)
                ?? throw new InvalidOperationException();
            Assert.Empty(importedPlan.Progress);
            Assert.True(
                importedPlan.LoopProgress.IsEmpty);
            Assert.Empty(importedPlan.LoopStates);
            Assert.Equal(
                4,
                Assert.Single(
                    importedPlan.Loops,
                    loop => !loop.Forever)
                    .TotalRuns);
            Assert.Equal(7, importedSetup.TeamSlot);
            Assert.Equal(
                1250,
                importedSetup
                    .PlacementIntervalMilliseconds);
            Assert.Equal(
                42_000,
                importedSetup
                    .DefaultAfterStartDelayMilliseconds);
            Assert.Equal(
                UnitTargetingPriority.Strongest,
                Assert.Single(importedSetup.Steps)
                    .TargetingPriority);
            Assert.Equal(
                PlacementSetupCatalog.IdFor(target),
                importedSetup.Id);
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
    public async Task Export_UsesSharedExpeditionSetupWhenNoMapOverrideExists()
    {
        string sourceRoot =
            TestPaths.NewTemporaryDirectory();
        string destinationRoot =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementTarget required = new()
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 2,
            };
            PlacementTarget shared = required with
            {
                MapNumber =
                    PlacementSetupCatalog.SharedExpeditionMapNumber,
            };
            PlacementModel setup =
                Setup(shared, team: 4);
            MacroPlan plan = Plan(
                new MacroTaskDefinition
                {
                    Id = "expedition-task",
                    Kind = MacroTaskKind.Expedition,
                    Name = "Flower Forest",
                    PlacementTarget = required,
                });

            AppPaths sourcePaths =
                new(sourceRoot);
            await new PlacementModelRepository(
                    sourcePaths)
                .SaveAsync(setup);
            await new MacroPlanRepository(sourcePaths)
                .SaveAsync(plan);

            string code =
                await Service(sourceRoot)
                    .ExportAsync(plan);
            FastNoAlignShareBundle bundle =
                Service(sourceRoot).Read(code);

            PlacementModel exported =
                Assert.Single(bundle.PlacementSetups);
            Assert.Equal(
                PlacementSetupCatalog.IdFor(shared),
                exported.Id);
            Assert.True(
                PlacementSetupCatalog.Covers(
                    exported.Target!,
                    required));

            await Service(destinationRoot)
                .ImportAsync(bundle);
            PlacementModel imported =
                await new PlacementModelRepository(
                        new AppPaths(destinationRoot))
                    .LoadAsync(setup.Id)
                ?? throw new InvalidOperationException();
            Assert.Equal(4, imported.TeamSlot);
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
    public void Plan_RejectsSharedExpeditionTargetAsRunnableTask()
    {
        MacroPlan plan = Plan(
            new MacroTaskDefinition
            {
                Id = "expedition-task",
                Kind = MacroTaskKind.Expedition,
                Name = "Shared",
                PlacementTarget = new PlacementTarget
                {
                    Mode = PlacementTargetMode.Expedition,
                    MapNumber =
                        PlacementSetupCatalog
                            .SharedExpeditionMapNumber,
                },
            });

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                plan.Validate);
        Assert.Contains(
            "specific Expedition map",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RejectsSharedStoryMapAsRunnableTask()
    {
        MacroPlan plan = Plan(
            new MacroTaskDefinition
            {
                Id = "story-task",
                Kind = MacroTaskKind.Story,
                Name = "Shared Story map",
                PlacementTarget = new PlacementTarget
                {
                    Mode = PlacementTargetMode.Story,
                    MapNumber = 2,
                    StoryRunKind = StoryRunKind.Act,
                    ActNumber =
                        PlacementSetupCatalog
                            .SharedStoryActNumber,
                },
            });

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                plan.Validate);
        Assert.Contains(
            "specific Story run",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_UsesSharedStoryMapWhenNoExactOverrideExists()
    {
        string sourceRoot =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementTarget required = new()
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = 3,
                StoryRunKind = StoryRunKind.Infinite,
                ActNumber = 1,
            };
            PlacementTarget shared = required with
            {
                StoryRunKind = StoryRunKind.Act,
                ActNumber =
                    PlacementSetupCatalog
                        .SharedStoryActNumber,
            };
            PlacementModel setup =
                Setup(shared, team: 6);
            MacroPlan plan = Plan(
                new MacroTaskDefinition
                {
                    Id = "story-task",
                    Kind = MacroTaskKind.Story,
                    Name = "Rose Kingdom Infinite",
                    PlacementTarget = required,
                    TargetRuntimeMinutes = 180,
                    CompleteOnRuntimeDefeat = true,
                });

            await new PlacementModelRepository(
                    new AppPaths(sourceRoot))
                .SaveAsync(setup);

            FastNoAlignShareService service =
                Service(sourceRoot);
            FastNoAlignShareBundle bundle =
                service.Read(
                    await service.ExportAsync(plan));

            PlacementModel exported =
                Assert.Single(bundle.PlacementSetups);
            Assert.Equal(setup.Id, exported.Id);
            Assert.True(
                PlacementSetupCatalog.Covers(
                    exported.Target!,
                    required));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                sourceRoot);
        }
    }

    [Fact]
    public void Bundle_RejectsLegacyTasks()
    {
        MacroPlan legacy = Plan(
            new MacroTaskDefinition
            {
                Id = "legacy",
                Kind = MacroTaskKind.Expedition,
                PresetId = "legacy-preset",
                Name = "Legacy",
            });
        FastNoAlignShareBundle bundle = new()
        {
            Plan = legacy,
            PlacementSetups = [],
        };

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                bundle.Validate);
        Assert.Contains(
            "Legacy preset",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChallengePlan_RequiresEveryChallengeMapSetup()
    {
        MacroPlan plan = Plan(
            new MacroTaskDefinition
            {
                Id = "challenge",
                Kind = MacroTaskKind.Challenge,
                Name = "Challenge rotation",
            });

        IReadOnlySet<string> ids =
            FastNoAlignShareBundle
                .RequiredSetupIds(plan);

        Assert.Equal(5, ids.Count);
        Assert.All(
            PlacementSetupCatalog.All.Where(
                route =>
                    route.Target.Mode ==
                    PlacementTargetMode.Challenge),
            route => Assert.Contains(
                route.ModelId,
                ids));
    }

    [Fact]
    public void Decode_RejectsDamagedText()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () =>
                    FastNoAlignShareCodec.Decode(
                        "EMFAST1:not-base64"));
        Assert.Contains(
            "damaged",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static FastNoAlignShareService Service(
        string root)
    {
        AppPaths paths = new(root);
        return new FastNoAlignShareService(
            new MacroPlanRepository(paths),
            new PlacementModelRepository(paths));
    }

    private static MacroPlan Plan(
        MacroTaskDefinition task) =>
        new()
        {
            Id = "shared-plan",
            Name = "Shared plan",
            Tasks = [task],
        };

    private static PlacementModel Setup(
        PlacementTarget target,
        int team)
    {
        PlacementSetupRoute route =
            PlacementSetupCatalog.For(target);
        return new PlacementModel
        {
            Id = route.ModelId,
            Name = route.Name,
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = target,
            TeamSlot = team,
            Steps =
            [
                new PlacementStep
                {
                    UnitKey = 1,
                    X = 400,
                    Y = 300,
                    DelayAfterMilliseconds = 900,
                    Phase = PlacementPhase.BeforeStart,
                    TargetingPriority =
                        UnitTargetingPriority.Strongest,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
