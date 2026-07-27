using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private async Task<ImageFrame?>
        TryWaitForPostMatchPreviewAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ImageFrame? initialFrame,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        StableNavigationActionTracker<
            ChallengeScreenState> tracker =
                new(required: 2);
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2);
        if (initialFrame is not null)
        {
            ChallengeScreenMatch initialMatch =
                ChallengeScreenDetector.Detect(initialFrame);
            (int X, int Y)? initialAction =
                initialMatch.State ==
                    ChallengeScreenState.PostMatchPreview
                    ? ChallengeScreenDetector.ActionFor(
                        ChallengeScreenState.PostMatchPreview,
                        initialFrame)
                    : null;
            (int X, int Y)? initialStableAction =
                tracker.Update(
                    initialMatch.State ==
                        ChallengeScreenState.PostMatchPreview
                        ? initialMatch.State
                        : ChallengeScreenState.None,
                    initialAction);
            budget.MarkObserved();
            if (initialStableAction is not null)
            {
                return initialFrame;
            }
        }
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            ImageFrame frame =
                CaptureClient(window, detector);
            ChallengeScreenMatch match =
                ChallengeScreenDetector.Detect(frame);
            budget.MarkObserved();
            (int X, int Y)? action =
                ChallengeScreenDetector.ActionFor(
                    ChallengeScreenState.PostMatchPreview,
                    frame);
            if (tracker.Update(
                    match.State ==
                        ChallengeScreenState.PostMatchPreview
                        ? match.State
                        : ChallengeScreenState.None,
                    action) is not null)
            {
                return frame;
            }
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
