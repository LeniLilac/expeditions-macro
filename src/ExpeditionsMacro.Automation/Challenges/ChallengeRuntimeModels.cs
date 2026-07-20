using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed record ChallengeMapRuntimeModels(
    CameraModel Camera,
    PlacementModel? PrestartPlacement,
    PlacementModel? DelayedPlacement);

public sealed record ChallengeRunSummary(
    DateTimeOffset StartedAt,
    TimeSpan Runtime,
    int Completed,
    int Victories,
    int Defeats,
    int Retries,
    int Recoveries,
    ChallengeType? CurrentType,
    ChallengeMapId? CurrentMap,
    DateTimeOffset? WaitingUntilUtc,
    bool DailyLimitReached);
