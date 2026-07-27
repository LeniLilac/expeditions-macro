using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Events;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private async Task<EventTerminalObservation>
        RunMatchAsync(
        RobloxWindow window,
        EventPreset preset,
        PlacementModel placement,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        char cancelPlacementKey,
        bool manualPlayback,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PlacementStep> afterStart =
            manualPlayback
                ? []
                : PlacementExecutionPlan.AfterStart(
                    placement);
        int nextStep = 0;
        int terminalDetections =
            Math.Max(2, preset.StableDetections);
        StableStateTracker<string> terminal =
            new(terminalDetections);
        StableStateTracker<string> recovery =
            new(terminalDetections);
        EventTerminalRuntimeGuard runtimeGuard =
            new(terminalDetections);
        InactivityKeepAlive keepAlive = new();
        TimeSpan? matchLimit =
            MatchRuntimePolicy.ForPlacement(
                placement,
                MatchRuntimePolicy.EventLimit(
                    preset));
        while (true)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            ImageFrame frame =
                CaptureClient(window, detector);
            EventScreenMatch current =
                EventScreenDetector.DetectMatchState(
                    frame);
            string? candidate =
                current.State is
                    EventScreenState.Victory or
                    EventScreenState.Defeat
                    ? current.State.ToString()
                    : null;
            if (terminal.Update(candidate) is
                string terminalState)
            {
                return new EventTerminalObservation(
                    Enum.Parse<EventScreenState>(
                        terminalState),
                    current.Confidence,
                    frame.Clone());
            }

            string? recoveryState =
                candidate is null
                    ? detector.RootRecoveryState(frame)
                    : null;
            if (candidate is null &&
                current.State ==
                    EventScreenState.GameModeSelector)
            {
                recoveryState = "play";
            }
            string? recoverable =
                EventRunPolicy.RecoveryCandidate(
                    current.State,
                    recoveryState);
            if (recovery.Update(recoverable) is
                string stableRecovery)
            {
                throw new RobloxUiUnavailableException(
                    $"The Event match was interrupted by {EventRunPolicy.RecoveryLabel(stableRecovery)}.");
            }

            if (runtimeGuard.ShouldEnforceRuntimeLimit(
                    candidate is not null,
                    terminal.HasPendingCandidate))
            {
                MatchRuntimePolicy.ThrowIfExceeded(
                    matchRuntime.Elapsed,
                    matchLimit,
                    "Event match");
            }
            await keepAlive.TryPulseAsync(
                (key, token) =>
                    _automation.TapLetterKeyAsync(
                        window,
                        key,
                        token),
                cancellationToken).ConfigureAwait(false);
            if (candidate is null &&
                recoverable is null &&
                nextStep < afterStart.Count &&
                PlacementExecutionPlan.IsAfterStartDue(
                    afterStart[nextStep],
                    matchRuntime.Elapsed))
            {
                await PlayPlacementAsync(
                    window,
                    preset,
                    placement,
                    [afterStart[nextStep]],
                    cancelPlacementKey,
                    cancellationToken).ConfigureAwait(false);
                nextStep++;
            }
            await Task.Delay(
                Math.Max(
                    200,
                    preset.PollMilliseconds),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record EventTerminalObservation(
        EventScreenState State,
        double Confidence,
        ImageFrame Frame);

}
