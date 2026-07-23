using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
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
        StableStateTracker<string> rewardTracker = new(stableDetections);
        InactivityKeepAlive keepAlive = new();
        DateTimeOffset deadline = DateTimeOffset.UtcNow + MatchTimeout;
        while (DateTimeOffset.UtcNow < deadline)
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

            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? detectorState = detector.Classify(scores);
            string? reward = detectorState?.Equals("reward", StringComparison.OrdinalIgnoreCase) == true ? "reward" : null;
            if (rewardTracker.Update(reward) is not null)
            {
                (int rewardX, int rewardY) = detector.ActionFor("reward", frame);
                await ClickAsync(window, rewardX, rewardY, cancellationToken).ConfigureAwait(false);
                await Task.Delay(3600, cancellationToken).ConfigureAwait(false);
                rewardTracker.Reset();
                continue;
            }
            await keepAlive.TryPulseAsync((key, token) => _automation.TapLetterKeyAsync(window, key, token), cancellationToken).ConfigureAwait(false);

            if (!placed && matchRuntime.Elapsed >= TimeSpan.FromSeconds(delaySeconds))
            {
                placed = true;
                await PlayPlacementAsync(window, delayedPlacement!, story, raid, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Math.Max(200, story?.PollMilliseconds ?? raid!.PollMilliseconds), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for the {RouteLabel(story is null ? StageMode.Raid : StageMode.Story, story, raid)} result.");
    }
}
