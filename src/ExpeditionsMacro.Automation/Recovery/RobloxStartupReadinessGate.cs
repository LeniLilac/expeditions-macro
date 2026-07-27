using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Recovery;

internal readonly record struct RobloxStartupReadinessObservation(
    string? ClassifiedState,
    double LobbyScore,
    double StrongestOtherScore,
    double LobbyThreshold);

internal static class RobloxStartupReadinessGate
{
    internal const int StableReadyFrames = 3;
    internal const double LobbyThresholdTolerance = 0.05;
    internal const double LobbyDominanceMargin = 0.20;

    public static async Task WaitAsync(
        Func<CancellationToken, Task<RobloxStartupReadinessObservation>>
            observe,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(observe);
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
        DateTimeOffset deadline = utcNow() + timeout;
        int stable = 0;
        while (utcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                RobloxStartupReadinessObservation observation =
                    await observe(cancellationToken).ConfigureAwait(false);
                stable = IsReady(observation)
                    ? stable + 1
                    : 0;
                if (stable >= StableReadyFrames)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                stable = 0;
            }

            await delay(
                pollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxSessionUnavailableException(
            "Roblox reopened but did not finish loading a Lobby view before the startup-check deadline.");
    }

    internal static bool IsReady(
        RobloxStartupReadinessObservation observation)
    {
        if (string.Equals(
                observation.ClassifiedState,
                "lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (observation.ClassifiedState is not null ||
            !double.IsFinite(observation.LobbyScore) ||
            !double.IsFinite(observation.StrongestOtherScore) ||
            !double.IsFinite(observation.LobbyThreshold) ||
            observation.LobbyScore is < 0 or > 1 ||
            observation.StrongestOtherScore is < 0 or > 1 ||
            observation.LobbyThreshold is <= LobbyThresholdTolerance or > 1)
        {
            return false;
        }

        double minimumLobbyScore =
            observation.LobbyThreshold -
            LobbyThresholdTolerance;
        return observation.LobbyScore >= minimumLobbyScore &&
            observation.LobbyScore -
            observation.StrongestOtherScore >=
            LobbyDominanceMargin;
    }
}
