using System.Text.Json;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class MacroSchedulerTests
{
    [Fact]
    public void Selection_UsesTheFirstEligiblePriorityWithoutStarvingLowerTasksDuringCooldown()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MacroTaskDefinition high = Task("high", MacroTaskKind.Challenge, priority: 1);
        MacroTaskDefinition middle = Task("middle", MacroTaskKind.Story, priority: 2);
        MacroTaskDefinition low = Task("low", MacroTaskKind.Raid, priority: 3);
        MacroPlan plan = Plan(high, middle, low) with
        {
            Progress =
            [
                new MacroTaskProgress { TaskId = high.Id, NextEligibleAtUtc = now.AddMinutes(20) },
                new MacroTaskProgress { TaskId = middle.Id },
                new MacroTaskProgress { TaskId = low.Id },
            ],
        };

        Assert.Equal(middle, MacroScheduler.SelectNext(plan, now));
        Assert.Equal(high, MacroScheduler.SelectNext(plan, now.AddMinutes(21)));
    }

    [Fact]
    public void ChallengeRemainsTheOwnerBetweenMatches_ButCooldownHandsOffToTheNextMode()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        MacroTaskDefinition challenge = Task("challenge", MacroTaskKind.Challenge, priority: 1);
        MacroTaskDefinition expedition = Task("expedition", MacroTaskKind.Expedition, priority: 2);
        MacroPlan plan = Plan(challenge, expedition);

        MacroTaskProgress afterMatch = MacroScheduler.Advance(
            challenge,
            plan.ProgressFor(challenge.Id),
            new ScheduledTaskResult(1, 0, TimeSpan.FromMinutes(4)),
            now);
        MacroPlan betweenMatches = plan with { Progress = [afterMatch, plan.ProgressFor(expedition.Id)] };

        Assert.Equal(challenge, MacroScheduler.SelectNext(betweenMatches, now));

        DateTimeOffset nextReset = now.AddMinutes(20);
        MacroTaskProgress onCooldown = MacroScheduler.Advance(
            challenge,
            afterMatch,
            new ScheduledTaskResult(0, 0, TimeSpan.FromSeconds(2), nextReset, Skipped: true),
            now.AddSeconds(2));
        MacroPlan readyForHandoff = plan with { Progress = [onCooldown, plan.ProgressFor(expedition.Id)] };

        Assert.Equal(expedition, MacroScheduler.SelectNext(readyForHandoff, now.AddSeconds(3)));
        Assert.Equal(challenge, MacroScheduler.SelectNext(readyForHandoff, nextReset));
    }

    [Fact]
    public void UtilityRunsImmediatelyThenHandsOffUntilItsInterval()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset nextRefuel = now.AddMinutes(30);
        MacroTaskDefinition utility =
            Task(
                "refuel",
                MacroTaskKind.Utility,
                priority: 1);
        MacroTaskDefinition story =
            Task(
                "story",
                MacroTaskKind.Story,
                priority: 2);
        MacroPlan plan = Plan(utility, story);

        Assert.Equal(
            utility,
            MacroScheduler.SelectNext(plan, now));
        MacroTaskProgress cooldown =
            MacroScheduler.Advance(
                utility,
                plan.ProgressFor(utility.Id),
                new ScheduledTaskResult(
                    0,
                    0,
                    TimeSpan.FromSeconds(20),
                    nextRefuel),
                now);
        MacroPlan waiting = plan with
        {
            Progress =
            [
                cooldown,
                plan.ProgressFor(story.Id),
            ],
        };

        Assert.False(cooldown.Completed);
        Assert.Equal(
            story,
            MacroScheduler.SelectNext(
                waiting,
                now.AddMinutes(1)));
        Assert.Equal(
            utility,
            MacroScheduler.SelectNext(
                waiting,
                nextRefuel));
    }

    [Fact]
    public async Task UtilityCooldown_IsPersistedBeforeTheNextTaskRuns()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            MacroPlanRepository repository = new(
                new AppPaths(root));
            MacroScheduler scheduler = new(repository);
            MacroTaskDefinition utility = Task(
                "refuel",
                MacroTaskKind.Utility,
                1);
            MacroTaskDefinition story = Task(
                "story",
                MacroTaskKind.Story,
                2);
            MacroPlan plan = Plan(utility, story);
            DateTimeOffset nextEligible =
                DateTimeOffset.UtcNow.AddMinutes(45);
            using CancellationTokenSource stopped = new();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    async (task, _, token) =>
                    {
                        if (task.Kind ==
                            MacroTaskKind.Utility)
                        {
                            return new ScheduledTaskResult(
                                0,
                                0,
                                TimeSpan.FromSeconds(10),
                                nextEligible);
                        }

                        MacroPlan saved =
                            Assert.IsType<MacroPlan>(
                                await repository.LoadAsync(
                                    plan.Id,
                                    token));
                        Assert.Equal(
                            nextEligible,
                            saved.ProgressFor(utility.Id)
                                .NextEligibleAtUtc);
                        stopped.Cancel();
                        token.ThrowIfCancellationRequested();
                        return new ScheduledTaskResult(
                            0,
                            0,
                            TimeSpan.Zero);
                    },
                    cancellationToken: stopped.Token));

            MacroPlan reloaded = Assert.IsType<MacroPlan>(
                await repository.LoadAsync(plan.Id));
            Assert.Equal(
                nextEligible,
                reloaded.ProgressFor(utility.Id)
                    .NextEligibleAtUtc);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void LegacyDisabledTask_DeserializesAsActiveAndIsOmittedFromNewJson()
    {
        MacroTaskDefinition task =
            Assert.IsType<MacroTaskDefinition>(
                JsonSerializer.Deserialize<
                    MacroTaskDefinition>(
                    """
                    {
                      "id": "legacy-disabled",
                      "kind": "raid",
                      "preset_id": "legacy-preset",
                      "name": "Legacy disabled task",
                      "priority": 1,
                      "enabled": false
                    }
                    """,
                    JsonFileStore.Options));
        MacroPlanLoopDefinition loop = new()
        {
            StartTaskId = task.Id,
            StopTaskId = task.Id,
            TotalRuns = 2,
        };
        MacroPlan plan = Plan(task) with
        {
            Loops = [loop],
        };

        plan.Validate();
        Assert.Equal(
            task,
            MacroScheduler.SelectNext(
                plan,
                DateTimeOffset.UtcNow));
        Assert.DoesNotContain(
            "\"enabled\"",
            JsonSerializer.Serialize(
                task,
                JsonFileStore.Options),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FiniteTask_CompletesAtItsVictoryTarget()
    {
        MacroTaskDefinition task = Task("story", MacroTaskKind.Story, priority: 1) with { TargetVictories = 2 };
        MacroTaskProgress before = new() { TaskId = task.Id, Victories = 1 };

        MacroTaskProgress after = MacroScheduler.Advance(
            task,
            before,
            new ScheduledTaskResult(1, 0, TimeSpan.FromMinutes(3)),
            DateTimeOffset.UtcNow);

        Assert.True(after.Completed);
        Assert.Equal(2, after.Victories);
        Assert.Equal(180, after.RuntimeSeconds);
        Assert.NotNull(after.LastCompletedAt);
    }

    [Fact]
    public void InfiniteStory_CompletesOnlyOnDefeatAfterItsRuntimeTarget()
    {
        MacroTaskDefinition task = Task("infinite", MacroTaskKind.Story, priority: 1) with
        {
            CompleteOnRuntimeDefeat = true,
            TargetRuntimeMinutes = 60,
        };
        MacroTaskProgress nearlyDone = new() { TaskId = task.Id, RuntimeSeconds = 3590 };

        MacroTaskProgress victory = MacroScheduler.Advance(
            task,
            nearlyDone,
            new ScheduledTaskResult(1, 0, TimeSpan.FromSeconds(20)),
            DateTimeOffset.UtcNow);
        MacroTaskProgress defeat = MacroScheduler.Advance(
            task,
            nearlyDone,
            new ScheduledTaskResult(0, 1, TimeSpan.FromSeconds(20)),
            DateTimeOffset.UtcNow);

        Assert.False(victory.Completed);
        Assert.True(defeat.Completed);
    }

    [Fact]
    public void SafeSkip_TemporarilyDefersAFiniteTask()
    {
        DateTimeOffset retry = DateTimeOffset.UtcNow.AddMinutes(5);
        MacroTaskDefinition task = Task("raid", MacroTaskKind.Raid, priority: 1);

        MacroTaskProgress after = MacroScheduler.Advance(
            task,
            new MacroTaskProgress { TaskId = task.Id },
            new ScheduledTaskResult(0, 0, TimeSpan.Zero, retry, Skipped: true),
            DateTimeOffset.UtcNow);

        Assert.False(after.Completed);
        Assert.Equal(retry, after.NextEligibleAtUtc);
    }

    [Theory]
    [InlineData(MacroTaskKind.Expedition, true)]
    [InlineData(MacroTaskKind.Story, true)]
    [InlineData(MacroTaskKind.Raid, true)]
    [InlineData(MacroTaskKind.Event, true)]
    [InlineData(MacroTaskKind.Challenge, false)]
    [InlineData(MacroTaskKind.Utility, false)]
    public void RepeatStage_RequiresTheSameFiniteRoute(MacroTaskKind kind, bool expected)
    {
        MacroTaskDefinition current = Task("current", kind, 1) with { PresetId = "same-route" };
        MacroTaskDefinition following = Task("following", kind, 2) with { PresetId = "same-route" };

        Assert.Equal(expected, MacroScheduler.CanRepeatStage(
            current,
            following,
            new ScheduledTaskResult(1, 0, TimeSpan.FromMinutes(2))));
        Assert.False(MacroScheduler.CanRepeatStage(
            current,
            following with { PresetId = "different-route" },
            new ScheduledTaskResult(1, 0, TimeSpan.FromMinutes(2))));
        Assert.False(MacroScheduler.CanRepeatStage(
            current,
            following,
            new ScheduledTaskResult(0, 0, TimeSpan.Zero, Skipped: true)));
    }

    [Fact]
    public async Task MatchCallback_PersistsEachRepeatedStageBeforeTheRunnerContinues()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlanRepository repository = new(paths);
            MacroScheduler scheduler = new(repository);
            MacroTaskDefinition expedition = Task("expedition", MacroTaskKind.Expedition, 1) with { TargetVictories = 2 };
            MacroPlan plan = Plan(expedition);
            using CancellationTokenSource stopped = new();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduler.RunAsync(
                plan,
                async (_, record, token) =>
                {
                    ScheduledTaskContinuation first = await record(
                        new ScheduledTaskResult(1, 0, TimeSpan.FromMinutes(3)),
                        token);
                    Assert.Equal(ScheduledTaskContinuation.RepeatStage, first);
                    MacroPlan afterFirst = Assert.IsType<MacroPlan>(await repository.LoadAsync(plan.Id, token));
                    Assert.Equal(1, afterFirst.ProgressFor(expedition.Id).Victories);

                    ScheduledTaskContinuation second = await record(
                        new ScheduledTaskResult(1, 0, TimeSpan.FromMinutes(3)),
                        token);
                    Assert.Equal(ScheduledTaskContinuation.Handoff, second);
                    stopped.Cancel();
                    return new ScheduledTaskResult(2, 0, TimeSpan.FromMinutes(6));
                },
                cancellationToken: stopped.Token));

            MacroPlan completed = Assert.IsType<MacroPlan>(await repository.LoadAsync(plan.Id));
            Assert.Equal(2, completed.ProgressFor(expedition.Id).Victories);
            Assert.True(completed.ProgressFor(expedition.Id).Completed);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData(MacroTaskKind.Expedition)]
    [InlineData(MacroTaskKind.Story)]
    [InlineData(MacroTaskKind.Raid)]
    [InlineData(MacroTaskKind.Challenge)]
    public async Task RouteIntoEvent_RequiresLobby(
        MacroTaskKind currentKind)
    {
        MacroTaskDefinition current =
            Task("current", currentKind, 1) with
            {
                TargetVictories = 1,
            };
        MacroTaskDefinition next =
            Task("event", MacroTaskKind.Event, 2) with
            {
                TargetVictories = 1,
            };
        ScheduledTaskResult result =
            currentKind == MacroTaskKind.Challenge
                ? new ScheduledTaskResult(
                    0,
                    0,
                    TimeSpan.Zero,
                    DateTimeOffset.UtcNow.AddMinutes(30),
                    Skipped: true)
                : new ScheduledTaskResult(
                    1,
                    0,
                    TimeSpan.FromMinutes(3));

        ScheduledTaskContinuation continuation =
            await ObserveContinuationAsync(
                current,
                next,
                result);

        Assert.Equal(
            ScheduledTaskContinuation.ReturnToLobby,
            continuation);
    }

    [Theory]
    [InlineData(MacroTaskKind.Expedition)]
    [InlineData(MacroTaskKind.Story)]
    [InlineData(MacroTaskKind.Raid)]
    [InlineData(MacroTaskKind.Event)]
    [InlineData(MacroTaskKind.Challenge)]
    public async Task RouteIntoUtility_RequiresLobby(
        MacroTaskKind currentKind)
    {
        MacroTaskDefinition current =
            Task("current", currentKind, 1);
        MacroTaskDefinition utility =
            Task(
                "utility",
                MacroTaskKind.Utility,
                2);
        ScheduledTaskResult result =
            currentKind == MacroTaskKind.Challenge
                ? new ScheduledTaskResult(
                    0,
                    0,
                    TimeSpan.Zero,
                    DateTimeOffset.UtcNow.AddMinutes(30),
                    Skipped: true)
                : new ScheduledTaskResult(
                    1,
                    0,
                    TimeSpan.FromMinutes(3));

        Assert.Equal(
            ScheduledTaskContinuation.ReturnToLobby,
            await ObserveContinuationAsync(
                current,
                utility,
                result));
    }

    [Fact]
    public async Task DifferentEventRoute_ReturnsToLobby()
    {
        MacroTaskDefinition current =
            Task("event-one", MacroTaskKind.Event, 1) with
            {
                TargetVictories = 1,
            };
        MacroTaskDefinition next =
            Task("event-two", MacroTaskKind.Event, 2) with
            {
                TargetVictories = 1,
            };

        ScheduledTaskContinuation continuation =
            await ObserveContinuationAsync(
                current,
                next,
                new ScheduledTaskResult(
                    1,
                    0,
                    TimeSpan.FromMinutes(3)));

        Assert.Equal(
            ScheduledTaskContinuation.ReturnToLobby,
            continuation);
    }

    [Fact]
    public async Task EventActOneAngleChange_ReturnsToLobby()
    {
        PlacementTarget angleOne = new()
        {
            Mode = PlacementTargetMode.Event,
            MapNumber =
                (int)EventModeId.VillainInvasion,
            ActNumber = (int)EventAct.Act1,
            SpawnRoute = EventSpawnRoute.Angle1,
        };
        MacroTaskDefinition current =
            Task("event-angle-one", MacroTaskKind.Event, 1) with
            {
                TargetVictories = 1,
                PlacementTarget = angleOne,
            };
        MacroTaskDefinition next =
            Task("event-angle-two", MacroTaskKind.Event, 2) with
            {
                TargetVictories = 1,
                PlacementTarget = angleOne with
                {
                    SpawnRoute =
                        EventSpawnRoute.Angle2,
                },
            };
        ScheduledTaskResult result = new(
            1,
            0,
            TimeSpan.FromMinutes(3));

        Assert.False(
            MacroScheduler.CanRepeatStage(
                current,
                next,
                result));
        Assert.Equal(
            ScheduledTaskContinuation.ReturnToLobby,
            await ObserveContinuationAsync(
                current,
                next,
                result));
    }

    [Fact]
    public async Task EventToPlayAccessibleRoute_UsesSharedHandoff()
    {
        MacroTaskDefinition current =
            Task("event", MacroTaskKind.Event, 1) with
            {
                TargetVictories = 1,
            };
        MacroTaskDefinition next =
            Task("story", MacroTaskKind.Story, 2) with
            {
                TargetVictories = 1,
            };

        ScheduledTaskContinuation continuation =
            await ObserveContinuationAsync(
                current,
                next,
                new ScheduledTaskResult(
                    1,
                    0,
                    TimeSpan.FromMinutes(3)));

        Assert.Equal(
            ScheduledTaskContinuation.Handoff,
            continuation);
    }

    [Fact]
    public async Task ResetProgress_PersistsAnEmptyRecordForEveryTask()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlanRepository repository = new(paths);
            MacroScheduler scheduler = new(repository);
            MacroTaskDefinition first = Task("first", MacroTaskKind.Expedition, 1);
            MacroTaskDefinition second = Task("second", MacroTaskKind.Raid, 2);
            MacroTaskDefinition challenge = Task(
                "challenge",
                MacroTaskKind.Challenge,
                3);
            MacroTaskDefinition utility = Task(
                "utility",
                MacroTaskKind.Utility,
                4) with
            {
                RefuelTarget = ResourceRefuelTarget.Both,
            };
            MacroPlan plan = Plan(
                first,
                second,
                challenge,
                utility) with
            {
                Progress =
                [
                    new MacroTaskProgress
                    {
                        TaskId = first.Id,
                        Victories = 4,
                        Completed = true,
                    },
                    new MacroTaskProgress { TaskId = second.Id, Defeats = 2 },
                    new MacroTaskProgress
                    {
                        TaskId = challenge.Id,
                    },
                    new MacroTaskProgress
                    {
                        TaskId = utility.Id,
                        RefuelCompletedTargets =
                            ResourceRefuelTarget.GoldMine,
                    },
                ],
                ChallengeRotation =
                    new ChallengeRotationProgress
                    {
                        Epoch = DateTimeOffset.UtcNow,
                        Attempted =
                            [ChallengeType.Trait],
                    },
            };

            MacroPlan reset = await scheduler.ResetProgressAsync(plan);
            MacroPlan loaded = Assert.IsType<MacroPlan>(await repository.LoadAsync(plan.Id));

            Assert.All(reset.Progress, value =>
            {
                Assert.Equal(0, value.Victories);
                Assert.Equal(0, value.Defeats);
                Assert.False(value.Completed);
                Assert.Equal(
                    ResourceRefuelTarget.None,
                    value.RefuelCompletedTargets);
            });
            Assert.Null(reset.ChallengeRotation);
            Assert.Equal(reset.Progress, loaded.Progress);
            Assert.Null(loaded.ChallengeRotation);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static MacroTaskDefinition Task(string id, MacroTaskKind kind, int priority) => new()
    {
        Id = id,
        Kind = kind,
        PresetId = kind == MacroTaskKind.Utility
            ? string.Empty
            : $"{id}-preset",
        Name = id,
        Priority = priority,
    };

    private static MacroPlan Plan(params MacroTaskDefinition[] tasks) => new()
    {
        Id = "test-plan",
        Name = "Test plan",
        Tasks = tasks,
    };

    private static async Task<ScheduledTaskContinuation>
        ObserveContinuationAsync(
        MacroTaskDefinition current,
        MacroTaskDefinition following,
        ScheduledTaskResult result)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            MacroScheduler scheduler = new(
                new MacroPlanRepository(
                    new AppPaths(root)));
            using CancellationTokenSource stopped = new();
            ScheduledTaskContinuation? observed = null;
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    Plan(current, following),
                    async (_, record, token) =>
                    {
                        observed = await record(
                            result,
                            token);
                        stopped.Cancel();
                        return result;
                    },
                    cancellationToken:
                        stopped.Token));
            return Assert.IsType<
                ScheduledTaskContinuation>(observed);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }
}
