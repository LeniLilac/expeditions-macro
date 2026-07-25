using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task<TerminalObservation> RunMatchAsync(
        RobloxWindow window,
        PlacementModel? delayedPlacement,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        int stableDetections,
        CancellationToken cancellationToken)
    {
        int delaySeconds = story?.DelayedPlacementSeconds ?? raid!.DelayedPlacementSeconds;
        bool placed = delayedPlacement is null;
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
            StageScreenMatch state = StageScreenDetector.Detect(frame);
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

            string? recovery = detector.RecoveryState(frame);
            if (state.State == StageScreenState.GameModeSelector) recovery = "play";
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
            if (!placed &&
                matchRuntime.Elapsed >= TimeSpan.FromSeconds(delaySeconds))
            {
                placed = true;
                await PlayPlacementAsync(
                    window,
                    delayedPlacement!,
                    story,
                    raid,
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
