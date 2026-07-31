using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private Task<TerminalObservation> RunConfiguredMatchAsync(
        RobloxWindow window,
        StageRuntimeModels models,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        int stableDetections,
        char cancelPlacementKey,
        bool manualPlayback,
        StageWaveObjective? waveObjective,
        CancellationToken cancellationToken)
    {
        PlacementModel? afterStartModel =
            models.Placement;
        IReadOnlyList<PlacementStep> afterStart =
            manualPlayback
                ? []
                : PlacementExecutionPlan.AfterStart(
                    models.Placement);
        return RunMatchAsync(
            window,
            afterStartModel,
            afterStart,
            story,
            raid,
            detector,
            matchRuntime,
            stableDetections,
            cancelPlacementKey,
            models.Placement,
            waveObjective,
            cancellationToken);
    }

    private async Task<TerminalObservation> RunMatchAsync(
        RobloxWindow window,
        PlacementModel? delayedPlacement,
        IReadOnlyList<PlacementStep> delayedSteps,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        int stableDetections,
        char cancelPlacementKey,
        PlacementModel? runtimePolicyPlacement,
        StageWaveObjective? waveObjective,
        CancellationToken cancellationToken)
    {
        int nextFastStep = 0;
        bool placed = delayedPlacement is null ||
            delayedSteps.Count == 0;
        StableStateTracker<string> terminalTracker = new(stableDetections);
        StableStateTracker<string> recoveryTracker = new(stableDetections);
        RaidDropDismissalTracker dropDismissal = new(raid);
        InactivityKeepAlive keepAlive = new();
        BountyWaveCompletionTracker? waveTracker =
            waveObjective is null
                ? null
                : new(
                    waveObjective);
        TimeSpan? matchLimit =
            MatchRuntimePolicy.StageLimit(story, raid);
        if (runtimePolicyPlacement is not null)
        {
            matchLimit =
                MatchRuntimePolicy.ForPlacement(
                    runtimePolicyPlacement,
                    matchLimit);
        }
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch state =
                StageScreenDetector.DetectMatchState(frame);
            StageGameplayHudMatch gameplayHud =
                StageGameplayHudDetector.Detect(frame);
            int? observedWave =
                waveTracker is null ||
                !gameplayHud.Visible
                    ? null
                    : DetectOwnedBountyWave(
                        frame,
                        gameplayHud);
            if (waveTracker?.Observe(
                    observedWave) == true)
            {
                return new TerminalObservation(
                    StageScreenState.None,
                    1,
                    frame.Clone(),
                    observedWave);
            }
            string? terminalCandidate = state.State switch
            {
                StageScreenState.Victory => "victory",
                StageScreenState.Defeat => "defeat",
                _ => null,
            };
            if (terminalTracker.Update(terminalCandidate) is string terminal)
            {
                return new TerminalObservation(
                    terminal == "victory" ? StageScreenState.Victory : StageScreenState.Defeat,
                    state.Confidence,
                    frame.Clone());
            }

            string? recovery =
                terminalCandidate is null
                    ? detector.RootRecoveryState(frame)
                    : null;
            if (terminalCandidate is null &&
                state.State ==
                    StageScreenState.GameModeSelector)
            {
                recovery = "play";
            }
            if (recoveryTracker.Update(IsRootRecovery(recovery) || recovery == "play" ? recovery : null) is string stableRecovery)
            {
                throw new StageRecoveryException(stableRecovery);
            }

            if (terminalCandidate is null)
            {
                MatchRuntimePolicy.ThrowIfExceeded(
                    matchRuntime.Elapsed,
                    matchLimit,
                    story is null ? "Raid match" : "Story match");
            }
            await keepAlive.TryPulseAsync((key, token) => _automation.TapLetterKeyAsync(window, key, token), cancellationToken).ConfigureAwait(false);

            bool placementCompletedThisIteration = false;
            IReadOnlyList<PlacementStep> dueSteps =
                terminalCandidate is null &&
                recovery is null &&
                !placed
                    ? PlacementExecutionPlan
                        .DueAfterStartBatch(
                            delayedSteps,
                            nextFastStep,
                            matchRuntime.Elapsed)
                    : [];
            if (dueSteps.Count > 0)
            {
                await PlayPlacementAsync(
                    window,
                    delayedPlacement!,
                    dueSteps,
                    story,
                    raid,
                    cancelPlacementKey,
                    cancellationToken).ConfigureAwait(false);
                nextFastStep += dueSteps.Count;
                placed =
                    nextFastStep >= delayedSteps.Count;
                placementCompletedThisIteration = true;
            }
            if (dropDismissal.Enabled &&
                placed &&
                !placementCompletedThisIteration &&
                dropDismissal.Observe(
                    afterStartPlacementComplete: true,
                    gameplayHudVisible:
                        gameplayHud.Visible,
                    terminalCandidateVisible: terminalCandidate is not null,
                    DateTimeOffset.UtcNow))
            {
                await ClickAsync(
                    window,
                    RaidDropDismissalTracker.ActionX,
                    RaidDropDismissalTracker.ActionY,
                    cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Math.Max(200, story?.PollMilliseconds ?? raid!.PollMilliseconds), cancellationToken).ConfigureAwait(false);
        }
    }

    internal static int? DetectOwnedBountyWave(
        ImageFrame frame) =>
        DetectOwnedBountyWave(
            frame,
            StageGameplayHudDetector
                .Detect(frame));

    private static int? DetectOwnedBountyWave(
        ImageFrame frame,
        StageGameplayHudMatch gameplayHud) =>
        gameplayHud.Visible
            ? WaveCounterRecognizer
                .Detect(frame)
                ?.Wave
            : null;
}
