using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
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
    private static readonly TimeSpan LobbyReadinessTimeout =
        TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DiscoveryPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ResizeSettleDelay =
        TimeSpan.FromMilliseconds(250);

    private readonly IRobloxAutomation _automation;
    private readonly IRobloxProcessController _processes;
    private readonly Func<CancellationToken, Task<IDetectorPack>>
        _detectorProvider;

    public RobloxPrivateServerRecoveryService(
        IRobloxAutomation automation,
        IRobloxProcessController processes,
        Func<CancellationToken, Task<IDetectorPack>>
            detectorProvider)
    {
        _automation = automation;
        _processes = processes;
        _detectorProvider = detectorProvider;
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
                    "Roblox reopened. Waiting for the lobby to finish loading.",
                    "roblox_restarted"));
                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Information,
                    $"Roblox reopened as {window.ProcessDescription}; waiting for stable lobby frames.",
                    "roblox_restarted"));
                IDetectorPack detector =
                    await _detectorProvider(cancellationToken)
                        .ConfigureAwait(false);
                await RobloxLobbyReadinessGate.WaitAsync(
                    token => CaptureCanonicalClientAsync(
                        window,
                        detector,
                        token),
                    detector.RecoveryState,
                    LobbyReadinessTimeout,
                    DiscoveryPollInterval,
                    cancellationToken).ConfigureAwait(false);
                progress?.Report(new MacroProgress(
                    "Recovery",
                    0,
                    "Roblox reached the lobby. Resuming the current task from its saved progress.",
                    "lobby"));
                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Success,
                    "Roblox reached a stable lobby after private-server restart.",
                    "lobby"));
                RobloxWindow? readyWindow = _automation.FindWindow();
                if (readyWindow is not RobloxWindow active ||
                    active.ProcessId != window.ProcessId)
                {
                    throw new RobloxSessionUnavailableException(
                        "Roblox reached the lobby but its active window changed before recovery could resume.");
                }

                return active;
            }
            await Task.Delay(DiscoveryPollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxSessionUnavailableException(
            "Roblox did not reopen within two minutes after the private-server launch was sent.");
    }

    private async Task<ImageFrame> CaptureCanonicalClientAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        RobloxWindow? current = _automation.FindWindow();
        if (current is not RobloxWindow active ||
            active.ProcessId != window.ProcessId)
        {
            throw new InvalidOperationException(
                "The relaunched Roblox window changed before the lobby became ready.");
        }

        if (!_automation.Focus(active))
        {
            throw new InvalidOperationException(
                "The relaunched Roblox window could not be focused.");
        }

        ClientBounds bounds = _automation.GetClientBounds(active);
        if (bounds.Width != detector.Manifest.ClientWidth ||
            bounds.Height != detector.Manifest.ClientHeight)
        {
            await _automation.ResizeClientAsync(
                active,
                detector.Manifest.ClientWidth,
                detector.Manifest.ClientHeight,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                ResizeSettleDelay,
                cancellationToken).ConfigureAwait(false);
            bounds = _automation.GetClientBounds(active);
        }

        if (bounds.Width != detector.Manifest.ClientWidth ||
            bounds.Height != detector.Manifest.ClientHeight)
        {
            throw new InvalidOperationException(
                "The relaunched Roblox window has not reached the canonical client size.");
        }

        return _automation.CaptureClient(active);
    }
}
