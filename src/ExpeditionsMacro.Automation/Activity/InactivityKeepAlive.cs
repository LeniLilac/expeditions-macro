namespace ExpeditionsMacro.Automation.Activity;

internal enum InactivityPulseResult
{
    NotDue,
    Sent,
    Deferred,
}

internal sealed class InactivityKeepAlive
{
    internal const char PulseKey = 'O';

    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(8);
    internal static readonly TimeSpan FailureRetryInterval = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _nextPulseAtUtc;

    public InactivityKeepAlive(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _nextPulseAtUtc = _timeProvider.GetUtcNow() + Interval;
    }

    internal DateTimeOffset NextPulseAtUtc => _nextPulseAtUtc;

    internal async Task<InactivityPulseResult> TryPulseAsync(
        Func<char, CancellationToken, Task> pulse,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now < _nextPulseAtUtc)
        {
            return InactivityPulseResult.NotDue;
        }

        try
        {
            await pulse(PulseKey, cancellationToken).ConfigureAwait(false);
            _nextPulseAtUtc = _timeProvider.GetUtcNow() + Interval;
            return InactivityPulseResult.Sent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // A focus or transient input failure must not stop the match. Retry
            // before the normal eight-minute interval while AFK recovery remains
            // available as the final fallback.
            _nextPulseAtUtc = _timeProvider.GetUtcNow() + FailureRetryInterval;
            return InactivityPulseResult.Deferred;
        }
    }
}
