namespace ExpeditionsMacro.Core.Models;

public sealed record ExpeditionPreset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public int MapNumber { get; init; } = 1;

    public int Difficulty { get; init; } = 1;

    public string CameraModelId { get; init; } = string.Empty;

    public string PlacementModelId { get; init; } = string.Empty;

    public string DetectorPackId { get; init; } = "anime-expeditions-expeditions";

    public bool ExtractAtCheckpoint { get; init; } = true;

    public int BossesBeforeExtract { get; init; } = 1;

    public bool AutoRecover { get; init; } = true;

    public int ZoomTicks { get; init; } = 30;

    public int PitchDragPixels { get; init; } = 1800;

    public int PollMilliseconds { get; init; } = 450;

    public int StableDetections { get; init; } = 2;

    public int UnitKeyHoldMilliseconds { get; init; } = 110;

    public int UnitSelectDelayMilliseconds { get; init; } = 250;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported preset format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Preset identity is missing.");
        if (MapNumber is < 1 or > 3) throw new InvalidDataException("Map must be 1, 2, or 3.");
        if (Difficulty is < 1 or > 3) throw new InvalidDataException("Difficulty must be 1, 2, or 3.");
        if (string.IsNullOrWhiteSpace(CameraModelId) || string.IsNullOrWhiteSpace(PlacementModelId)) throw new InvalidDataException("Choose both a camera and placement model.");
        if (BossesBeforeExtract is < 0 or > 99) throw new InvalidDataException("Boss checkpoint target must be 0 through 99.");
        if (ZoomTicks is < 5 or > 80 || PitchDragPixels is < 300 or > 5000) throw new InvalidDataException("Camera preparation settings are out of range.");
        if (PollMilliseconds is < 150 or > 5000 || StableDetections is < 1 or > 5) throw new InvalidDataException("Detection timing is out of range.");
        if (UnitKeyHoldMilliseconds is < 30 or > 1000 || UnitSelectDelayMilliseconds is < 25 or > 5000) throw new InvalidDataException("Placement timing is out of range.");
    }
}
