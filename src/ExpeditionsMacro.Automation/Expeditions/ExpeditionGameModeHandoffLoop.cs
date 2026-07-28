using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Expeditions;

internal static class ExpeditionGameModeHandoffLoop
{
    internal const int MaximumChangeGamemodeAttempts = 3;

    internal static async Task<ChallengeScreenMatch> RunAsync(
        ImageFrame? initialFrame,
        Func<ImageFrame> capture,
        Func<ImageFrame, ChallengeScreenMatch> detect,
        Func<ImageFrame, (int X, int Y)?> locateChangeGamemode,
        Func<
            (int X, int Y),
            int,
            ChallengeScreenMatch,
            CancellationToken,
            Task> clickChangeGamemode,
        Func<
            ChallengeScreenMatch,
            CancellationToken,
            Task<ImageFrame?>> openPlayMenu,
        TimeSpan timeout,
        int stableDetections,
        int pollMilliseconds,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(detect);
        ArgumentNullException.ThrowIfNull(locateChangeGamemode);
        ArgumentNullException.ThrowIfNull(clickChangeGamemode);
        ArgumentNullException.ThrowIfNull(openPlayMenu);
        ArgumentNullException.ThrowIfNull(utcNow);
        ArgumentNullException.ThrowIfNull(delay);

        StableStateTracker<ChallengeScreenState> tracker =
            new(Math.Max(1, stableDetections));
        StableNavigationActionTracker<ChallengeScreenState>
            actionTracker =
                new(Math.Max(2, stableDetections));
        ObservationWaitBudget budget = new(
            timeout,
            Math.Max(2, stableDetections),
            utcNow);
        ChallengeScreenMatch last =
            new(ChallengeScreenState.None, 0);
        ChallengeScreenState lastVerifiedState =
            ChallengeScreenState.None;
        int changeGamemodeAttempts = 0;
        ImageFrame? current = initialFrame;

        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate ||
                   actionTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = current ?? capture();
            current = null;
            last = detect(frame);
            ChallengeScreenState? stable =
                tracker.Update(last.State);
            (int X, int Y)? stableChangeGamemode =
                actionTracker.Update(
                    last.State ==
                        ChallengeScreenState.PostMatchPreview
                        ? last.State
                        : ChallengeScreenState.None,
                    locateChangeGamemode(frame));
            budget.MarkObserved();
            if (stable is null)
            {
                await delay(
                    TimeSpan.FromMilliseconds(pollMilliseconds),
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (stable.Value != ChallengeScreenState.None &&
                lastVerifiedState != ChallengeScreenState.None &&
                stable.Value != lastVerifiedState)
            {
                changeGamemodeAttempts = 0;
            }
            if (stable.Value != ChallengeScreenState.None)
            {
                lastVerifiedState = stable.Value;
            }

            switch (ExpeditionMacroRunner
                        .SelectGameModeHandoffCommand(stable.Value))
            {
                case ExpeditionMacroRunner
                    .GameModeHandoffCommand.Complete:
                    return last;
                case ExpeditionMacroRunner
                    .GameModeHandoffCommand.ChangeGamemode:
                    if (stableChangeGamemode is null)
                    {
                        await delay(
                            TimeSpan.FromMilliseconds(
                                pollMilliseconds),
                            cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (changeGamemodeAttempts >=
                        MaximumChangeGamemodeAttempts)
                    {
                        await delay(
                            TimeSpan.FromMilliseconds(
                                pollMilliseconds),
                            cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    changeGamemodeAttempts++;
                    await clickChangeGamemode(
                        stableChangeGamemode.Value,
                        changeGamemodeAttempts,
                        last,
                        cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    tracker.Reset();
                    actionTracker.Reset();
                    await delay(
                        TimeSpan.FromMilliseconds(700),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case ExpeditionMacroRunner
                    .GameModeHandoffCommand.PressPlayKey:
                    current = await openPlayMenu(
                        last,
                        cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    tracker.Reset();
                    actionTracker.Reset();
                    await delay(
                        TimeSpan.FromMilliseconds(700),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case ExpeditionMacroRunner
                    .GameModeHandoffCommand.Wait:
                    await delay(
                        TimeSpan.FromMilliseconds(
                            pollMilliseconds),
                        cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException(
                        "The Expedition handoff policy returned an unknown command.");
            }
        }

        throw new TimeoutException(
            $"Timed out leaving the completed Expedition. Last state: {last.State} ({last.Confidence:P0}).");
    }
}
