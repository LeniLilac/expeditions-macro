using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Expeditions;

public static class ExpeditionRunPolicy
{
    private static readonly string[] RecoveryRootPriority =
    [
        "afk",
        "disconnect",
        "lobby",
    ];

    private static readonly string[] ActiveStatePriority =
    [
        "defeat",
        "victory",
        "extract_confirm",
        "confirm",
        "checkpoint",
        "continue",
        "start",
        "reward",
    ];

    public static int RecoveryStableDetections(ExpeditionPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return Math.Max(2, preset.StableDetections);
    }

    public static bool ShouldExtract(ExpeditionPreset preset, int bossesSeen)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return preset.ExtractAtCheckpoint && bossesSeen >= Math.Max(0, preset.BossesBeforeExtract);
    }

    public static bool IsEarlyDefeat(ExpeditionPreset preset, int bossesSeen)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return preset.ExtractAtCheckpoint && bossesSeen < Math.Max(0, preset.BossesBeforeExtract);
    }

    public static bool CanEnterRecoveryDuringRun(string? state) => state is "afk" or "disconnect" or "lobby";

    public static bool StopDeadlineReached(DateTimeOffset nowUtc, DateTimeOffset? stopAfterCurrentRunUtc) =>
        stopAfterCurrentRunUtc is DateTimeOffset deadline && nowUtc >= deadline;

    public static string? PreferActiveState(
        DetectorPackManifest manifest,
        IReadOnlyDictionary<string, double> scores,
        string? classified)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(scores);
        foreach (string state in RecoveryRootPriority)
        {
            if (IsStateDetected(manifest, scores, state)) return state;
        }

        foreach (string state in ActiveStatePriority)
        {
            if (IsStateDetected(manifest, scores, state)) return state;
        }

        return classified is "play" or "map_select" or "map_preview" ? null : classified;
    }

    public static string? PreferDesiredState(
        DetectorPackManifest manifest,
        IReadOnlyDictionary<string, double> scores,
        string desired,
        string? classified) =>
        IsStateDetected(manifest, scores, desired)
            ? desired
            : PreferActiveState(manifest, scores, classified);

    public static string? RecoveryTransition(
        DetectorPackManifest manifest,
        IReadOnlyDictionary<string, double> scores,
        string? recoveryState,
        bool allowStandaloneContinue = false)
    {
        if (IsStateDetected(manifest, scores, "start")) return "start";
        if (recoveryState is not null) return recoveryState;
        return allowStandaloneContinue && IsStateDetected(manifest, scores, "continue")
            ? "continue"
            : null;
    }

    public static bool IsStateDetected(
        DetectorPackManifest manifest,
        IReadOnlyDictionary<string, double> scores,
        string state)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(scores);
        DetectorStateDefinition? definition = manifest.States.FirstOrDefault(
            value => value.Name.Equals(state, StringComparison.OrdinalIgnoreCase));
        return definition is not null &&
            scores.TryGetValue(definition.Name, out double score) &&
            score >= definition.Threshold;
    }

    public static string ActionOwnerState(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return action.ToLowerInvariant() switch
        {
            "map_1" or
            "map_2" or
            "map_3" or
            "difficulty_minus" or
            "difficulty_plus" or
            "select_stage" => "map_select",
            "extract" => "checkpoint",
            "afk" or
            "disconnect" or
            "play" or
            "map_preview" or
            "checkpoint" or
            "continue" or
            "start" or
            "reward" or
            "confirm" or
            "extract_confirm" or
            "victory" or
            "defeat" => action.ToLowerInvariant(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "The Expedition action does not have an owning screen state."),
        };
    }
}
