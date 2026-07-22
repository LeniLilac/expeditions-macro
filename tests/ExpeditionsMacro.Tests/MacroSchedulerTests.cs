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
    public void Selection_SkipsCompletedAndDisabledFiniteTasks_ButChallengeRemainsRecurring()
    {
        MacroTaskDefinition challenge = Task("challenge", MacroTaskKind.Challenge, priority: 3);
        MacroTaskDefinition complete = Task("complete", MacroTaskKind.Expedition, priority: 1);
        MacroTaskDefinition disabled = Task("disabled", MacroTaskKind.Raid, priority: 2) with { Enabled = false };
        MacroPlan plan = Plan(complete, disabled, challenge) with
        {
            Progress =
            [
                new MacroTaskProgress { TaskId = complete.Id, Completed = true },
                new MacroTaskProgress { TaskId = disabled.Id },
                new MacroTaskProgress { TaskId = challenge.Id, Completed = true },
            ],
        };

        Assert.Equal(challenge, MacroScheduler.SelectNext(plan, DateTimeOffset.UtcNow));
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
            MacroPlan plan = Plan(first, second) with
            {
                Progress =
                [
                    new MacroTaskProgress { TaskId = first.Id, Victories = 4, Completed = true },
                    new MacroTaskProgress { TaskId = second.Id, Defeats = 2 },
                ],
            };

            MacroPlan reset = await scheduler.ResetProgressAsync(plan);
            MacroPlan loaded = Assert.IsType<MacroPlan>(await repository.LoadAsync(plan.Id));

            Assert.All(reset.Progress, value =>
            {
                Assert.Equal(0, value.Victories);
                Assert.Equal(0, value.Defeats);
                Assert.False(value.Completed);
            });
            Assert.Equal(reset.Progress, loaded.Progress);
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
        PresetId = $"{id}-preset",
        Name = id,
        Priority = priority,
    };

    private static MacroPlan Plan(params MacroTaskDefinition[] tasks) => new()
    {
        Id = "test-plan",
        Name = "Test plan",
        Tasks = tasks,
    };
}
