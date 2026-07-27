using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private static readonly TimeSpan GameModeHandoffTimeout =
        TimeSpan.FromSeconds(90);

    internal enum GameModeHandoffCommand
    {
        Complete,
        ChangeGamemode,
        PressPlayKey,
        Wait,
    }

    private Task OpenPlayMenuForModeSwitchAsync(
        RobloxWindow window,
        IDetectorPack detector,
        RunTerminal terminal,
        ExpeditionPreset preset,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        CompleteGameModeHandoffAsync(
            window,
            detector,
            preset,
            playMenuKey,
            terminal.State,
            pressPlayFirst: true,
            report,
            log,
            cancellationToken);

    private async Task CompleteGameModeHandoffAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        char playMenuKey,
        string handoffState,
        bool pressPlayFirst,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        ImageFrame? initialFrame = null;
        if (pressPlayFirst)
        {
            report(
                "Handoff",
                100,
                $"Opening Play with {playMenuKey}.",
                handoffState,
                null);
            initialFrame = await OpenPlayMenuAsync(
                window,
                detector,
                preset,
                playMenuKey,
                "Handoff",
                handoffState,
                report,
                log,
                cancellationToken).ConfigureAwait(false);
        }

        ChallengeScreenMatch completed =
            await ExpeditionGameModeHandoffLoop.RunAsync(
                initialFrame,
                () => CaptureClient(window, detector),
                ChallengeScreenDetector.Detect,
                frame => ChallengeScreenDetector.ActionFor(
                    ChallengeScreenState.PostMatchPreview,
                    frame),
                async (action, attempt, match, token) =>
                {
                    report(
                        "Handoff",
                        100,
                        "Leaving the Expedition party through " +
                        $"Change Gamemode ({attempt}/" +
                        $"{ExpeditionGameModeHandoffLoop.MaximumChangeGamemodeAttempts}).",
                        "expedition_change_gamemode",
                        match.Confidence);
                    Focus(window);
                    await _automation.ClickClientAsync(
                        window,
                        action.X,
                        action.Y,
                        token).ConfigureAwait(false);
                },
                async (match, token) =>
                    await OpenPlayMenuAsync(
                        window,
                        detector,
                        preset,
                        playMenuKey,
                        "Handoff",
                        match.State.ToString(),
                        report,
                        log,
                        token).ConfigureAwait(false),
                GameModeHandoffTimeout,
                preset.StableDetections,
                preset.PollMilliseconds,
                static () => DateTimeOffset.UtcNow,
                static (duration, token) =>
                    Task.Delay(duration, token),
                cancellationToken).ConfigureAwait(false);
        log(
            "Expedition handoff reached the shared game-mode selector.",
            MacroEventLevel.Success,
            "game_mode_selector",
            completed.Confidence);
    }

    internal static GameModeHandoffCommand
        SelectGameModeHandoffCommand(
            ChallengeScreenState state) => state switch
            {
                ChallengeScreenState.GameModeSelector =>
                    GameModeHandoffCommand.Complete,
                ChallengeScreenState.Victory or
                ChallengeScreenState.Defeat =>
                    GameModeHandoffCommand.PressPlayKey,
                ChallengeScreenState.PostMatchPreview =>
                    GameModeHandoffCommand.ChangeGamemode,
                _ => GameModeHandoffCommand.Wait,
            };

    private Task<ImageFrame> OpenPlayMenuAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        char playMenuKey,
        string phase,
        string state,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey,
            () => CaptureClient(window, detector),
            (key, token) =>
                _automation.TapLetterKeyAsync(window, key, token),
            (initialFrame, timeout, token) => TryWaitForPlayMenuAsync(
                window,
                detector,
                preset,
                initialFrame,
                timeout,
                token),
            attempt => report(
                phase,
                100,
                attempt == 1
                    ? $"Opening the Play menu with {playMenuKey}."
                    : $"Retrying the {playMenuKey} Play-menu key ({attempt}/{PlayMenuNavigator.MaximumAttempts}).",
                state,
                null),
            attempt => log(
                $"The {playMenuKey} Play-menu key did not open navigation (attempt {attempt}/{PlayMenuNavigator.MaximumAttempts}).",
                MacroEventLevel.Warning,
                state,
                null),
            cancellationToken);

    private async Task<ImageFrame?> TryWaitForPlayMenuAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ImageFrame? initialFrame,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        await WaitForStablePlayMenuAsync(
            initialFrame,
            () => CaptureClient(window, detector),
            ChallengeScreenDetector.Detect,
            frame => ChallengeScreenDetector.ActionFor(
                ChallengeScreenState.PostMatchPreview,
                frame),
            timeout,
            preset.StableDetections,
            static () => DateTimeOffset.UtcNow,
            static (duration, token) =>
                Task.Delay(duration, token),
            preset.PollMilliseconds,
            cancellationToken).ConfigureAwait(false);

    internal static async Task<ImageFrame?>
        WaitForStablePlayMenuAsync(
        ImageFrame? initialFrame,
        Func<ImageFrame> capture,
        Func<ImageFrame, ChallengeScreenMatch> detect,
        Func<ImageFrame, (int X, int Y)?> locateAction,
        TimeSpan timeout,
        int stableDetections,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay,
        int pollMilliseconds,
        CancellationToken cancellationToken)
    {
        StableStateTracker<ChallengeScreenState> tracker =
            new(Math.Max(1, stableDetections));
        StableNavigationActionTracker<ChallengeScreenState>
            actionTracker =
                new(Math.Max(2, stableDetections));
        ObservationWaitBudget budget = new(
            timeout,
            Math.Max(2, stableDetections),
            utcNow);
        ImageFrame? current = initialFrame;
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate ||
                   actionTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            current ??= capture();
            ChallengeScreenMatch match = detect(current);
            ChallengeScreenState? stable = tracker.Update(
                match.State == ChallengeScreenState.PostMatchPreview
                    ? ChallengeScreenState.PostMatchPreview
                    : ChallengeScreenState.None);
            (int X, int Y)? stableAction =
                actionTracker.Update(
                    match.State ==
                        ChallengeScreenState.PostMatchPreview
                        ? match.State
                        : ChallengeScreenState.None,
                    locateAction(current));
            budget.MarkObserved();
            if (stable ==
                    ChallengeScreenState.PostMatchPreview &&
                stableAction is not null)
            {
                return current;
            }
            current = null;
            await delay(
                TimeSpan.FromMilliseconds(pollMilliseconds),
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<bool> WaitForStateAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string desired,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        DateTimeOffset? stopAfterCurrentRunUtc,
        CancellationToken cancellationToken)
    {
        StableStateTracker<string> tracker =
            new(preset.StableDetections);
        int recoveryRequired =
            ExpeditionRunPolicy.RecoveryStableDetections(preset);
        StableStateTracker<string> recoveryTracker =
            new(recoveryRequired);
        ObservationWaitBudget budget = new(
            GameModeHandoffTimeout,
            Math.Max(preset.StableDetections, recoveryRequired));
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate ||
                   recoveryTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ExpeditionRunPolicy.StopDeadlineReached(
                    DateTimeOffset.UtcNow,
                    stopAfterCurrentRunUtc))
            {
                return false;
            }
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores =
                detector.ScoreStates(frame);
            string? state = ExpeditionRunPolicy.PreferDesiredState(
                detector.Manifest,
                scores,
                desired,
                detector.Classify(scores));
            ThrowForStableRecovery(
                recoveryTracker,
                state,
                activeRunOnly: true);
            if (state is not null)
            {
                report(
                    "Waiting",
                    0,
                    $"Detected {Label(state)}.",
                    state,
                    scores[state]);
            }
            bool ready = tracker.Update(state) == desired;
            budget.MarkObserved();
            if (ready)
            {
                log(
                    $"Recognized {desired} at {scores[desired]:P0} confidence.",
                    MacroEventLevel.Success,
                    desired,
                    scores[desired]);
                return true;
            }
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Timed out waiting for {Label(desired)}.");
    }

    private async Task<bool> WaitForStateWithTimeoutAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string desired,
        TimeSpan timeout,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        StableStateTracker<string> tracker =
            new(preset.StableDetections);
        int recoveryRequired =
            ExpeditionRunPolicy.RecoveryStableDetections(preset);
        StableStateTracker<string> recoveryTracker =
            new(recoveryRequired);
        ObservationWaitBudget budget = new(
            timeout,
            Math.Max(preset.StableDetections, recoveryRequired));
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate ||
                   recoveryTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores =
                detector.ScoreStates(frame);
            string? state = ExpeditionRunPolicy.PreferDesiredState(
                detector.Manifest,
                scores,
                desired,
                detector.Classify(scores));
            ThrowForStableRecovery(
                recoveryTracker,
                state,
                activeRunOnly: true);
            if (state is not null)
            {
                report(
                    "Waiting",
                    0,
                    $"Detected {Label(state)}.",
                    state,
                    scores[state]);
            }
            bool ready = tracker.Update(state) == desired;
            budget.MarkObserved();
            if (ready) return true;
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private async Task<bool> WaitForStateToClearAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string stateToClear,
        TimeSpan timeout,
        ExpeditionPreset preset,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        // A short detector flicker cannot authorize another click.
        int clearRequired = Math.Max(3, preset.StableDetections);
        int recoveryRequired =
            ExpeditionRunPolicy.RecoveryStableDetections(preset);
        StableStateTracker<string> clearTracker =
            new(clearRequired);
        StableStateTracker<string> recoveryTracker =
            new(recoveryRequired);
        ObservationWaitBudget budget = new(
            timeout,
            Math.Max(clearRequired, recoveryRequired));
        while (budget.ShouldObserve(
                   clearTracker.HasPendingCandidate ||
                   recoveryTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            IReadOnlyDictionary<string, double> scores =
                detector.ScoreStates(frame);
            string? classified =
                ExpeditionRunPolicy.PreferActiveState(
                    detector.Manifest,
                    scores,
                    detector.Classify(scores));
            ThrowForStableRecovery(
                recoveryTracker,
                classified,
                activeRunOnly: true);
            bool visible = ExpeditionRunPolicy.IsStateDetected(
                detector.Manifest,
                scores,
                stateToClear);
            bool cleared = false;
            if (visible)
            {
                clearTracker.Reset();
                report(
                    "Waiting",
                    0,
                    $"Waiting for {Label(stateToClear)} to close.",
                    stateToClear,
                    scores[stateToClear]);
            }
            else
            {
                cleared =
                    clearTracker.Update("cleared") == "cleared";
            }
            budget.MarkObserved();
            if (cleared) return true;
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        return false;
    }
}
