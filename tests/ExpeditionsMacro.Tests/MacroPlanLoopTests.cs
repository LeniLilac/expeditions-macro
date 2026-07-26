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
                        if (changed.LoopProgress
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
                saved.LoopProgress.CompletedRuns);
            Assert.Equal(
                MacroPlanLoopPhase.AfterLoop,
                saved.LoopProgress.Phase);
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
    public async Task ForeverLoop_NeverRunsTasksAfterItsStop()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            MacroTaskDefinition looping =
                Task("looping", MacroTaskKind.Raid, 1);
            MacroTaskDefinition after =
                Task("after", MacroTaskKind.Story, 2);
            MacroPlan plan =
                Plan(looping, after) with
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
                        if (changed.LoopProgress
                                .CompletedRuns >= 3)
                        {
                            stopped.Cancel();
                        }
                    },
                    cancellationToken:
                        stopped.Token));

            Assert.Equal(
                [looping.Id, looping.Id, looping.Id],
                executions);
            Assert.DoesNotContain(after.Id, executions);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
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

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) => new()
        {
            Id = "loop-plan",
            Name = "Loop plan",
            Tasks = tasks,
        };
}
