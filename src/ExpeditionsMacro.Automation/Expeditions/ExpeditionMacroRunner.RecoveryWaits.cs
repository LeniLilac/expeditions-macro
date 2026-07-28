using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task ConfigureMapAndDifficultyAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        IDetectorPack detector,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            ImageFrame current =
                CaptureClient(window, detector);
            int? selected =
                detector.SelectedMap(current);
            if (selected == preset.MapNumber)
            {
                break;
            }

            report(
                "Recovery",
                0,
                $"Selecting Expedition map {preset.MapNumber}.",
                "map_select",
                null);
            await ClickActionAsync(
                window,
                detector,
                $"map_{preset.MapNumber}",
                cancellationToken).ConfigureAwait(false);
            if (await WaitForSelectionAsync(
                    window,
                    detector,
                    value => detector.SelectedMap(value),
                    preset.MapNumber,
                    TimeSpan.FromSeconds(3),
                    preset,
                    cancellationToken).ConfigureAwait(false))
            {
                break;
            }

            log(
                $"Map selection did not register (attempt {attempt}/3).",
                MacroEventLevel.Warning,
                "map_select",
                null);
            if (attempt == 3)
            {
                throw new InvalidOperationException(
                    $"Map {preset.MapNumber} could not be selected. It may still be locked.");
            }
        }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            report(
                "Recovery",
                0,
                $"Selecting difficulty {preset.Difficulty}.",
                "map_select",
                null);
            ImageFrame current =
                CaptureClient(window, detector);
            int? selected =
                detector.SelectedDifficulty(current);
            if (selected == preset.Difficulty)
            {
                return;
            }

            if (selected is null)
            {
                for (int index = 0; index < 3; index++)
                {
                    await ClickActionAsync(
                        window,
                        detector,
                        "difficulty_minus",
                        cancellationToken).ConfigureAwait(false);
                    await Task.Delay(
                        300,
                        cancellationToken).ConfigureAwait(false);
                }
                for (int index = 1;
                     index < preset.Difficulty;
                     index++)
                {
                    await ClickActionAsync(
                        window,
                        detector,
                        "difficulty_plus",
                        cancellationToken).ConfigureAwait(false);
                    await Task.Delay(
                        350,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                string action =
                    selected < preset.Difficulty
                        ? "difficulty_plus"
                        : "difficulty_minus";
                for (int index = 0;
                     index < Math.Abs(
                         preset.Difficulty -
                         selected.Value);
                     index++)
                {
                    await ClickActionAsync(
                        window,
                        detector,
                        action,
                        cancellationToken).ConfigureAwait(false);
                    await Task.Delay(
                        350,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            // One positive active-difficulty match is sufficient. Transition
            // frames are ignored until the animation settles.
            if (await WaitForSelectionAsync(
                    window,
                    detector,
                    value => detector.SelectedDifficulty(value),
                    preset.Difficulty,
                    TimeSpan.FromSeconds(4.5),
                    preset,
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            log(
                $"Difficulty selection did not register (attempt {attempt}/3).",
                MacroEventLevel.Warning,
                "map_select",
                null);
        }

        throw new RobloxUiUnavailableException(
            $"Difficulty {preset.Difficulty} could not be selected.");
    }

    private async Task<bool> WaitForSelectionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        Func<ImageFrame, int?> selector,
        int target,
        TimeSpan timeout,
        ExpeditionPreset preset,
        CancellationToken cancellationToken)
    {
        int required =
            ExpeditionRunPolicy.RecoveryStableDetections(preset);
        StableStateTracker<string> recoveryTracker =
            new(required);
        ObservationWaitBudget budget =
            new(timeout, required);
        while (budget.ShouldObserve(
                   recoveryTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame =
                CaptureClient(window, detector);
            string? recovery =
                detector.RecoveryState(frame);
            ThrowForStableRecovery(
                recoveryTracker,
                recovery == "map_select"
                    ? null
                    : recovery);
            bool selected =
                selector(frame) == target;
            budget.MarkObserved();
            if (selected)
            {
                return true;
            }
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private async Task<string?> WaitForRecoveryChangeAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string excluded,
        TimeSpan timeout,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken,
        string? initialState = null)
    {
        int stableDetections =
            ExpeditionRunPolicy.RecoveryStableDetections(preset);
        StableStateTracker<string> tracker =
            new(stableDetections);
        ObservationWaitBudget budget =
            new(timeout, stableDetections);
        if (!string.IsNullOrWhiteSpace(initialState))
        {
            _ = tracker.Update(initialState);
            budget.MarkObserved();
        }
        bool allowStandaloneContinue =
            excluded.Equals(
                "map_preview",
                StringComparison.OrdinalIgnoreCase);
        bool captureErrorReported = false;
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ImageFrame frame =
                    CaptureClient(window, detector);
                IReadOnlyDictionary<string, double> scores =
                    detector.ScoreStates(frame);
                string? state =
                    ExpeditionRunPolicy.RecoveryTransition(
                        detector.Manifest,
                        scores,
                        detector.RecoveryState(frame),
                        allowStandaloneContinue);
                if (state is not null &&
                    scores.TryGetValue(
                        state,
                        out double score))
                {
                    report(
                        "Recovery",
                        0,
                        $"Detected {Label(state)}.",
                        state,
                        score);
                }
                bool acceptedTransition =
                    RecoveryStates.Contains(
                        state ?? string.Empty) ||
                    state == "start" ||
                    (allowStandaloneContinue &&
                        state == "continue");
                if (!acceptedTransition)
                {
                    tracker.Reset();
                }
                else
                {
                    string? stable =
                        tracker.Update(state);
                    if (stable is not null &&
                        !stable.Equals(
                            excluded,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return stable;
                    }
                }
                budget.MarkObserved();
            }
            catch (Exception error)
                when (error is not OperationCanceledException)
            {
                if (!captureErrorReported)
                {
                    log(
                        $"Waiting for Roblox during recovery: {error.Message}",
                        MacroEventLevel.Warning,
                        null,
                        null);
                    captureErrorReported = true;
                }
                budget.MarkObserved();
            }
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<string?> ProbeStableRecoveryStateAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        bool allowNavigationEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            string? first =
                RecoveryProbeCandidate(
                    CaptureClient(window, detector),
                    detector,
                    allowNavigationEntry);
            if (first is null)
            {
                return null;
            }
            int required =
                ExpeditionRunPolicy.RecoveryStableDetections(preset);
            for (int observation = 1;
                 observation < required;
                 observation++)
            {
                await Task.Delay(
                    preset.PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
                string? current =
                    RecoveryProbeCandidate(
                        CaptureClient(window, detector),
                        detector,
                        allowNavigationEntry);
                if (!string.Equals(
                        first,
                        current,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            return first;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task ThrowIfRecoveryAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        CancellationToken cancellationToken)
    {
        string? state =
            await ProbeStableRecoveryStateAsync(
                window,
                detector,
                preset,
                allowNavigationEntry: false,
                cancellationToken).ConfigureAwait(false);
        if (state is not null)
        {
            throw new RecoveryNeededException(state);
        }
    }

    private static string? RecoveryProbeCandidate(
        ImageFrame frame,
        IDetectorPack detector,
        bool allowNavigationEntry)
    {
        IReadOnlyDictionary<string, double> scores =
            detector.ScoreStates(frame);
        if (ExpeditionRunPolicy.IsStateDetected(
                detector.Manifest,
                scores,
                "start"))
        {
            return null;
        }
        string? state =
            detector.RecoveryState(frame);
        if (state is not null &&
            (allowNavigationEntry ||
                ExpeditionRunPolicy
                    .CanEnterRecoveryDuringRun(state)))
        {
            return state;
        }
        if (!allowNavigationEntry)
        {
            return null;
        }
        ChallengeScreenState navigation =
            ChallengeScreenDetector.Detect(frame).State;
        return navigation is
            ChallengeScreenState.Victory or
            ChallengeScreenState.Defeat or
            ChallengeScreenState.PostMatchPreview
            ? "post_match_party"
            : null;
    }

    private static void ThrowForStableRecovery(
        StableStateTracker<string> tracker,
        string? state,
        bool activeRunOnly = false)
    {
        if (state is null ||
            !RecoveryStates.Contains(state) ||
            (activeRunOnly &&
                !ExpeditionRunPolicy
                    .CanEnterRecoveryDuringRun(state)))
        {
            tracker.Reset();
            return;
        }
        string? stable =
            tracker.Update(state);
        if (stable is not null)
        {
            throw new RecoveryNeededException(stable);
        }
    }
}
