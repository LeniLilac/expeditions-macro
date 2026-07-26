using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private Task<TerminalObservation> RunConfiguredMatchAsync(
        RobloxWindow window,
        StageRuntimeModels models,
        CameraPreparationMode cameraMode,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        int stableDetections,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        PlacementModel? afterStartModel =
            cameraMode == CameraPreparationMode.FastNoAlign
                ? models.PrestartPlacement
                : models.DelayedPlacement;
        IReadOnlyList<PlacementStep> afterStart =
            PlacementExecutionPlan.AfterStart(
                cameraMode,
                models.PrestartPlacement,
                models.DelayedPlacement);
        int delaySeconds =
            cameraMode == CameraPreparationMode.FastNoAlign
                ? 0
                : story?.DelayedPlacementSeconds ??
                    raid!.DelayedPlacementSeconds;
        return RunMatchAsync(
            window,
            afterStartModel,
            afterStart,
            cameraMode,
            delaySeconds,
            story,
            raid,
            detector,
            matchRuntime,
            stableDetections,
            cancelPlacementKey,
            cancellationToken);
    }

    private async Task<TerminalObservation> RunMatchAsync(
        RobloxWindow window,
        PlacementModel? delayedPlacement,
        IReadOnlyList<PlacementStep> delayedSteps,
        CameraPreparationMode cameraMode,
        int delaySeconds,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        int stableDetections,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        bool fast =
            cameraMode ==
            CameraPreparationMode.FastNoAlign;
        int nextFastStep = 0;
        bool placed = delayedPlacement is null ||
            delayedSteps.Count == 0;
        StableStateTracker<string> terminalTracker = new(stableDetections);
        StableStateTracker<string> recoveryTracker = new(stableDetections);
        RaidDropDismissalTracker dropDismissal = new(raid);
        InactivityKeepAlive keepAlive = new();
        TimeSpan? matchLimit =
            MatchRuntimePolicy.StageLimit(story, raid);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch state =
                StageScreenDetector.DetectMatchState(frame);
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
            if (terminalCandidate is null &&
                recovery is null &&
                fast &&
                !placed &&
                PlacementExecutionPlan.IsAfterStartDue(
                    delayedSteps[nextFastStep],
                    matchRuntime.Elapsed))
            {
                PlacementStep step =
                    delayedSteps[nextFastStep];
                await PlayPlacementAsync(
                    window,
                    delayedPlacement!,
                    [step],
                    story,
                    raid,
                    cancelPlacementKey,
                    cancellationToken).ConfigureAwait(false);
                nextFastStep++;
                placed =
                    nextFastStep >= delayedSteps.Count;
                placementCompletedThisIteration = true;
            }
            else if (terminalCandidate is null &&
                recovery is null &&
                !fast &&
                !placed &&
                matchRuntime.Elapsed >= TimeSpan.FromSeconds(delaySeconds))
            {
                placed = true;
                await PlayPlacementAsync(
                    window,
                    delayedPlacement!,
                    delayedSteps,
                    story,
                    raid,
                    cancelPlacementKey,
                    cancellationToken).ConfigureAwait(false);
                placementCompletedThisIteration = true;
            }

            if (dropDismissal.Enabled &&
                placed &&
                !placementCompletedThisIteration &&
                dropDismissal.Observe(
                    afterStartPlacementComplete: true,
                    gameplayHudVisible:
                        StageGameplayHudDetector.Detect(frame).Visible,
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
}
