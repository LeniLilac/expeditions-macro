using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class ExpeditionRunPolicyTests
{
    [Fact]
    public void DisabledExtraction_NeverExtractsOrReportsEarlyDefeat()
    {
        ExpeditionPreset preset = Preset(extract: false, target: 1);

        Assert.False(ExpeditionRunPolicy.ShouldExtract(preset, 99));
        Assert.False(ExpeditionRunPolicy.IsEarlyDefeat(preset, 0));
    }

    [Fact]
    public void ZeroTarget_ExtractsAtFirstRealCheckpoint()
    {
        ExpeditionPreset preset = Preset(extract: true, target: 0);

        Assert.True(ExpeditionRunPolicy.ShouldExtract(preset, 0));
        Assert.False(ExpeditionRunPolicy.IsEarlyDefeat(preset, 0));
    }

    [Fact]
    public void BossTarget_WaitsUntilRequestedBossCount()
    {
        ExpeditionPreset preset = Preset(extract: true, target: 2);

        Assert.False(ExpeditionRunPolicy.ShouldExtract(preset, 1));
        Assert.True(ExpeditionRunPolicy.IsEarlyDefeat(preset, 1));
        Assert.True(ExpeditionRunPolicy.ShouldExtract(preset, 2));
        Assert.False(ExpeditionRunPolicy.IsEarlyDefeat(preset, 2));
    }

    private static ExpeditionPreset Preset(bool extract, int target) => new()
    {
        Id = "test",
        Name = "Test",
        CameraModelId = "camera",
        PlacementModelId = "placement",
        ExtractAtCheckpoint = extract,
        BossesBeforeExtract = target,
    };
}
