namespace ExpeditionsMacro.Automation.Navigation;

internal sealed class ObservationWaitBudget
{
    private static readonly TimeSpan MinimumUiLoadWindow =
        TimeSpan.FromSeconds(12);
    private static readonly TimeSpan MinimumHardGrace =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumHardGrace =
        TimeSpan.FromSeconds(60);

    private readonly Func<DateTimeOffset> _utcNow;
    private readonly DateTimeOffset _startedAt;
    private DateTimeOffset _softDeadline;
    private DateTimeOffset _hardDeadline;
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
        _startedAt = _utcNow();
        SetDeadlines(softTimeout);
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

    public void ExtendSoftTimeout(TimeSpan softTimeout)
    {
        if (softTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(softTimeout));
        }
        if (_startedAt + softTimeout <= _softDeadline)
        {
            return;
        }

        SetDeadlines(softTimeout);
    }

    private void SetDeadlines(TimeSpan softTimeout)
    {
        TimeSpan effectiveSoftTimeout =
            softTimeout < MinimumUiLoadWindow
                ? MinimumUiLoadWindow
                : softTimeout;
        _softDeadline =
            _startedAt + effectiveSoftTimeout;
        double graceMilliseconds = Math.Clamp(
            effectiveSoftTimeout.TotalMilliseconds * 3,
            MinimumHardGrace.TotalMilliseconds,
            MaximumHardGrace.TotalMilliseconds);
        _hardDeadline =
            _softDeadline +
            TimeSpan.FromMilliseconds(graceMilliseconds);
    }
}
