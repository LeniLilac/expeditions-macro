using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Expeditions;

public static class ExpeditionRunPolicy
{
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
