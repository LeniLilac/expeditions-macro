using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class RecurringTaskProgressTests
{
    [Fact]
    public async Task PartialRefuelCheckpoint_SurvivesSchedulerCancellation()
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
                1) with
            {
                RefuelTarget = ResourceRefuelTarget.Both,
            };
            MacroPlan plan = Plan(utility);
            using CancellationTokenSource stopped = new();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    async (_, _, checkpoint, token) =>
                    {
                        await checkpoint(
                            new ScheduledTaskCheckpoint(
                                ResourceRefuelTarget.GoldMine));
                        MacroPlan saved = Assert.IsType<MacroPlan>(
                            await repository.LoadAsync(
                                plan.Id,
                                token));
                        Assert.Equal(
                            ResourceRefuelTarget.GoldMine,
                            saved.ProgressFor(utility.Id)
                                .RefuelCompletedTargets);
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
                ResourceRefuelTarget.GoldMine,
                reloaded.ProgressFor(utility.Id)
                    .RefuelCompletedTargets);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task ChallengeResult_PersistsItsRotationSnapshot()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            MacroPlanRepository repository = new(
                new AppPaths(root));
            MacroScheduler scheduler = new(repository);
            MacroTaskDefinition challenge = Task(
                "challenge",
                MacroTaskKind.Challenge,
                1);
            MacroTaskDefinition story = Task(
                "story",
                MacroTaskKind.Story,
                2);
            MacroPlan plan = Plan(challenge, story);
            ChallengeRotationProgress rotation = new()
            {
                Epoch = new DateTimeOffset(
                    2026,
                    7,
                    31,
                    12,
                    0,
                    0,
                    TimeSpan.Zero),
                Attempted = [ChallengeType.Stat],
            };
            using CancellationTokenSource stopped = new();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    async (task, _, token) =>
                    {
                        if (task.Kind ==
                            MacroTaskKind.Challenge)
                        {
                            return new ScheduledTaskResult(
                                1,
                                0,
                                TimeSpan.FromMinutes(4),
                                DateTimeOffset.UtcNow
                                    .AddHours(1),
                                Skipped: true,
                                ChallengeRotation: rotation);
                        }
                        MacroPlan saved =
                            Assert.IsType<MacroPlan>(
                                await repository.LoadAsync(
                                    plan.Id,
                                    token));
                        AssertRotation(
                            rotation,
                            saved.ChallengeRotation);
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
            AssertRotation(
                rotation,
                reloaded.ChallengeRotation);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static void AssertRotation(
        ChallengeRotationProgress expected,
        ChallengeRotationProgress? actual)
    {
        ChallengeRotationProgress persisted =
            Assert.IsType<ChallengeRotationProgress>(actual);
        Assert.Equal(expected.Epoch, persisted.Epoch);
        Assert.Equal(expected.Attempted, persisted.Attempted);
    }

    private static MacroTaskDefinition Task(
        string id,
        MacroTaskKind kind,
        int priority) => new()
        {
            Id = id,
            Kind = kind,
            PresetId = kind == MacroTaskKind.Utility
            ? string.Empty
            : $"{id}-preset",
            Name = id,
            Priority = priority,
        };

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) => new()
        {
            Id = "recurring-progress-test",
            Name = "Recurring progress test",
            Tasks = tasks,
        };
}
