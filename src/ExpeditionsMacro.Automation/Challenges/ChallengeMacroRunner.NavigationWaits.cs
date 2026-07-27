using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task<(
        ImageFrame Frame,
        ChallengeScreenMatch Match)> OpenChallengeTypeAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeType type,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            (int x, int y) =
                ChallengeScreenDetector.ActionForType(type);
            await ClickAsync(
                window,
                x,
                y,
                cancellationToken).ConfigureAwait(false);
            StableStateTracker<ChallengeScreenState> stateTracker =
                new(preset.StableDetections);
            StableNavigationActionTracker<ChallengeScreenState>
                actionTracker =
                    new(Math.Max(2, preset.StableDetections));
            ObservationWaitBudget budget = new(
                TimeSpan.FromSeconds(5),
                Math.Max(2, preset.StableDetections));
            while (budget.ShouldObserve(
                       stateTracker.HasPendingCandidate ||
                       actionTracker.HasPendingCandidate))
            {
                ImageFrame frame = CaptureClient(window, detector);
                ChallengeScreenMatch match =
                    ChallengeScreenDetector.Detect(frame);
                budget.MarkObserved();
                ChallengeScreenState candidate =
                    match.State is
                        ChallengeScreenState.ChallengeAvailable or
                        ChallengeScreenState.ChallengeCooldown
                        ? match.State
                        : ChallengeScreenState.None;
                ChallengeScreenState? stableState =
                    stateTracker.Update(candidate);
                (int X, int Y)? stableAction =
                    actionTracker.Update(
                        candidate ==
                            ChallengeScreenState.ChallengeAvailable
                            ? candidate
                            : ChallengeScreenState.None,
                        MatchAction(match));
                bool ready =
                    stableState ==
                        ChallengeScreenState.ChallengeCooldown ||
                    stableState ==
                        ChallengeScreenState.ChallengeAvailable &&
                    stableAction is not null;
                if (ready)
                {
                    ChallengeScreenState stable =
                        stableState!.Value;
                    report(
                        "Challenge selection",
                        15,
                        stable ==
                            ChallengeScreenState.ChallengeAvailable
                            ? "Select Stage is available."
                            : "Challenge is on cooldown.",
                        stable.ToString(),
                        match.Confidence);
                    return (
                        frame,
                        match with
                        {
                            State = stable,
                            ActionX = stableAction?.X ??
                                match.ActionX,
                            ActionY = stableAction?.Y ??
                                match.ActionY,
                        });
                }
                await Task.Delay(
                    preset.PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
            report(
                "Challenge selection",
                10,
                $"Selector click did not open Challenge {type} (attempt {attempt}/3).",
                null,
                null);
        }
        throw new RobloxUiUnavailableException(
            $"Challenge {Label(type)} could not be opened from the fixed selector row.");
    }

    private async Task<ImageFrame?> TryWaitForScreenAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeScreenState desired,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken,
        bool initialDesiredObservation = false,
        ImageFrame? initialFrame = null)
    {
        if (RequiresStableChallengeAction(desired))
        {
            (ImageFrame Frame, ChallengeScreenMatch Match)? action =
                await TryWaitForActionAsync(
                        window,
                        preset,
                        detector,
                        desired,
                        timeout,
                        report,
                        cancellationToken,
                        initialFrame is null
                            ? null
                            : (
                                initialFrame,
                                ChallengeScreenDetector
                                    .Detect(initialFrame)))
                    .ConfigureAwait(false);
            return action?.Frame;
        }

        StableStateTracker<ChallengeScreenState> stateTracker =
            new(preset.StableDetections);
        ObservationWaitBudget budget = new(
            timeout,
            preset.StableDetections);
        if (initialDesiredObservation)
        {
            _ = stateTracker.Update(desired);
            budget.MarkObserved();
        }
        while (budget.ShouldObserve(
                   stateTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            ChallengeScreenMatch match =
                ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState candidate =
                match.State == desired
                    ? desired
                    : ChallengeScreenState.None;
            ChallengeScreenState? stable =
                stateTracker.Update(candidate);
            if (stable == desired)
            {
                return frame;
            }
            if (match.State != ChallengeScreenState.None)
            {
                report(
                    "Waiting",
                    0,
                    $"Detected {Label(match.State)}.",
                    match.State.ToString(),
                    match.Confidence);
            }
            budget.MarkObserved();
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private Task<(ImageFrame Frame, ChallengeScreenMatch Match)?>
        TryWaitForActionAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeScreenState desired,
        TimeSpan timeout,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken,
        (ImageFrame Frame, ChallengeScreenMatch Match)?
            initialObservation = null) =>
        WaitForStableActionAsync(
            desired,
            preset.StableDetections,
            () =>
            {
                ImageFrame frame =
                    CaptureClient(window, detector);
                return (
                    frame,
                    ChallengeScreenDetector.Detect(frame));
            },
            timeout,
            preset.PollMilliseconds,
            match =>
            {
                if (match.State !=
                    ChallengeScreenState.None)
                {
                    report(
                        "Waiting",
                        0,
                        $"Detected {Label(match.State)}.",
                        match.State.ToString(),
                        match.Confidence);
                }
            },
            cancellationToken,
            initialObservation: initialObservation);

    private async Task<(ImageFrame Frame, ChallengeScreenMatch Match)>
        WaitForPreviewStartAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        (ImageFrame Frame, ChallengeScreenMatch Match)? observation =
            await TryWaitForActionAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.PreviewReady,
                    TimeSpan.FromSeconds(15),
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        return observation ??
            throw new RobloxUiUnavailableException(
                "The Challenge preview did not expose a stable live Start button within 15 seconds.");
    }

    internal static async Task<(
        ImageFrame Frame,
        ChallengeScreenMatch Match)?>
        WaitForStableActionAsync(
        ChallengeScreenState desired,
        int stableDetections,
        Func<(ImageFrame Frame, ChallengeScreenMatch Match)>
            observe,
        TimeSpan timeout,
        int pollMilliseconds,
        Action<ChallengeScreenMatch>? observed,
        CancellationToken cancellationToken,
        Func<DateTimeOffset>? utcNow = null,
        Func<int, CancellationToken, Task>? delay = null,
        (ImageFrame Frame, ChallengeScreenMatch Match)?
            initialObservation = null)
    {
        ArgumentNullException.ThrowIfNull(observe);
        if (stableDetections < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stableDetections));
        }
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout));
        }
        if (pollMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollMilliseconds));
        }

        int required = Math.Max(2, stableDetections);
        StableNavigationActionTracker<ChallengeScreenState>
            tracker = new(required);
        // A first obscured recheck may invalidate the seed; leave enough
        // completed-observation budget to begin a fresh stable proof.
        int minimumObservations =
            required +
            (initialObservation is null ? 0 : 1);
        ObservationWaitBudget budget =
            new(timeout, minimumObservations, utcNow);
        if (initialObservation is not null)
        {
            ChallengeScreenMatch initialMatch =
                initialObservation.Value.Match;
            (int X, int Y)? initialAction =
                initialMatch.State == desired
                    ? MatchAction(initialMatch)
                    : null;
            (int X, int Y)? stableAction =
                tracker.Update(desired, initialAction);
            observed?.Invoke(initialMatch);
            budget.MarkObserved();
            if (stableAction is not null)
            {
                return (
                    initialObservation.Value.Frame,
                    initialMatch with
                    {
                        ActionX = stableAction.Value.X,
                        ActionY = stableAction.Value.Y,
                    });
            }
        }
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            (ImageFrame Frame, ChallengeScreenMatch Match)
                observation = observe();
            ChallengeScreenMatch match =
                observation.Match;
            // GB-011: the enum's None value is still a valid generic tracker
            // state. Strip every action that does not belong to the requested UI.
            (int X, int Y)? action =
                match.State == desired
                    ? MatchAction(match)
                    : null;
            (int X, int Y)? stableAction =
                tracker.Update(desired, action);
            observed?.Invoke(match);
            budget.MarkObserved();
            if (stableAction is not null)
            {
                return (
                    observation.Frame,
                    match with
                    {
                        ActionX = stableAction.Value.X,
                        ActionY = stableAction.Value.Y,
                    });
            }

            await (delay is null
                    ? Task.Delay(
                        pollMilliseconds,
                        cancellationToken)
                    : delay(
                        pollMilliseconds,
                        cancellationToken))
                .ConfigureAwait(false);
        }
        return null;
    }

    private static bool IsChallengeNavigationAction(
        ChallengeScreenState state) =>
        state is
            ChallengeScreenState.PostMatchPreview or
            ChallengeScreenState.PreviewReady;

    private static bool RequiresStableChallengeAction(
        ChallengeScreenState state) =>
        state is
            ChallengeScreenState.ChallengeAvailable or
            ChallengeScreenState.PreviewReady or
            ChallengeScreenState.PostMatchPreview or
            ChallengeScreenState.Prestart;

    private static (int X, int Y)? MatchAction(
        ChallengeScreenMatch match) =>
        match.ActionX is int x && match.ActionY is int y
            ? (x, y)
            : null;
}
