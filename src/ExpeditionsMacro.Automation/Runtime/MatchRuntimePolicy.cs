using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Runtime;

internal static class MatchRuntimePolicy
{
    public static readonly TimeSpan FifteenWaveLimit =
        TimeSpan.FromMinutes(12);

    public static readonly TimeSpan EventActFourLimit =
        TimeSpan.FromMinutes(17);

    public static readonly TimeSpan ExpeditionFirstCheckpointLimit =
        TimeSpan.FromMinutes(10);

    public static TimeSpan ChallengeLimit() => FifteenWaveLimit;

    public static TimeSpan EventLimit(EventPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return preset.Act == EventAct.Act4
            ? EventActFourLimit
            : FifteenWaveLimit;
    }

    public static TimeSpan? StageLimit(
        StoryPreset? story,
        RaidPreset? raid)
    {
        if ((story is null) == (raid is null))
        {
            throw new ArgumentException(
                "Provide exactly one Story or Raid preset.");
        }

        return story?.RunKind == StoryRunKind.Infinite
            ? null
            : FifteenWaveLimit;
    }

    public static TimeSpan? ExpeditionLimit(
        ExpeditionPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!preset.ExtractAtCheckpoint) return null;
        return preset.BossesBeforeExtract == 0
            ? ExpeditionFirstCheckpointLimit
            : TimeSpan.FromMinutes(
                15d * preset.BossesBeforeExtract);
    }

    public static void ThrowIfExceeded(
        TimeSpan elapsed,
        TimeSpan? limit,
        string route)
    {
        if (limit is null || elapsed < limit.Value) return;

        throw new TimeoutException(
            $"{route} did not reach Victory or Defeat within " +
            $"{Format(limit.Value)}. The match is treated as stalled so " +
            "Roblox can be restarted and the same incomplete task retried.");
    }

    private static string Format(TimeSpan value) =>
        value.TotalMinutes == 1
            ? "1 minute"
            : $"{value.TotalMinutes:0} minutes";
}
