namespace ExpeditionsMacro.Core.Models;

public enum ChallengeType
{
    Trait = 1,
    Stat = 2,
    Sprite = 3,
}

public enum ChallengeMapId
{
    SchoolGrounds = 1,
    FlowerForest = 2,
    RoseKingdom = 3,
    FairyKingForest = 4,
    KingsTomb = 5,
}

public enum ChallengeIdleBehavior
{
    WaitForReset,
    RunExpeditions,
}

public sealed record ChallengeMapProfile
{
    public required ChallengeMapId Map { get; init; }

    public string CameraModelId { get; init; } = string.Empty;

    public string PrestartPlacementModelId { get; init; } = string.Empty;

    public string DelayedPlacementModelId { get; init; } = string.Empty;

    public int DelayedPlacementSeconds { get; init; } = 30;

    public int TeamSlot { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(Map)) throw new InvalidDataException("Challenge map is invalid.");
        if (DelayedPlacementSeconds is < 0 or > 3600) throw new InvalidDataException("Delayed placement time must be 0 through 3600 seconds.");
        if (TeamSlot is < 0 or > 8) throw new InvalidDataException("Team must be Don't change or Team 1 through 8.");
        ValidateOptionalId(CameraModelId, "camera model");
        ValidateOptionalId(PrestartPlacementModelId, "prestart placement model");
        ValidateOptionalId(DelayedPlacementModelId, "delayed placement model");
    }

    public void ValidateReady()
    {
        Validate();
        if (string.IsNullOrWhiteSpace(CameraModelId)) throw new InvalidDataException($"Choose a camera model for {Map}.");
        if (string.IsNullOrWhiteSpace(PrestartPlacementModelId) && string.IsNullOrWhiteSpace(DelayedPlacementModelId))
        {
            throw new InvalidDataException($"Choose at least one placement model for {Map}.");
        }
    }

    private static void ValidateOptionalId(string id, string label)
    {
        if (string.IsNullOrEmpty(id)) return;
        string name = Path.GetFileName(id);
        if (string.IsNullOrWhiteSpace(name) || name != id || id is "." or "..") throw new InvalidDataException($"The selected {label} is invalid.");
    }
}

public sealed record ChallengePreset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public bool RunTraitChallenge { get; init; } = true;

    public bool RunStatChallenge { get; init; } = true;

    public bool RunSpriteChallenge { get; init; } = true;

    public required IReadOnlyList<ChallengeMapProfile> Maps { get; init; }

    public string DetectorPackId { get; init; } = "anime-expeditions-expeditions";

    public ChallengeIdleBehavior IdleBehavior { get; init; } = ChallengeIdleBehavior.WaitForReset;

    public string ExpeditionPresetId { get; init; } = string.Empty;

    public bool AutoRecover { get; init; } = true;

    public int DefeatRetries { get; init; }

    public int ZoomTicks { get; init; } = 30;

    public int PitchDragPixels { get; init; } = 1800;

    public int PollMilliseconds { get; init; } = 450;

    public int StableDetections { get; init; } = 2;

    public int UnitKeyHoldMilliseconds { get; init; } = 110;

    public int UnitSelectDelayMilliseconds { get; init; } = 250;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ChallengeType> EnabledTypes =>
        new[]
        {
            (Enabled: RunTraitChallenge, Type: ChallengeType.Trait),
            (Enabled: RunStatChallenge, Type: ChallengeType.Stat),
            (Enabled: RunSpriteChallenge, Type: ChallengeType.Sprite),
        }
        .Where(value => value.Enabled)
        .Select(value => value.Type)
        .ToArray();

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported Challenge preset format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Challenge preset identity is missing.");
        if (EnabledTypes.Count == 0) throw new InvalidDataException("Select at least one regular Challenge type.");
        if (string.IsNullOrWhiteSpace(DetectorPackId)) throw new InvalidDataException("Choose a detector pack.");
        if (Maps.Count != Enum.GetValues<ChallengeMapId>().Length || Maps.Select(profile => profile.Map).Distinct().Count() != Maps.Count)
        {
            throw new InvalidDataException("Configure each Challenge map exactly once.");
        }
        foreach (ChallengeMapProfile profile in Maps) profile.Validate();
        if (IdleBehavior == ChallengeIdleBehavior.RunExpeditions && string.IsNullOrWhiteSpace(ExpeditionPresetId))
        {
            throw new InvalidDataException("Choose the Expeditions preset to run while Challenges are on cooldown.");
        }
        if (ZoomTicks is < 5 or > 80 || PitchDragPixels is < 300 or > 5000) throw new InvalidDataException("Camera preparation settings are out of range.");
        if (PollMilliseconds is < 150 or > 5000 || StableDetections is < 1 or > 5) throw new InvalidDataException("Detection timing is out of range.");
        if (UnitKeyHoldMilliseconds is < 30 or > 1000 || UnitSelectDelayMilliseconds is < 25 or > 5000) throw new InvalidDataException("Placement timing is out of range.");
        if (DefeatRetries is < 0 or > 20) throw new InvalidDataException("Defeat retries must be 0 through 20.");
    }

    public void ValidateReady()
    {
        Validate();
        foreach (ChallengeMapProfile profile in Maps) profile.ValidateReady();
    }

    public static IReadOnlyList<ChallengeMapProfile> EmptyMapProfiles() =>
        Enum.GetValues<ChallengeMapId>()
            .Select(map => new ChallengeMapProfile { Map = map })
            .ToArray();
}
