using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Challenges;

public static class ChallengeRunPolicy
{
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
}
