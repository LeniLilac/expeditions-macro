using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Scheduling;

public sealed class RecoveringMacroScheduler
{
    private readonly MacroScheduler _scheduler;
    private readonly MacroPlanRepository _plans;
    private readonly IRobloxRuntimeRecoveryService _recovery;

    public RecoveringMacroScheduler(
        MacroScheduler scheduler,
        MacroPlanRepository plans,
        IRobloxRuntimeRecoveryService recovery)
    {
        _scheduler = scheduler;
        _plans = plans;
        _recovery = recovery;
    }

    public async Task RunAsync(
        MacroPlan initialPlan,
        RobloxPrivateServerLaunchTarget? restartTarget,
        Func<
            MacroTaskDefinition,
            Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>>,
            CancellationToken,
            Task<ScheduledTaskResult>> execute,
        IProgress<MacroProgress>? progress = null,
        Action<MacroPlan>? planChanged = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default)
    {
        MacroPlan plan = initialPlan;
        RobloxRestartCircuitBreaker circuitBreaker = new();
        while (true)
        {
            try
            {
                await _scheduler.RunAsync(
                    plan,
                    execute,
                    progress,
                    planChanged,
                    log,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error) when (
                restartTarget is not null &&
                RobloxRuntimeRecoveryPolicy.IsRestartCandidate(error))
            {
                if (!circuitBreaker.TryReserve(DateTimeOffset.UtcNow))
                {
                    throw new RobloxSessionUnavailableException(
                        "Roblox needed more than three restarts within ten minutes. Automatic relaunch stopped to prevent a restart loop.",
                        error);
                }

                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Warning,
                    $"Roblox runtime recovery was required: {error.Message}",
                    "roblox_restart"));
                try
                {
                    await _recovery.RestartAsync(
                        restartTarget,
                        progress,
                        log,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception restartError)
                {
                    throw new RobloxSessionUnavailableException(
                        "Roblox runtime recovery failed while reopening the configured private server.",
                        new AggregateException(error, restartError));
                }

                plan = await _plans.LoadAsync(initialPlan.Id, cancellationToken)
                    .ConfigureAwait(false) ?? plan;
            }
        }
    }
}
