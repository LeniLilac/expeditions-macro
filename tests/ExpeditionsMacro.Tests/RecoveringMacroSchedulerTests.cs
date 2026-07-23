using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class RecoveringMacroSchedulerTests
{
    [Fact]
    public async Task RuntimeFailure_RestartsRobloxAndRetriesTheIncompleteTask()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlanRepository plans = new(paths);
            MacroTaskDefinition task = new()
            {
                Id = "expedition-1",
                Kind = MacroTaskKind.Expedition,
                PresetId = "map-1",
                Name = "Map 1",
                Priority = 1,
                TargetVictories = 1,
            };
            MacroPlan plan = new()
            {
                Id = "recovery-plan",
                Name = "Recovery plan",
                Tasks = [task],
            };
            FakeRecovery recovery = new();
            RecoveringMacroScheduler scheduler = new(
                new MacroScheduler(plans),
                plans,
                recovery);
            RobloxPrivateServerLaunchTarget target =
                RobloxPrivateServerLaunchTarget.Parse(
                    "https://www.roblox.com/share?code=Test_Server_123&type=Server");
            using CancellationTokenSource cancellation = new();
            int executions = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scheduler.RunAsync(
                    plan,
                    target,
                    (_, _, _) =>
                    {
                        executions++;
                        return executions == 1
                            ? Task.FromException<ScheduledTaskResult>(
                                new TimeoutException("navigation stalled"))
                            : Task.FromResult(
                                new ScheduledTaskResult(
                                    1,
                                    0,
                                    TimeSpan.FromMinutes(2)));
                    },
                    planChanged: saved =>
                    {
                        if (saved.ProgressFor(task.Id).Completed)
                        {
                            cancellation.Cancel();
                        }
                    },
                    cancellationToken: cancellation.Token));

            Assert.Equal(2, executions);
            Assert.Equal(1, recovery.Restarts);
            MacroPlan saved =
                await plans.LoadAsync(plan.Id) ??
                throw new InvalidOperationException("Saved plan missing.");
            Assert.True(saved.ProgressFor(task.Id).Completed);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private sealed class FakeRecovery : IRobloxRuntimeRecoveryService
    {
        public int Restarts { get; private set; }

        public Task<RobloxWindow> RestartAsync(
            RobloxPrivateServerLaunchTarget target,
            IProgress<MacroProgress>? progress = null,
            Action<MacroEvent>? log = null,
            CancellationToken cancellationToken = default)
        {
            Restarts++;
            return Task.FromResult(
                new RobloxWindow(
                    1,
                    "Roblox",
                    42,
                    "RobloxPlayerBeta"));
        }
    }
}
