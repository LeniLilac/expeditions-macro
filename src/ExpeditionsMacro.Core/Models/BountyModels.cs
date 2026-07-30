namespace ExpeditionsMacro.Core.Models;

public enum BountyObjectiveKind
{
    RaidActOne,
    InfiniteWave,
    Challenge,
}

public sealed record BountyObjective
{
    public required string Key { get; init; }
    public required BountyObjectiveKind Kind { get; init; }
    public ChallengeMapId? Map { get; init; }
    public int TargetWave { get; init; }
    public int RequiredCount { get; init; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidDataException(
                "Bounty objective identity is missing.");
        }
        if (RequiredCount is < 1 or > 10)
        {
            throw new InvalidDataException(
                "Bounty objective count is invalid.");
        }
        if (Kind == BountyObjectiveKind.InfiniteWave)
        {
            if (Map is null ||
                TargetWave is not (15 or 30 or 45 or 60))
            {
                throw new InvalidDataException(
                    "Bounty Infinite objective is invalid.");
            }
        }
        else if (Map is not null || TargetWave != 0)
        {
            throw new InvalidDataException(
                "Only Infinite Bounty objectives use a map and wave.");
        }
    }
}

public sealed record BountyDefinition(
    int Number,
    bool AlwaysReroll,
    bool ChallengeConditional,
    IReadOnlyList<BountyObjective> Objectives)
{
    public bool HasWork => Objectives.Count != 0;
}

public static class BountyCatalog
{
    public const int DailyClaimLimit = 10;

    public static IReadOnlyList<BountyDefinition> All { get; } =
    [
        Skip(1),
        Work(
            2,
            Raid(),
            Infinite("sg-30", ChallengeMapId.SchoolGrounds, 30),
            Infinite("fkf-30", ChallengeMapId.FairyKingForest, 30)),
        Skip(3),
        Work(
            4,
            Raid(),
            Infinite("sg-30", ChallengeMapId.SchoolGrounds, 30),
            Infinite("fkf-15", ChallengeMapId.FairyKingForest, 15)),
        Work(
            5,
            Infinite("rk-15", ChallengeMapId.RoseKingdom, 15),
            Infinite("rk-45", ChallengeMapId.RoseKingdom, 45)),
        Work(
            6,
            Infinite("fkf-60", ChallengeMapId.FairyKingForest, 60),
            Infinite("fkf-30", ChallengeMapId.FairyKingForest, 30),
            Infinite("kt-60", ChallengeMapId.KingsTomb, 60)),
        Work(
            7,
            conditional: true,
            Challenge("challenge-2", 2),
            Infinite("ff-15", ChallengeMapId.FlowerForest, 15)),
        Skip(8),
        Work(
            9,
            conditional: true,
            Infinite("rk-45", ChallengeMapId.RoseKingdom, 45),
            Challenge("challenge-1", 1)),
        Skip(10),
    ];

    public static IReadOnlyList<PlacementTarget>
        RequiredPlacementTargets
    { get; } =
    [
        new()
        {
            Mode = PlacementTargetMode.Raid,
            MapNumber = 1,
            ActNumber = 1,
        },
        .. Enumerable.Range(1, 5).Select(map =>
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = map,
                StoryRunKind = StoryRunKind.Infinite,
                ActNumber = 1,
            }),
    ];

    public static BountyDefinition For(int number) =>
        All.SingleOrDefault(value => value.Number == number) ??
        throw new InvalidDataException(
            $"Mythic Bounty #{number} is not supported.");

    private static BountyDefinition Skip(int number) =>
        new(number, true, false, []);

    private static BountyDefinition Work(
        int number,
        params BountyObjective[] objectives) =>
        Work(number, false, objectives);

    private static BountyDefinition Work(
        int number,
        bool conditional,
        params BountyObjective[] objectives) =>
        new(number, false, conditional, objectives);

    private static BountyObjective Raid() =>
        new()
        {
            Key = "raid-spirit-city-act-1",
            Kind = BountyObjectiveKind.RaidActOne,
        };

    private static BountyObjective Infinite(
        string key,
        ChallengeMapId map,
        int wave) =>
        new()
        {
            Key = key,
            Kind = BountyObjectiveKind.InfiniteWave,
            Map = map,
            TargetWave = wave,
        };

    private static BountyObjective Challenge(
        string key,
        int count) =>
        new()
        {
            Key = key,
            Kind = BountyObjectiveKind.Challenge,
            RequiredCount = count,
        };
}

public sealed record BountyActiveProgress
{
    public required int Number { get; init; }
    public IReadOnlyDictionary<string, int> ObjectiveProgress { get; init; } =
        new Dictionary<string, int>();

    public int ProgressFor(string key) =>
        ObjectiveProgress.GetValueOrDefault(key);
}

public sealed record BountyProgressState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public DateTimeOffset DailyEpochUtc { get; init; } =
        UtcDay(DateTimeOffset.UtcNow);
    public int ClaimedToday { get; init; }
    public IReadOnlyList<BountyActiveProgress> Active { get; init; } = [];
    public DateTimeOffset UpdatedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public BountyProgressState AdvanceDay(DateTimeOffset now)
    {
        DateTimeOffset day = UtcDay(now);
        return day == DailyEpochUtc
            ? this
            : this with
            {
                DailyEpochUtc = day,
                ClaimedToday = 0,
                UpdatedAtUtc = now.ToUniversalTime(),
            };
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                "Unsupported Bounty progress format.");
        }
        if (ClaimedToday is < 0 or > DailyClaimLimitWithBuffer)
        {
            throw new InvalidDataException(
                "Bounty daily claim progress is invalid.");
        }
        if (Active.Select(value => value.Number).Distinct().Count() !=
            Active.Count)
        {
            throw new InvalidDataException(
                "Every active Bounty must be unique.");
        }
        foreach (BountyActiveProgress active in Active)
        {
            BountyDefinition definition = BountyCatalog.For(active.Number);
            foreach ((string key, int value) in active.ObjectiveProgress)
            {
                BountyObjective objective =
                    definition.Objectives.SingleOrDefault(item =>
                        string.Equals(
                            item.Key,
                            key,
                            StringComparison.Ordinal)) ??
                    throw new InvalidDataException(
                        "Bounty progress refers to an unknown objective.");
                if (value < 0 || value > objective.RequiredCount)
                {
                    throw new InvalidDataException(
                        "Bounty objective progress is invalid.");
                }
            }
        }
    }

    public static DateTimeOffset UtcDay(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
    }

    private const int DailyClaimLimitWithBuffer = 10;
}

public sealed record BountyWorkRoute
{
    public required BountyObjectiveKind Kind { get; init; }
    public ChallengeMapId? Map { get; init; }
    public int TargetWave { get; init; }
    public int ChallengeRuns { get; init; }
    public required IReadOnlyList<int> CoveredBounties { get; init; }
}
