using System.Text.Json;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class MacroPlanLoopTests
{
    [Fact]
    public void Validation_RequiresAnOrderedRangeWithAFiniteTask()
    {
        MacroTaskDefinition first =
            Task("first", MacroTaskKind.Story, 1);
        MacroTaskDefinition second =
            Task("second", MacroTaskKind.Raid, 2);
        MacroPlan reversed = Plan(first, second) with
        {
            Loop = new MacroPlanLoopDefinition
            {
                StartTaskId = second.Id,
                StopTaskId = first.Id,
                TotalRuns = 2,
            },
        };
        MacroPlan challengeOnly =
            Plan(
                Task(
                    "challenge",
                    MacroTaskKind.Challenge,
                    1)) with
            {
                Loop = new MacroPlanLoopDefinition
                {
                    StartTaskId = "challenge",
                    StopTaskId = "challenge",
                    TotalRuns = 2,
                },
            };

        Assert.Throws<InvalidDataException>(
            reversed.Validate);
        Assert.Throws<InvalidDataException>(
            challengeOnly.Validate);
    }

    [Fact]
    public void Validation_AllowsNestedAndSeparateLoopsButRejectsCrossing()
    {
        MacroTaskDefinition[] tasks =
        [
            Task("one", MacroTaskKind.Story, 1),
            Task("two", MacroTaskKind.Raid, 2),
            Task("three", MacroTaskKind.Event, 3),
            Task("four", MacroTaskKind.Story, 4),
        ];
        MacroPlan valid = Plan(tasks) with
        {
            Loops =
            [
                Loop(tasks[0], tasks[2], 2),
                Loop(tasks[1], tasks[1], 3),
                Loop(tasks[3], tasks[3], 4),
            ],
        };
        MacroPlan crossing = valid with
        {
            Loops =
            [
                Loop(tasks[0], tasks[2], 2),
                Loop(tasks[1], tasks[3], 2),
            ],
        };

        valid.Validate();
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                crossing.Validate);
        Assert.Contains(
            "cannot cross",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_RequiresOneTerminalForeverLoop()
    {
        MacroTaskDefinition[] tasks =
        [
            Task("one", MacroTaskKind.Story, 1),
            Task("two", MacroTaskKind.Raid, 2),
            Task("three", MacroTaskKind.Event, 3),
        ];
        MacroPlan nonTerminal = Plan(tasks) with
        {
            Loops =
            [
                Loop(
                    tasks[0],
                    tasks[1],
                    1,
                    forever: true),
            ],
        };
        MacroPlan twoForever = Plan(tasks) with
        {
            Loops =
            [
                Loop(
                    tasks[0],
                    tasks[2],
                    1,
                    forever: true),
                Loop(
                    tasks[2],
                    tasks[2],
                    1,
                    forever: true),
            ],
        };

        Assert.Throws<InvalidDataException>(
            nonTerminal.Validate);
        Assert.Throws<InvalidDataException>(
            twoForever.Validate);
    }

    [Fact]
    public void Validation_AllowsThreeLoopLevelsButRejectsFour()
    {
        MacroTaskDefinition[] tasks =
        [
            Task("one", MacroTaskKind.Story, 1),
            Task("two", MacroTaskKind.Raid, 2),
            Task("three", MacroTaskKind.Event, 3),
            Task("four", MacroTaskKind.Story, 4),
        ];
        MacroPlan threeLevels = Plan(tasks) with
        {
            Loops =
            [
                Loop(tasks[0], tasks[3], 2),
                Loop(tasks[0], tasks[2], 2),
                Loop(tasks[0], tasks[1], 2),
            ],
        };
        MacroPlan fourLevels = threeLevels with
        {
            Loops =
            [
                .. threeLevels.Loops,
                Loop(tasks[0], tasks[0], 2),
            ],
        };

        threeLevels.Validate();
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                fourLevels.Validate);
        Assert.Contains(
            "three levels",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FiniteLoop_RunsItsInclusiveRangeThenContinues()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            MacroTaskDefinition before =
                Task("before", MacroTaskKind.Story, 1);
            MacroTaskDefinition loopStart =
                Task("loop-start", MacroTaskKind.Raid, 2);
            MacroTaskDefinition loopStop =
                Task("loop-stop", MacroTaskKind.Event, 3);
            MacroTaskDefinition after =
                Task("after", MacroTaskKind.Story, 4);
            MacroPlan plan =
                Plan(
                    before,
                    loopStart,
                    loopStop,
                    after) with
                {
                    Loop = new MacroPlanLoopDefinition
                    {
                        StartTaskId = loopStart.Id,
                        StopTaskId = loopStop.Id,
                        TotalRuns = 3,
                    },
                };
            MacroPlanRepository repository =
                new(new AppPaths(root));
            MacroScheduler scheduler =
                new(repository);
            List<string> executions = [];
            using CancellationTokenSource stopped =
                new();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    (task, _, _) =>
                    {
                        executions.Add(task.Id);
                        return System.Threading.Tasks.Task.FromResult(
                            new ScheduledTaskResult(
                                1,
                                0,
                                TimeSpan.FromMinutes(1)));
                    },
                    planChanged: changed =>
                    {
                        if (changed.EffectiveLoopStates()
                                .Single()
                                .CompletedRuns == 3 &&
                            changed.ProgressFor(after.Id)
                                .Completed)
                        {
                            stopped.Cancel();
                        }
                    },
                    cancellationToken:
                        stopped.Token));

            Assert.Equal(
                [
                    before.Id,
                    loopStart.Id,
                    loopStop.Id,
                    loopStart.Id,
                    loopStop.Id,
                    loopStart.Id,
                    loopStop.Id,
                    after.Id,
                ],
                executions);
            MacroPlan saved =
                Assert.IsType<MacroPlan>(
                    await repository.LoadAsync(
                        plan.Id));
            Assert.Equal(
                3,
                saved.EffectiveLoopStates()
                    .Single()
                    .CompletedRuns);
            Assert.Equal(
                MacroPlanLoopPhase.AfterLoop,
                saved.EffectiveLoopStates()
                    .Single()
                    .Phase);
            Assert.Equal(
                3,
                saved.ProgressFor(loopStart.Id)
                    .Victories);
            Assert.Equal(
                2,
                saved.ProgressFor(loopStart.Id)
                    .TargetVictoryBaseline);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task ForeverLoop_MustBeTrailingAndNeverExits()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            MacroTaskDefinition before =
                Task("before", MacroTaskKind.Story, 1);
            MacroTaskDefinition looping =
                Task("looping", MacroTaskKind.Raid, 2);
            MacroPlan plan =
                Plan(before, looping) with
                {
                    Loop = new MacroPlanLoopDefinition
                    {
                        StartTaskId = looping.Id,
                        StopTaskId = looping.Id,
                        Forever = true,
                    },
                };
            MacroScheduler scheduler =
                new(
                    new MacroPlanRepository(
                        new AppPaths(root)));
            List<string> executions = [];
            using CancellationTokenSource stopped =
                new();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    (task, _, _) =>
                    {
                        executions.Add(task.Id);
                        return System.Threading.Tasks.Task.FromResult(
                            new ScheduledTaskResult(
                                1,
                                0,
                                TimeSpan.FromMinutes(1)));
                    },
                    planChanged: changed =>
                    {
                        if (changed.EffectiveLoopStates()
                                .Single()
                                .CompletedRuns >= 3)
                        {
                            stopped.Cancel();
                        }
                    },
                    cancellationToken:
                        stopped.Token));

            Assert.Equal(
                [
                    before.Id,
                    looping.Id,
                    looping.Id,
                    looping.Id,
                ],
                executions);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task SeparateFiniteLoops_RunInPlanOrder()
    {
        MacroTaskDefinition[] tasks =
        [
            Task("one", MacroTaskKind.Story, 1),
            Task("two", MacroTaskKind.Raid, 2),
            Task("three", MacroTaskKind.Event, 3),
            Task("four", MacroTaskKind.Story, 4),
        ];
        MacroPlan plan = Plan(tasks) with
        {
            Loops =
            [
                Loop(tasks[0], tasks[1], 2),
                Loop(tasks[2], tasks[3], 3),
            ],
        };

        IReadOnlyList<string> executions =
            await RunUntilAsync(
                plan,
                changed =>
                    changed.EffectiveLoopStates()
                        .Count == 2 &&
                    changed.EffectiveLoopStates()
                        .All(state =>
                            state.Phase ==
                                MacroPlanLoopPhase
                                    .AfterLoop));

        Assert.Equal(
            [
                "one",
                "two",
                "one",
                "two",
                "three",
                "four",
                "three",
                "four",
                "three",
                "four",
            ],
            executions);
    }

    [Fact]
    public async Task ForeverOuterLoop_ReplaysNestedFiniteLoop()
    {
        MacroTaskDefinition[] tasks =
        [
            Task("one", MacroTaskKind.Story, 1),
            Task("two", MacroTaskKind.Raid, 2),
        ];
        MacroPlanLoopDefinition forever =
            Loop(
                tasks[0],
                tasks[1],
                1,
                forever: true);
        MacroPlanLoopDefinition finite =
            Loop(tasks[0], tasks[1], 2);
        MacroPlan plan = Plan(tasks) with
        {
            Loops = [forever, finite],
        };

        IReadOnlyList<string> executions =
            await RunUntilAsync(
                plan,
                changed =>
                    changed.LoopStateFor(forever)
                        .CompletedRuns >= 2);

        Assert.Equal(
            [
                "one",
                "two",
                "one",
                "two",
                "one",
                "two",
                "one",
                "two",
            ],
            executions);
    }

    [Fact]
    public async Task FiniteLoopCanPrecedeTrailingForeverLoop()
    {
        MacroTaskDefinition[] tasks =
        [
            Task("finite", MacroTaskKind.Story, 1),
            Task("forever", MacroTaskKind.Raid, 2),
        ];
        MacroPlanLoopDefinition finite =
            Loop(tasks[0], tasks[0], 2);
        MacroPlanLoopDefinition forever =
            Loop(
                tasks[1],
                tasks[1],
                1,
                forever: true);
        MacroPlan plan = Plan(tasks) with
        {
            Loops = [finite, forever],
        };

        IReadOnlyList<string> executions =
            await RunUntilAsync(
                plan,
                changed =>
                    changed.LoopStateFor(forever)
                        .CompletedRuns >= 3);

        Assert.Equal(
            [
                "finite",
                "finite",
                "forever",
                "forever",
                "forever",
            ],
            executions);
    }

    [Fact]
    public void LegacySingleLoop_RemainsValidAndMigrates()
    {
        MacroTaskDefinition task =
            Task("legacy", MacroTaskKind.Story, 1);
        MacroPlan legacy = Plan(task) with
        {
            Loop = Loop(task, task, 4),
            LoopProgress = new MacroPlanLoopProgress
            {
                ConfigurationSignature =
                    Loop(task, task, 4)
                        .ConfigurationSignature,
                Phase = MacroPlanLoopPhase.Loop,
                CompletedRuns = 2,
            },
        };

        legacy.Validate();
        MacroPlan migrated =
            MacroPlanLoopPolicy.Normalize(legacy);

        Assert.Null(migrated.Loop);
        Assert.True(migrated.LoopProgress.IsEmpty);
        Assert.Equal(
            4,
            Assert.Single(migrated.Loops)
                .TotalRuns);
        Assert.Equal(
            2,
            Assert.Single(migrated.LoopStates)
                .CompletedRuns);
    }

    [Fact]
    public void LegacyPlanWithoutLoopFields_RemainsLoopFree()
    {
        const string json =
            """
            {
              "schema_version": 1,
              "id": "beta29-no-loop",
              "name": "Beta 29 no loop",
              "tasks": [
                {
                  "id": "story",
                  "kind": "story",
                  "preset_id": "story-preset",
                  "name": "Story",
                  "priority": 1
                }
              ],
              "progress": []
            }
            """;
        MacroPlan legacy =
            JsonSerializer.Deserialize<MacroPlan>(
                json,
                JsonFileStore.Options) ??
            throw new InvalidDataException(
                "The legacy plan did not deserialize.");

        legacy.Validate();
        MacroPlan normalized =
            MacroPlanLoopPolicy.Normalize(legacy);

        Assert.Empty(normalized.EffectiveLoops());
        Assert.Empty(
            normalized.EffectiveLoopStates());
        Assert.Null(normalized.Loop);
        Assert.True(
            normalized.LoopProgress.IsEmpty);
    }

    [Fact]
    public void Advance_UsesTheCurrentLoopBaseline()
    {
        MacroTaskDefinition task =
            Task("story", MacroTaskKind.Story, 1) with
            {
                TargetVictories = 2,
            };
        MacroTaskProgress before = new()
        {
            TaskId = task.Id,
            Victories = 4,
            TargetVictoryBaseline = 3,
        };

        MacroTaskProgress after =
            MacroScheduler.Advance(
                task,
                before,
                new ScheduledTaskResult(
                    1,
                    0,
                    TimeSpan.FromMinutes(1)),
                DateTimeOffset.UtcNow);

        Assert.True(after.Completed);
        Assert.Equal(5, after.Victories);
    }

    private static MacroTaskDefinition Task(
        string id,
        MacroTaskKind kind,
        int priority) => new()
        {
            Id = id,
            Kind = kind,
            PresetId = $"{id}-preset",
            Name = id,
            Priority = priority,
        };

    private static MacroPlanLoopDefinition Loop(
        MacroTaskDefinition start,
        MacroTaskDefinition stop,
        int runs,
        bool forever = false) => new()
        {
            StartTaskId = start.Id,
            StopTaskId = stop.Id,
            TotalRuns = runs,
            Forever = forever,
        };

    private static async Task<IReadOnlyList<string>>
        RunUntilAsync(
        MacroPlan plan,
        Func<MacroPlan, bool> stop)
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            MacroScheduler scheduler =
                new(
                    new MacroPlanRepository(
                        new AppPaths(root)));
            List<string> executions = [];
            using CancellationTokenSource stopped =
                new();
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    (task, _, _) =>
                    {
                        executions.Add(task.Id);
                        return System.Threading.Tasks.Task
                            .FromResult(
                            new ScheduledTaskResult(
                                1,
                                0,
                                TimeSpan.FromMinutes(1)));
                    },
                    planChanged: changed =>
                    {
                        if (stop(changed))
                        {
                            stopped.Cancel();
                        }
                    },
                    cancellationToken:
                        stopped.Token));
            return executions;
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) => new()
        {
            Id = "loop-plan",
            Name = "Loop plan",
            Tasks = tasks,
        };
}
