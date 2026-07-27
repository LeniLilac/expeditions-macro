using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private Task ClickActionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        CancellationToken cancellationToken) =>
        ClickActionAsync(
            window,
            detector,
            state,
            clientImage: null,
            cancellationToken);

    private async Task ClickActionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        ImageFrame? clientImage,
        CancellationToken cancellationToken)
    {
        (int X, int Y) action =
            await WaitForStableActionAsync(
                state,
                clientImage,
                () => CaptureClient(window, detector),
                detector.ActionFor,
                static () => DateTimeOffset.UtcNow,
                static (duration, token) =>
                    Task.Delay(duration, token),
                cancellationToken).ConfigureAwait(false);
        Focus(window);
        await _automation.ClickClientAsync(
            window,
            action.X,
            action.Y,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<(int X, int Y)>
        WaitForStableActionAsync(
        string state,
        ImageFrame? initialFrame,
        Func<ImageFrame> capture,
        Func<string, ImageFrame?, (int X, int Y)> locate,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(locate);
        ArgumentNullException.ThrowIfNull(utcNow);
        ArgumentNullException.ThrowIfNull(delay);

        StableNavigationActionTracker<string> tracker =
            new();
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(3),
            minimumObservations: 2,
            utcNow);
        ImageFrame? current = initialFrame;
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            current ??= capture();
            (int X, int Y) action =
                locate(state, current);
            (int X, int Y)? stable =
                tracker.Update(state, action);
            budget.MarkObserved();
            if (stable is not null)
            {
                return stable.Value;
            }

            current = null;
            await delay(
                TimeSpan.FromMilliseconds(150),
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            $"The {state} action did not settle before it could be clicked.");
    }
}
