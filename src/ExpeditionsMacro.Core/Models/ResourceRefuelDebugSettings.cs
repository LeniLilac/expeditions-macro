namespace ExpeditionsMacro.Core.Models;

[Flags]
public enum ResourceRefuelTarget
{
    None = 0,
    GoldMine = 1,
    ResourceDrill = 2,
    Both = GoldMine | ResourceDrill,
}

public sealed record ResourceRefuelDebugSettings
{
    public int RetryCount { get; init; } = 2;

    public int GoldForward1Milliseconds { get; init; } = 3000;

    public int GoldLeftMilliseconds { get; init; } = 820;

    public int GoldForward2Milliseconds { get; init; } = 2600;

    public int DrillForward1Milliseconds { get; init; } = 3000;

    public int DrillLeft1Milliseconds { get; init; } = 750;

    public int DrillForward2Milliseconds { get; init; } = 1000;

    public int DrillLeft2Milliseconds { get; init; } = 1600;

    public void Validate()
    {
        if (RetryCount is < 0 or > 5)
        {
            throw new InvalidDataException(
                "Resource refuel retries must be between 0 and 5.");
        }

        foreach ((string Label, int Value) timing in Timings())
        {
            if (timing.Value is < 50 or > 10000)
            {
                throw new InvalidDataException(
                    $"{timing.Label} must be between 50 and 10,000 ms.");
            }
        }
    }

    public IReadOnlyList<(char Key, int HoldMilliseconds)>
        RouteFor(ResourceRefuelTarget target) => target switch
        {
            ResourceRefuelTarget.GoldMine =>
            [
                ('W', GoldForward1Milliseconds),
            ('A', GoldLeftMilliseconds),
            ('W', GoldForward2Milliseconds),
        ],
            ResourceRefuelTarget.ResourceDrill =>
            [
                ('W', DrillForward1Milliseconds),
            ('A', DrillLeft1Milliseconds),
            ('W', DrillForward2Milliseconds),
            ('A', DrillLeft2Milliseconds),
        ],
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

    private IEnumerable<(string Label, int Value)> Timings()
    {
        yield return ("Gold Mine W1", GoldForward1Milliseconds);
        yield return ("Gold Mine A", GoldLeftMilliseconds);
        yield return ("Gold Mine W2", GoldForward2Milliseconds);
        yield return ("Resource Drill W1", DrillForward1Milliseconds);
        yield return ("Resource Drill A1", DrillLeft1Milliseconds);
        yield return ("Resource Drill W2", DrillForward2Milliseconds);
        yield return ("Resource Drill A2", DrillLeft2Milliseconds);
    }

}
