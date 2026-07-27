namespace ExpeditionsMacro.Automation.Navigation;

internal sealed class ObservationWaitBudget
{
    private static readonly TimeSpan MinimumHardTimeout =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumHardTimeout =
        TimeSpan.FromSeconds(90);

    private readonly Func<DateTimeOffset> _utcNow;
    private readonly DateTimeOffset _softDeadline;
    private readonly DateTimeOffset _hardDeadline;
    private readonly int _minimumObservations;
    private int _observations;

    public ObservationWaitBudget(
        TimeSpan softTimeout,
        int minimumObservations,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (softTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(softTimeout));
        }
        if (minimumObservations < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumObservations));
        }

        _utcNow = utcNow ??
            (() => DateTimeOffset.UtcNow);
        DateTimeOffset startedAt = _utcNow();
        _softDeadline = startedAt + softTimeout;
        TimeSpan hardTimeout = TimeSpan.FromTicks(
            Math.Clamp(
                softTimeout.Ticks * 4,
                MinimumHardTimeout.Ticks,
                MaximumHardTimeout.Ticks));
        _hardDeadline = startedAt + hardTimeout;
        _minimumObservations = minimumObservations;
    }

    public bool ShouldObserve(
        bool confirmationPending = false)
    {
        DateTimeOffset now = _utcNow();
        return now < _hardDeadline &&
            (now < _softDeadline ||
             _observations < _minimumObservations ||
             confirmationPending);
    }

    public void MarkObserved() =>
        _observations++;
}
