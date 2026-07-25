using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task<MatchTerminal> MonitorMatchAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeMapProfile profile,
        ChallengeMapRuntimeModels models,
        IReadOnlyList<PlacementStep> fastAfterStartSteps,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        bool fast = preset.CameraPreparationMode ==
            CameraPreparationMode.FastNoAlign;
        int nextFastStep = 0;
        bool delayedPlaced = fast
            ? fastAfterStartSteps.Count == 0
            : models.DelayedPlacement is null;
        StableStateTracker<ChallengeScreenState> terminalTracker =
            new(preset.StableDetections);
        StableStateTracker<string> recoveryTracker =
            new(Math.Max(2, preset.StableDetections));
        InactivityKeepAlive keepAlive = new();

        report(
            "Gameplay",
            55,
            delayedPlaced
                ? "Match active. Watching for Victory or Defeat."
                : "Match active. Waiting for delayed placement.",
            null,
            null);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match =
                ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState candidate =
                match.State is ChallengeScreenState.Victory or
                    ChallengeScreenState.Defeat
                    ? match.State
                    : ChallengeScreenState.None;
            ChallengeScreenState? stable =
                terminalTracker.Update(candidate);
            if (stable is ChallengeScreenState.Victory or
                ChallengeScreenState.Defeat)
            {
                return new MatchTerminal(
                    stable.Value,
                    match.Confidence,
                    frame.Clone());
            }

            string? recovery = detector.RecoveryState(frame);
            if (recoveryTracker.Update(recovery ?? string.Empty) is
                    string stableRecovery &&
                !string.IsNullOrEmpty(stableRecovery))
            {
                throw new ChallengeRecoveryException(stableRecovery);
            }

            if (candidate is not ChallengeScreenState.Victory and
                not ChallengeScreenState.Defeat)
            {
                MatchRuntimePolicy.ThrowIfExceeded(
                    matchRuntime.Elapsed,
                    MatchRuntimePolicy.ChallengeLimit(),
                    "Challenge match");
            }
            if (candidate == ChallengeScreenState.None &&
                recovery is null &&
                fast &&
                !delayedPlaced &&
                PlacementExecutionPlan.IsAfterStartDue(
                    fastAfterStartSteps[nextFastStep],
                    matchRuntime.Elapsed))
            {
                PlacementStep step =
                    fastAfterStartSteps[nextFastStep];
                report(
                    "Placement",
                    65,
                    $"Placing Unit {step.UnitKey} at " +
                    $"{matchRuntime.Elapsed.TotalSeconds:F1}s " +
                    "after Start.",
                    null,
                    null);
                await PlaceAsync(
                    window,
                    preset,
                    models.PrestartPlacement!,
                    [step],
                    log,
                    cancellationToken).ConfigureAwait(false);
                nextFastStep++;
                delayedPlaced =
                    nextFastStep >=
                    fastAfterStartSteps.Count;
            }
            else if (candidate == ChallengeScreenState.None &&
                recovery is null &&
                !fast &&
                !delayedPlaced &&
                ChallengeRunPolicy.IsDelayedPlacementDue(
                    profile,
                    matchRuntime.Elapsed))
            {
                report(
                    "Placement",
                    65,
                    $"Running delayed placements after " +
                    $"{matchRuntime.Elapsed.TotalSeconds:F0} seconds.",
                    null,
                    null);
                await PlaceAsync(
                    window,
                    preset,
                    models.DelayedPlacement!,
                    log,
                    cancellationToken).ConfigureAwait(false);
                delayedPlaced = true;
            }

            await keepAlive.TryPulseAsync(
                (key, token) =>
                    _automation.TapLetterKeyAsync(window, key, token),
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
