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
        CancellationToken cancellationToken = default,
        Func<Exception, CancellationToken, Task>?
            recoverableFailure = null,
        Func<CancellationToken, Task>?
            prepareSession = null,
        RobloxPrivateServerLaunchTarget?
            startupRestartTarget = null)
    {
        MacroPlan plan = initialPlan;
        RobloxRestartCircuitBreaker circuitBreaker = new();
        bool operationPrepared = prepareSession is null;
        if (startupRestartTarget is not null)
        {
            log?.Invoke(new MacroEvent(
                DateTimeOffset.Now,
                MacroEventLevel.Information,
                "Macro start is establishing a fresh private-server session before startup checks.",
                "roblox_startup_restart"));
            if (operationPrepared)
            {
                await _recovery.RestartAsync(
                    startupRestartTarget,
                    progress,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // GB-028: noncanonical UI Scale can keep strict Lobby
                // recognition below threshold until preflight corrects it.
                await _recovery.RestartForStartupAsync(
                    startupRestartTarget,
                    progress,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        while (true)
        {
            try
            {
                if (!operationPrepared && prepareSession is not null)
                {
                    await prepareSession(
                        cancellationToken).ConfigureAwait(false);
                    operationPrepared = true;
                }
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
                if (recoverableFailure is not null)
                {
                    try
                    {
                        await recoverableFailure(
                            error,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception diagnosticError)
                    {
                        log?.Invoke(new MacroEvent(
                            DateTimeOffset.Now,
                            MacroEventLevel.Warning,
                            "Recoverable-failure diagnostics could not " +
                            $"finish before Roblox restart: {diagnosticError.Message}",
                            "roblox_restart_diagnostics"));
                    }
                }
                try
                {
                    if (operationPrepared)
                    {
                        await _recovery.RestartAsync(
                            restartTarget,
                            progress,
                            log,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // A failed preflight still needs the scale-tolerant
                        // startup handoff; task/runtime retries never do.
                        await _recovery.RestartForStartupAsync(
                            restartTarget,
                            progress,
                            log,
                            cancellationToken).ConfigureAwait(false);
                    }
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
