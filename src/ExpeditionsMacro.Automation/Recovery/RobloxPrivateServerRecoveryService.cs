using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Recovery;

public interface IRobloxRuntimeRecoveryService
{
    Task LaunchAsync(
        RobloxPrivateServerLaunchTarget target,
        CancellationToken cancellationToken = default);

    Task<RobloxWindow> RestartAsync(
        RobloxPrivateServerLaunchTarget target,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default);
}

public sealed class RobloxPrivateServerRecoveryService
    : IRobloxRuntimeRecoveryService
{
    private static readonly TimeSpan WindowDiscoveryTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DiscoveryPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IRobloxAutomation _automation;
    private readonly IRobloxProcessController _processes;

    public RobloxPrivateServerRecoveryService(
        IRobloxAutomation automation,
        IRobloxProcessController processes)
    {
        _automation = automation;
        _processes = processes;
    }

    public Task LaunchAsync(
        RobloxPrivateServerLaunchTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _processes.LaunchAsync(target.LaunchUri, cancellationToken);
    }

    public async Task<RobloxWindow> RestartAsync(
        RobloxPrivateServerLaunchTarget target,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        RobloxWindow? current = _automation.FindWindow();
        int previousProcessId = current?.ProcessId ?? 0;

        progress?.Report(new MacroProgress(
            "Recovery",
            0,
            "In-client recovery stalled. Restarting Roblox through the configured private server.",
            "roblox_restart"));
        log?.Invoke(new MacroEvent(
            DateTimeOffset.Now,
            MacroEventLevel.Warning,
            "Closing the verified Roblox player process for private-server recovery.",
            "roblox_restart"));

        await _processes.CloseAsync(current, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await _processes.LaunchAsync(target.LaunchUri, cancellationToken).ConfigureAwait(false);
        log?.Invoke(new MacroEvent(
            DateTimeOffset.Now,
            MacroEventLevel.Information,
            "Private-server launch was sent through the registered Roblox protocol.",
            "roblox_restart"));

        DateTimeOffset deadline = DateTimeOffset.UtcNow + WindowDiscoveryTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RobloxWindow? candidate = _automation.FindWindow();
            if (candidate is RobloxWindow window &&
                window.ProcessId > 0 &&
                (previousProcessId == 0 || window.ProcessId != previousProcessId))
            {
                progress?.Report(new MacroProgress(
                    "Recovery",
                    0,
                    "Roblox reopened. Resuming the current task from its saved progress.",
                    "roblox_restarted"));
                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Success,
                    $"Roblox reopened as {window.ProcessDescription}.",
                    "roblox_restarted"));
                return window;
            }
            await Task.Delay(DiscoveryPollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxSessionUnavailableException(
            "Roblox did not reopen within two minutes after the private-server launch was sent.");
    }
}
