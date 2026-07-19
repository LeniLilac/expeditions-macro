using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class ExpeditionRunPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void RecoveryAlwaysRequiresAtLeastTwoConsecutiveDetections(int configured, int expected)
    {
        ExpeditionPreset preset = Preset(extract: false, target: 1) with { StableDetections = configured };

        Assert.Equal(expected, ExpeditionRunPolicy.RecoveryStableDetections(preset));
    }

    [Fact]
    public void OneFrameRecoveryCollision_DoesNotBecomeStable()
    {
        ExpeditionPreset preset = Preset(extract: true, target: 1) with { StableDetections = 1 };
        StableStateTracker<string> tracker = new(ExpeditionRunPolicy.RecoveryStableDetections(preset));

        Assert.Null(tracker.Update("play"));
        Assert.Null(tracker.Update(null));
        Assert.Null(tracker.Update("play"));
        Assert.Equal("play", tracker.Update("play"));
    }

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
