using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Expeditions;

public static class ExpeditionRunPolicy
{
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
}
