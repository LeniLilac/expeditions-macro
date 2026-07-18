namespace ExpeditionsMacro.Core.Models;

public sealed record PlacementStep
{
    public required int UnitKey { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required int DelayAfterMilliseconds { get; init; }

    public void Validate(int clientWidth, int clientHeight)
    {
        if (UnitKey is < 0 or > 9) throw new InvalidDataException("Unit key must be 0 through 9.");
        if (X < 0 || Y < 0 || X >= clientWidth || Y >= clientHeight) throw new InvalidDataException("Placement coordinate falls outside the Roblox client.");
        if (DelayAfterMilliseconds < 0) throw new InvalidDataException("Placement delay cannot be negative.");
    }
}

public sealed record PlacementCapture(
    int UnitKey,
    int X,
    int Y,
    int SelectedAtMilliseconds,
    int ClickedAtMilliseconds);

public sealed record PlacementModel
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int ClientWidth { get; init; }

    public required int ClientHeight { get; init; }

    public required IReadOnlyList<PlacementStep> Steps { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported placement model format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Placement model identity is missing.");
        if (ClientWidth <= 0 || ClientHeight <= 0) throw new InvalidDataException("Placement model client size is invalid.");
        if (Steps.Count == 0) throw new InvalidDataException("Placement model has no steps.");
        foreach (PlacementStep step in Steps) step.Validate(ClientWidth, ClientHeight);
    }

    public static IReadOnlyList<PlacementStep> FromCaptures(
        IReadOnlyList<PlacementCapture> captures,
        int defaultDelayMilliseconds,
        bool useRecordedDelays)
    {
        if (defaultDelayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(defaultDelayMilliseconds));
        List<PlacementStep> steps = new(captures.Count);
        for (int index = 0; index < captures.Count; index++)
        {
            PlacementCapture capture = captures[index];
            int delay = defaultDelayMilliseconds;
            if (useRecordedDelays && index + 1 < captures.Count)
            {
                delay = Math.Max(0, captures[index + 1].SelectedAtMilliseconds - capture.ClickedAtMilliseconds);
            }

            steps.Add(new PlacementStep
            {
                UnitKey = capture.UnitKey,
                X = capture.X,
                Y = capture.Y,
                DelayAfterMilliseconds = delay,
            });
        }

        return steps;
    }
}
