using System.Diagnostics;
using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private static readonly string[] ActiveRunStates =
    [
        "defeat",
        "victory",
        "extract_confirm",
        "confirm",
        "checkpoint",
        "continue",
        "start",
        "reward",
    ];

    private async Task<RunTerminal> MonitorUntilRunEndAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        PlacementModel placement,
        IReadOnlyList<PlacementStep> initialRetryableSteps,
        IReadOnlyList<PlacementStep> afterStartSteps,
        IDetectorPack detector,
        Stopwatch matchRuntime,
        Action<int> bossesChanged,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        StableStateTracker<string> stateTracker = new(preset.StableDetections);
        StableStateTracker<string> nodeTracker = new(preset.StableDetections);
        // Recovery abandons the active run and resets its observed boss progress.
        // Confirm it independently so one UI animation frame cannot trigger rejoin.
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        InactivityKeepAlive keepAlive = new();
        List<PlacementStep> retryableSteps =
            [.. initialRetryableSteps];
        int nextAfterStartStep = 0;
        string? currentNode = null;
        int bosses = 0;
        report("Gameplay", 0, "Gameplay active. Watching node type, pauses, rewards, and run end.", null, null);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            string? stableNode = nodeTracker.Update(detector.CurrentNodeType(frame));
            if (stableNode is not null && !stableNode.Equals(currentNode, StringComparison.OrdinalIgnoreCase))
            {
                currentNode = stableNode;
                log($"Progress bar: current node is {stableNode}.", MacroEventLevel.Information, stableNode, null);
                if (stableNode == "boss")
                {
                    bosses++;
                    bossesChanged(bosses);
                    log($"Boss node count is now {bosses}.", MacroEventLevel.Information, stableNode, null);
                }
            }

            IReadOnlyDictionary<string, double> scores =
                detector.ScoreStates(
                    frame,
                    ActiveRunStates);
            string? activeCandidate =
                ExpeditionRunPolicy.PreferActiveState(
                    detector.Manifest,
                    scores,
                    detector.Classify(scores));
            string? candidate =
                activeCandidate ??
                detector.RootRecoveryState(frame);
            ThrowForStableRecovery(recoveryTracker, candidate, activeRunOnly: true);
            if (ExpeditionRunPolicy.CanEnterRecoveryDuringRun(
                    candidate))
            {
                stateTracker.Reset();
                report(
                    "Recovery",
                    0,
                    $"Detected {Label(candidate!)}; waiting for stable recovery confirmation.",
                    candidate,
                    null);
                await Task.Delay(
                    preset.PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (candidate is not null)
            {
                report(
                    "Gameplay",
                    0,
                    $"Detected {Label(candidate)}.",
                    candidate,
                    scores.TryGetValue(
                        candidate,
                        out double candidateScore)
                        ? candidateScore
                        : null);
            }
            if (candidate is null &&
                nextAfterStartStep <
                    afterStartSteps.Count &&
                PlacementExecutionPlan.IsAfterStartDue(
                    afterStartSteps[nextAfterStartStep],
                    matchRuntime.Elapsed))
            {
                PlacementStep step =
                    afterStartSteps[nextAfterStartStep];
                report(
                    "Placement",
                    5,
                    $"Placing Unit {step.UnitKey} at " +
                    $"{matchRuntime.Elapsed.TotalSeconds:F1}s " +
                    "after Start.",
                    null,
                    null);
                await PlaceStepsAsync(
                    window,
                    placement,
                    [step],
                    preset,
                    log,
                    cancelPlacementKey,
                    stepSent: null,
                    cancellationToken).ConfigureAwait(false);
                retryableSteps.Add(step);
                nextAfterStartStep++;
            }
            if (candidate is null) await keepAlive.TryPulseAsync((key, token) => _automation.TapLetterKeyAsync(window, key, token), cancellationToken).ConfigureAwait(false);
            string? state = stateTracker.Update(candidate);
            if (state is null)
            {
                if (candidate is not "defeat" and not "victory")
                {
                    MatchRuntimePolicy.ThrowIfExceeded(
                        matchRuntime.Elapsed,
                        MatchRuntimePolicy.ForPlacement(
                            placement,
                            MatchRuntimePolicy.ExpeditionLimit(
                                preset)),
                        $"Expedition map {preset.MapNumber}, difficulty " +
                        $"{preset.Difficulty}");
                }
                await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
                continue;
            }

            stateTracker.Reset();
            double score = scores[state];
            log($"Recognized {state} at {score:P0} confidence.", MacroEventLevel.Success, state, score);
            if (state is "defeat" or "victory") return new RunTerminal(state, frame.Clone());
            MatchRuntimePolicy.ThrowIfExceeded(
                matchRuntime.Elapsed,
                MatchRuntimePolicy.ForPlacement(
                    placement,
                    MatchRuntimePolicy.ExpeditionLimit(
                        preset)),
                $"Expedition map {preset.MapNumber}, difficulty " +
                $"{preset.Difficulty}");
            if (state == "reward")
            {
                report("Reward", 0, "Selecting the first available reward card.", state, score);
                await ClickActionAsync(window, detector, "reward", frame, cancellationToken).ConfigureAwait(false);
                await Task.Delay(4300, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (state == "confirm")
            {
                await DismissNodeConfirmationAsync(window, detector, preset, frame, report, log, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (state == "extract_confirm")
            {
                if (ExpeditionRunPolicy.ShouldExtract(preset, bosses))
                {
                    ExtractionTransactionState transaction = new();
                    if (!transaction.TryBegin()) throw new InvalidOperationException("Could not begin extraction confirmation handling.");
                    await ConfirmExtractionAsync(window, detector, preset, transaction, frame, report, log, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    log("Extraction confirmation appeared while extraction is disabled.", MacroEventLevel.Warning, state, score);
                    await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                }
                continue;
            }
            if (state is "start" or "checkpoint" or "continue")
            {
                if (state == "checkpoint" && ExpeditionRunPolicy.ShouldExtract(preset, bosses))
                {
                    report("Extraction", 0, $"Extraction target met after {bosses} boss node(s).", state, score);
                    await ExtractAtCheckpointAsync(window, detector, preset, frame, report, log, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                await RetryRemainingUnitsAsync(
                    window,
                    placement,
                    retryableSteps,
                    preset,
                    detector,
                    frame,
                    log,
                    cancelPlacementKey,
                    cancellationToken).ConfigureAwait(false);
                report("Transition", 0, $"Continuing from the {state} pause.", state, score);
                // Placement retries can take several seconds. Re-capture the pause so
                // the click follows its current control rather than a stale frame.
                await ClickActionAsync(window, detector, state, cancellationToken).ConfigureAwait(false);
                if (state is "checkpoint" or "continue") await WaitForConfirmationAsync(window, detector, preset, report, log, cancellationToken).ConfigureAwait(false);
                await Task.Delay(2300, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
