namespace ExpeditionsMacro.Core.Models;

public enum StoryRunKind
{
    Act,
    Infinite,
    Mastery,
}

public enum RaidAct
{
    Act1 = 1,
    Act2 = 2,
    Act3 = 3,
}

public sealed record StoryPreset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ChallengeMapId Map { get; init; } = ChallengeMapId.SchoolGrounds;
    public StoryRunKind RunKind { get; init; } = StoryRunKind.Act;
    public int ActNumber { get; init; } = 1;
    public bool HardMode { get; init; }
    public string CameraModelId { get; init; } = string.Empty;
    public string PrestartPlacementModelId { get; init; } = string.Empty;
    public string DelayedPlacementModelId { get; init; } = string.Empty;
    public int DelayedPlacementSeconds { get; init; } = 30;
    public int TeamSlot { get; init; }
    public int DefeatRetries { get; init; }
    public bool AutoRecover { get; init; } = true;
    public int ZoomTicks { get; init; } = 30;
    public int PitchDragPixels { get; init; } = 1800;
    public int PollMilliseconds { get; init; } = 450;
    public int StableDetections { get; init; } = 2;
    public int UnitKeyHoldMilliseconds { get; init; } = 110;
    public int UnitSelectDelayMilliseconds { get; init; } = 250;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Validate(bool requireModels = false)
    {
        ValidateIdentity(SchemaVersion, Id, Name, "Story");
        if (!Enum.IsDefined(Map) || !Enum.IsDefined(RunKind)) throw new InvalidDataException("Story map or run type is invalid.");
        if (ActNumber is < 1 or > 5) throw new InvalidDataException("Story act must be 1 through 5.");
        ValidateRuntimeSettings(DelayedPlacementSeconds, TeamSlot, DefeatRetries, ZoomTicks, PitchDragPixels, PollMilliseconds, StableDetections, UnitKeyHoldMilliseconds, UnitSelectDelayMilliseconds);
        ValidateModelId(CameraModelId, "camera model", requireModels);
        ValidateModelId(PrestartPlacementModelId, "before-start placement model", false);
        ValidateModelId(DelayedPlacementModelId, "after-start placement model", false);
        if (requireModels && string.IsNullOrWhiteSpace(PrestartPlacementModelId) && string.IsNullOrWhiteSpace(DelayedPlacementModelId))
        {
            throw new InvalidDataException("Choose at least one Story placement model.");
        }
    }

    internal static void ValidateIdentity(int schemaVersion, string id, string name, string label)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new InvalidDataException($"Unsupported {label} preset format.");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new InvalidDataException($"{label} preset identity is missing.");
    }

    internal static void ValidateRuntimeSettings(int delay, int team, int retries, int zoom, int pitch, int poll, int stable, int keyHold, int keyDelay)
    {
        if (delay is < 0 or > 14400) throw new InvalidDataException("After-start placement delay must be 0 through 14400 seconds.");
        if (team is < 0 or > 8) throw new InvalidDataException("Team must be Don't change or Team 1 through 8.");
        if (retries is < 0 or > 20) throw new InvalidDataException("Defeat retries must be 0 through 20.");
        if (zoom is < 5 or > 80 || pitch is < 300 or > 5000) throw new InvalidDataException("Camera preparation settings are out of range.");
        if (poll is < 150 or > 5000 || stable is < 1 or > 5) throw new InvalidDataException("Detection timing is out of range.");
        if (keyHold is < 30 or > 1000 || keyDelay is < 25 or > 5000) throw new InvalidDataException("Placement timing is out of range.");
    }

    internal static void ValidateModelId(string id, string label, bool required)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            if (required) throw new InvalidDataException($"Choose a {label}.");
            return;
        }

        string name = Path.GetFileName(id);
        if (name != id || id is "." or "..") throw new InvalidDataException($"The selected {label} is invalid.");
    }
}

public sealed record RaidPreset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string Id { get; init; }
    public required string Name { get; init; }
    public RaidAct Act { get; init; } = RaidAct.Act1;
    public string CameraModelId { get; init; } = string.Empty;
    public string PrestartPlacementModelId { get; init; } = string.Empty;
    public string DelayedPlacementModelId { get; init; } = string.Empty;
    public int DelayedPlacementSeconds { get; init; } = 30;
    public int TeamSlot { get; init; }
    public int DefeatRetries { get; init; }
    public bool AutoRecover { get; init; } = true;
    public int ZoomTicks { get; init; } = 30;
    public int PitchDragPixels { get; init; } = 1800;
    public int PollMilliseconds { get; init; } = 450;
    public int StableDetections { get; init; } = 2;
    public int UnitKeyHoldMilliseconds { get; init; } = 110;
    public int UnitSelectDelayMilliseconds { get; init; } = 250;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Validate(bool requireModels = false)
    {
        StoryPreset.ValidateIdentity(SchemaVersion, Id, Name, "Raid");
        if (!Enum.IsDefined(Act)) throw new InvalidDataException("Raid act is invalid.");
        StoryPreset.ValidateRuntimeSettings(DelayedPlacementSeconds, TeamSlot, DefeatRetries, ZoomTicks, PitchDragPixels, PollMilliseconds, StableDetections, UnitKeyHoldMilliseconds, UnitSelectDelayMilliseconds);
        StoryPreset.ValidateModelId(CameraModelId, "camera model", requireModels);
        StoryPreset.ValidateModelId(PrestartPlacementModelId, "before-start placement model", false);
        StoryPreset.ValidateModelId(DelayedPlacementModelId, "after-start placement model", false);
        if (requireModels && string.IsNullOrWhiteSpace(PrestartPlacementModelId) && string.IsNullOrWhiteSpace(DelayedPlacementModelId))
        {
            throw new InvalidDataException("Choose at least one Raid placement model.");
        }
    }
}
