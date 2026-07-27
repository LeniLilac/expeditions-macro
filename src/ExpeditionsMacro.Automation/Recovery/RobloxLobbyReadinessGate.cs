using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Recovery;

internal static class RobloxLobbyReadinessGate
{
    internal const int StableLobbyFrames = 3;

    public static async Task WaitAsync(
        Func<CancellationToken, Task<ImageFrame>> capture,
        Func<ImageFrame, string?> classify,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(classify);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        if (pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }

        utcNow ??= static () => DateTimeOffset.UtcNow;
        delay ??= static (duration, token) =>
            Task.Delay(duration, token);
        StableStateTracker<string> lobbyTracker =
            new(StableLobbyFrames);
        ObservationWaitBudget budget = new(
            timeout,
            StableLobbyFrames,
            utcNow);
        while (budget.ShouldObserve(
                   lobbyTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ImageFrame frame =
                    await capture(cancellationToken).ConfigureAwait(false);
                string? state = classify(frame);
                string? lobby = string.Equals(
                    state,
                    "lobby",
                    StringComparison.OrdinalIgnoreCase)
                    ? "lobby"
                    : null;
                if (lobbyTracker.Update(lobby) is not null) return;
                budget.MarkObserved();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                lobbyTracker.Reset();
            }

            await delay(
                pollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxSessionUnavailableException(
            "Roblox reopened but did not reach a stable lobby before the recovery deadline.");
    }
}
