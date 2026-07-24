namespace ExpeditionsMacro.Automation.Discord;

public sealed record MacroRunTotalsSnapshot(
    TimeSpan Runtime,
    int Victories,
    int Defeats);

public sealed class MacroRunTotals
{
    private readonly DateTimeOffset _startedAt;
    private readonly Func<DateTimeOffset> _utcNow;
    private int _victories;
    private int _defeats;

    public MacroRunTotals()
        : this(DateTimeOffset.UtcNow, () => DateTimeOffset.UtcNow)
    {
    }

    internal MacroRunTotals(
        DateTimeOffset startedAt,
        Func<DateTimeOffset> utcNow)
    {
        _startedAt = startedAt;
        _utcNow = utcNow;
    }

    public MacroRunTotalsSnapshot Snapshot()
    {
        TimeSpan runtime = _utcNow() - _startedAt;
        return new MacroRunTotalsSnapshot(
            runtime < TimeSpan.Zero ? TimeSpan.Zero : runtime,
            Volatile.Read(ref _victories),
            Volatile.Read(ref _defeats));
    }

    internal MacroRunTotalsSnapshot RecordEvent(string eventName)
    {
        if (eventName.Equals("victory", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _victories);
        }
        else if (eventName.Equals("defeat", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _defeats);
        }

        return Snapshot();
    }
}
