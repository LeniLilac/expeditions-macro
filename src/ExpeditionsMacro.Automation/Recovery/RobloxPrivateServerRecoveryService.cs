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

    Task<RobloxWindow> RestartForStartupAsync(
        RobloxPrivateServerLaunchTarget target,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default) =>
        RestartAsync(
            target,
            progress,
            log,
            cancellationToken);

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
    private static readonly TimeSpan RestartReadinessTimeout =
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
        CancellationToken cancellationToken = default) =>
        await RestartCoreAsync(
            target,
            startupPreflightReadiness: false,
            progress,
            log,
            cancellationToken).ConfigureAwait(false);

    public async Task<RobloxWindow> RestartForStartupAsync(
        RobloxPrivateServerLaunchTarget target,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default) =>
        await RestartCoreAsync(
            target,
            startupPreflightReadiness: true,
            progress,
            log,
            cancellationToken).ConfigureAwait(false);

    private async Task<RobloxWindow> RestartCoreAsync(
        RobloxPrivateServerLaunchTarget target,
        bool startupPreflightReadiness,
        IProgress<MacroProgress>? progress,
        Action<MacroEvent>? log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        RobloxWindow? current = _automation.FindWindow();
        int previousProcessId = current?.ProcessId ?? 0;

        progress?.Report(new MacroProgress(
            "Recovery",
            0,
            "Restarting Roblox through the configured private server.",
            "roblox_restart"));
        log?.Invoke(new MacroEvent(
            DateTimeOffset.Now,
            MacroEventLevel.Information,
            current is null
                ? "No Roblox player process was open; launching the configured private server."
                : "Closing the verified Roblox player process before private-server launch.",
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
                    startupPreflightReadiness
                        ? "Roblox reopened. Waiting for the game view to finish loading before startup checks."
                        : "Roblox reopened. Waiting for the lobby to finish loading.",
                    "roblox_restarted"));
                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Information,
                    startupPreflightReadiness
                        ? $"Roblox reopened as {window.ProcessDescription}; waiting for a stable startup-check view."
                        : $"Roblox reopened as {window.ProcessDescription}; waiting for stable lobby frames.",
                    "roblox_restarted"));
                IDetectorPack detector =
                    await _detectorProvider(cancellationToken)
                        .ConfigureAwait(false);
                if (startupPreflightReadiness)
                {
                    DetectorStateDefinition lobby =
                        detector.Manifest.States.Single(
                            state => state.Name.Equals(
                                "lobby",
                                StringComparison.OrdinalIgnoreCase));
                    await RobloxStartupReadinessGate.WaitAsync(
                        token => ObserveStartupReadinessAsync(
                            window,
                            detector,
                            lobby.Threshold,
                            token),
                        RestartReadinessTimeout,
                        DiscoveryPollInterval,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await RobloxLobbyReadinessGate.WaitAsync(
                        token => CaptureCanonicalClientAsync(
                            window,
                            detector,
                            token),
                        detector.RecoveryState,
                        RestartReadinessTimeout,
                        DiscoveryPollInterval,
                        cancellationToken).ConfigureAwait(false);
                }
                progress?.Report(new MacroProgress(
                    "Recovery",
                    0,
                    startupPreflightReadiness
                        ? "Roblox finished loading. Starting the configured startup checks."
                        : "Roblox reached the lobby. Resuming the current task from its saved progress.",
                    startupPreflightReadiness
                        ? "startup_ready"
                        : "lobby"));
                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Success,
                    startupPreflightReadiness
                        ? "Roblox reached a stable Lobby-shaped view for startup checks; strict Lobby verification remains pending."
                        : "Roblox reached a stable lobby after private-server restart.",
                    startupPreflightReadiness
                        ? "startup_ready"
                        : "lobby"));
                RobloxWindow? readyWindow = _automation.FindWindow();
                if (readyWindow is not RobloxWindow active ||
                    active.ProcessId != window.ProcessId)
                {
                    throw new RobloxSessionUnavailableException(
                        startupPreflightReadiness
                            ? "Roblox finished loading but its active window changed before startup checks could begin."
                            : "Roblox reached the lobby but its active window changed before recovery could resume.");
                }

                return active;
            }
            await Task.Delay(DiscoveryPollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxSessionUnavailableException(
            "Roblox did not reopen within two minutes after the private-server launch was sent.");
    }

    private async Task<RobloxStartupReadinessObservation>
        ObserveStartupReadinessAsync(
        RobloxWindow window,
        IDetectorPack detector,
        double lobbyThreshold,
        CancellationToken cancellationToken)
    {
        ImageFrame frame = await CaptureCanonicalClientAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, double> scores =
            detector.ScoreStates(frame);
        double lobbyScore =
            scores.GetValueOrDefault("lobby");
        double strongestOther = scores
            .Where(pair => !pair.Key.Equals(
                "lobby",
                StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .DefaultIfEmpty(0)
            .Max();
        return new RobloxStartupReadinessObservation(
            detector.Classify(scores),
            lobbyScore,
            strongestOther,
            lobbyThreshold);
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
