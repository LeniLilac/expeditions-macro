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
        int firstCovered = -1;
        for (int index = 0;
             index < steps.Count;
             index++)
        {
            PlacementStep step = steps[index];
            PlacementStep? coordinateOwner =
                step.Kind switch
                {
                    MatchStepKind.Placement => step,
                    MatchStepKind.ReconfigureUnit or
                        MatchStepKind.UpgradeUnit =>
                        PlacementReferencePolicy.ResolveTarget(
                            steps,
                            step),
                    _ => null,
                };
            if (coordinateOwner is not null &&
                dialogOcclusion.Contains(
                    coordinateOwner.X,
                    coordinateOwner.Y))
            {
                firstCovered = index;
                break;
            }
        }

        if (firstCovered < 0)
        {
            return new ChallengePlacementPartition(
                steps.ToArray(),
                []);
        }

        // The prestart timeline is author ordered. Once a coordinate action is
        // hidden by Start Game, defer its complete suffix so a Delay or a
        // dependent unit action cannot leap ahead of the placement it owns.
        return new ChallengePlacementPartition(
            steps.Take(firstCovered).ToArray(),
            steps.Skip(firstCovered).ToArray());
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
