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
            int recoverableFailures = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scheduler.RunAsync(
                    plan,
                    target,
                    (_, _, _) =>
                    {
                        executions++;
                        return executions == 1
                            ? Task.FromException<ScheduledTaskResult>(
                                new RobloxUiUnavailableException(
                                    "Team list did not reach its verified row."))
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
                    cancellationToken: cancellation.Token,
                    recoverableFailure: (_, _) =>
                    {
                        recoverableFailures++;
                        return Task.CompletedTask;
                    }));

            Assert.Equal(2, executions);
            Assert.Equal(1, recovery.Restarts);
            Assert.Equal(1, recoverableFailures);
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

    [Fact]
    public async Task StartupUiFailure_RestartsAndRepeatsPreflightBeforeTask()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlanRepository plans = new(paths);
            MacroTaskDefinition task = new()
            {
                Id = "story-1",
                Kind = MacroTaskKind.Story,
                PresetId = "story",
                Name = "Story",
                Priority = 1,
                TargetVictories = 1,
            };
            MacroPlan plan = new()
            {
                Id = "preflight-recovery",
                Name = "Preflight recovery",
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
            int preflights = 0;
            int executions = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scheduler.RunAsync(
                    plan,
                    target,
                    (_, _, _) =>
                    {
                        executions++;
                        return Task.FromResult(
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
                    cancellationToken: cancellation.Token,
                    prepareSession: _ =>
                    {
                        preflights++;
                        return preflights == 1
                            ? Task.FromException(
                                new RobloxUiUnavailableException(
                                    "Settings panel did not settle."))
                            : Task.CompletedTask;
                    }));

            Assert.Equal(2, preflights);
            Assert.Equal(1, executions);
            Assert.Equal(1, recovery.Restarts);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task RuntimeFailure_AfterSuccessfulPreflight_DoesNotRepeatPreflightAfterRestart()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlanRepository plans = new(paths);
            MacroTaskDefinition task = new()
            {
                Id = "story-1",
                Kind = MacroTaskKind.Story,
                PresetId = "story",
                Name = "Story",
                Priority = 1,
                TargetVictories = 1,
            };
            MacroPlan plan = new()
            {
                Id = "prepared-recovery",
                Name = "Prepared recovery",
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
            int preflights = 0;
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
                                new RobloxUiUnavailableException(
                                    "Story panel stopped responding."))
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
                    cancellationToken: cancellation.Token,
                    prepareSession: _ =>
                    {
                        preflights++;
                        return Task.CompletedTask;
                    }));

            Assert.Equal(1, preflights);
            Assert.Equal(2, executions);
            Assert.Equal(1, recovery.Restarts);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task UiFailureWithoutConfiguredRejoinTarget_StopsSafely()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlan plan = new()
            {
                Id = "no-target",
                Name = "No target",
                Tasks =
                [
                    new MacroTaskDefinition
                    {
                        Id = "raid",
                        Kind = MacroTaskKind.Raid,
                        PresetId = "raid",
                        Name = "Raid",
                    },
                ],
            };
            FakeRecovery recovery = new();
            RecoveringMacroScheduler scheduler = new(
                new MacroScheduler(
                    new MacroPlanRepository(paths)),
                new MacroPlanRepository(paths),
                recovery);

            await Assert.ThrowsAsync<RobloxUiUnavailableException>(
                () => scheduler.RunAsync(
                    plan,
                    restartTarget: null,
                    (_, _, _) =>
                        Task.FromException<ScheduledTaskResult>(
                            new RobloxUiUnavailableException(
                                "Raid panel stopped responding."))));

            Assert.Equal(0, recovery.Restarts);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private sealed class FakeRecovery : IRobloxRuntimeRecoveryService
    {
        public int Restarts { get; private set; }

        public Task LaunchAsync(
            RobloxPrivateServerLaunchTarget target,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
