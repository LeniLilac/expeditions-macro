using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class BountyIntegrationTests
{
    [Fact]
    public void ShareDependencies_IncludeExactlyTheSixBountyRoutes()
    {
        MacroPlan plan = Plan(Bounty());

        IReadOnlyList<PlacementTarget> targets =
            FastNoAlignShareBundle.RequiredSetupTargets(plan);

        Assert.Equal(
            BountyCatalog.RequiredPlacementTargets,
            targets);
        HashSet<string> expectedIds =
            BountyCatalog.RequiredPlacementTargets
                .Select(PlacementSetupCatalog.IdFor)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
        Assert.True(
            expectedIds.SetEquals(
                FastNoAlignShareBundle
                    .RequiredSetupIds(plan)));
    }

    [Fact]
    public void SchedulerProgress_NeverCompletesTheRecurringBountyTask()
    {
        MacroTaskDefinition bounty = Bounty();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        MacroTaskProgress progress =
            MacroScheduler.Advance(
                bounty,
                new MacroTaskProgress
                {
                    TaskId = bounty.Id,
                },
                new ScheduledTaskResult(
                    10,
                    0,
                    TimeSpan.FromHours(2),
                    now.AddDays(1)),
                now);

        Assert.False(progress.Completed);
        Assert.Equal(
            now.AddDays(1),
            progress.NextEligibleAtUtc);
    }

    [Fact]
    public async Task NoGold_ExcludesOnlyBountyForTheCurrentSchedulerRun()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            MacroTaskDefinition bounty = Bounty();
            MacroTaskDefinition story = new()
            {
                Id = "story",
                Kind = MacroTaskKind.Story,
                PresetId = "story-preset",
                Name = "Story",
                Priority = 2,
            };
            MacroPlan plan = Plan(
                bounty,
                story);
            MacroScheduler scheduler = new(
                new MacroPlanRepository(
                    new AppPaths(root)));
            List<string> executed = [];
            using CancellationTokenSource stopped =
                new();

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                () => scheduler.RunAsync(
                    plan,
                    (task, _, token) =>
                    {
                        executed.Add(task.Id);
                        if (task.Kind ==
                            MacroTaskKind.Bounty)
                        {
                            return Task.FromResult(
                                new ScheduledTaskResult(
                                    0,
                                    0,
                                    TimeSpan.FromSeconds(5),
                                    Skipped: true,
                                    SkipUntilSchedulerRestart:
                                        true));
                        }
                        stopped.Cancel();
                        token.ThrowIfCancellationRequested();
                        return Task.FromResult(
                            new ScheduledTaskResult(
                                0,
                                0,
                                TimeSpan.Zero));
                    },
                    cancellationToken:
                        stopped.Token));

            Assert.Equal(
                new[] { "bounty", "story" },
                executed);
            Assert.Equal(
                bounty,
                MacroScheduler.SelectNext(
                    plan,
                    DateTimeOffset.UtcNow));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    private static MacroTaskDefinition Bounty() =>
        new()
        {
            Id = "bounty",
            Kind = MacroTaskKind.Bounty,
            Name = "Mythic Bounty Board",
            Priority = 1,
            BountyParkedNonViableLimit = 2,
        };

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) =>
        new()
        {
            Id = "bounty-plan",
            Name = "Bounty plan",
            Tasks = tasks,
        };
}
