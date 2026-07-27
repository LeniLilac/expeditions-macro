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
    private static readonly string[] MatchNavigationRecoveryStates =
    [
        "map_select",
        "map_preview",
    ];

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
        char cancelPlacementKey,
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
                ChallengeScreenDetector.DetectMatchState(
                    frame);
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

            string? recovery =
                candidate is ChallengeScreenState.Victory or
                    ChallengeScreenState.Defeat
                    ? null
                    : DetectMatchRecoveryState(
                        detector,
                        frame,
                        match.State);
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
                    RuntimeLimit(
                        models),
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
                    cancelPlacementKey,
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
                    cancelPlacementKey,
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

    private static TimeSpan RuntimeLimit(
        ChallengeMapRuntimeModels models)
    {
        PlacementModel? placement =
            models.PrestartPlacement ??
            models.DelayedPlacement;
        return placement is null
            ? MatchRuntimePolicy.ChallengeLimit()
            : MatchRuntimePolicy.ForPlacement(
                placement,
                MatchRuntimePolicy.ChallengeLimit())!.Value;
    }

    internal static string? DetectMatchRecoveryState(
        IDetectorPack detector,
        ImageFrame frame,
        ChallengeScreenState matchState)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(frame);
        string? root = detector.RootRecoveryState(frame);
        if (root?.Equals(
                "afk",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return root;
        }

        IReadOnlyDictionary<string, double> navigationScores =
            detector.ScoreStates(
                frame,
                MatchNavigationRecoveryStates);
        if (ExpeditionRunPolicy.IsStateDetected(
                detector.Manifest,
                navigationScores,
                "map_select"))
        {
            return "map_select";
        }
        if (root is not null)
        {
            return root;
        }
        if (matchState ==
            ChallengeScreenState.GameModeSelector)
        {
            return "play";
        }
        return ExpeditionRunPolicy.IsStateDetected(
                detector.Manifest,
                navigationScores,
                "map_preview")
            ? "map_preview"
            : null;
    }
}
