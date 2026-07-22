using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Challenges;

internal enum ChallengeTerminalContinuation
{
    PlayMenu,
    RepeatStage,
}

public static class ChallengeRunPolicy
{
    internal static ChallengeTerminalContinuation TerminalContinuation(
        bool victory,
        int retriesUsed,
        int configuredRetries)
    {
        if (retriesUsed < 0) throw new ArgumentOutOfRangeException(nameof(retriesUsed));
        if (configuredRetries < 0) throw new ArgumentOutOfRangeException(nameof(configuredRetries));
        return !victory && retriesUsed < configuredRetries
            ? ChallengeTerminalContinuation.RepeatStage
            : ChallengeTerminalContinuation.PlayMenu;
    }

    public static ChallengePlacementPartition PartitionPrestartPlacements(
        IReadOnlyList<PlacementStep> steps,
        ScreenRegion dialogOcclusion)
    {
        ArgumentNullException.ThrowIfNull(steps);
        List<PlacementStep> beforeStart = [];
        List<PlacementStep> afterStart = [];
        foreach (PlacementStep step in steps)
        {
            (dialogOcclusion.Contains(step.X, step.Y) ? afterStart : beforeStart).Add(step);
        }
        return new ChallengePlacementPartition(beforeStart, afterStart);
    }

    public static DateTimeOffset ResetEpoch(DateTimeOffset now)
    {
        int minute = now.Minute < 30 ? 0 : 30;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, minute, 0, now.Offset);
    }

    public static DateTimeOffset NextGlobalReset(DateTimeOffset now)
    {
        if (now.Minute < 30)
        {
            return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 30, 0, now.Offset);
        }

        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset).AddHours(1);
    }

    public static ChallengeType? NextType(ChallengePreset preset, IReadOnlySet<ChallengeType> attempted)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(attempted);
        preset.Validate();

        foreach (ChallengeType type in preset.EnabledTypes)
        {
            if (!attempted.Contains(type))
            {
                return type;
            }
        }

        return null;
    }

    public static bool ShouldRunExpeditionsWhileWaiting(ChallengePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        preset.Validate();
        return preset.IdleBehavior == ChallengeIdleBehavior.RunExpeditions;
    }

    public static bool IsDelayedPlacementDue(ChallengeMapProfile profile, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        return !string.IsNullOrWhiteSpace(profile.DelayedPlacementModelId) &&
            elapsed >= TimeSpan.FromSeconds(profile.DelayedPlacementSeconds);
    }

    public static DateTimeOffset NextUtcMidnight(DateTimeOffset now)
    {
        DateTimeOffset utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1);
    }
}

public sealed record ChallengePlacementPartition(
    IReadOnlyList<PlacementStep> BeforeStart,
    IReadOnlyList<PlacementStep> AfterStart);

public sealed class ChallengeRotationState
{
    private readonly HashSet<ChallengeType> _attempted = [];

    public DateTimeOffset? Epoch { get; private set; }

    public DateTimeOffset? PreviousAllCooldownEpoch { get; private set; }

    public DateTimeOffset? DailyLimitUntilUtc { get; private set; }

    public IReadOnlySet<ChallengeType> Attempted => _attempted;

    public bool Advance(DateTimeOffset now)
    {
        DateTimeOffset epoch = ChallengeRunPolicy.ResetEpoch(now);
        bool changed = Epoch is null || Epoch.Value != epoch;
        if (changed)
        {
            Epoch = epoch;
            _attempted.Clear();
        }
        if (DailyLimitUntilUtc is DateTimeOffset until && now.ToUniversalTime() >= until)
        {
            DailyLimitUntilUtc = null;
            PreviousAllCooldownEpoch = null;
        }
        return changed;
    }

    public void MarkAttempted(ChallengeType type) => _attempted.Add(type);

    public void ObserveAvailability()
    {
        PreviousAllCooldownEpoch = null;
        DailyLimitUntilUtc = null;
    }

    public bool ObserveAllCooldown(DateTimeOffset now)
    {
        Advance(now);
        DateTimeOffset current = Epoch!.Value;
        if (PreviousAllCooldownEpoch is DateTimeOffset previous && previous < current)
        {
            DailyLimitUntilUtc = ChallengeRunPolicy.NextUtcMidnight(now);
            return true;
        }
        PreviousAllCooldownEpoch = current;
        return false;
    }
}
