using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task<RunTerminal> MonitorUntilRunEndAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        PlacementModel placement,
        IDetectorPack detector,
        Action<int> bossesChanged,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        StableStateTracker<string> stateTracker = new(preset.StableDetections);
        StableStateTracker<string> nodeTracker = new(preset.StableDetections);
        // Recovery abandons the active run and resets its observed boss progress.
        // Confirm it independently so one UI animation frame cannot trigger rejoin.
        StableStateTracker<string> recoveryTracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));
        InactivityKeepAlive keepAlive = new();
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

            IReadOnlyDictionary<string, double> scores = detector.ScoreStates(frame);
            string? candidate = ExpeditionRunPolicy.PreferActiveState(detector.Manifest, scores, detector.Classify(scores));
            ThrowForStableRecovery(recoveryTracker, candidate, activeRunOnly: true);
            if (candidate is not null) report("Gameplay", 0, $"Detected {Label(candidate)}.", candidate, scores[candidate]);
            if (candidate is null) await keepAlive.TryPulseAsync((key, token) => _automation.TapLetterKeyAsync(window, key, token), cancellationToken).ConfigureAwait(false);
            string? state = stateTracker.Update(candidate);
            if (state is null)
            {
                await Task.Delay(preset.PollMilliseconds, cancellationToken).ConfigureAwait(false);
                continue;
            }

            stateTracker.Reset();
            double score = scores[state];
            log($"Recognized {state} at {score:P0} confidence.", MacroEventLevel.Success, state, score);
            if (state is "defeat" or "victory") return new RunTerminal(state, frame.Clone());
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
                await RetryRemainingUnitsAsync(window, placement, preset, detector, frame, log, cancellationToken).ConfigureAwait(false);
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
