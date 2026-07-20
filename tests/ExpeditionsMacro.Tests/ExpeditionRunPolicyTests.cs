using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Geometry;
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

    [Theory]
    [InlineData("afk", true)]
    [InlineData("disconnect", true)]
    [InlineData("lobby", true)]
    [InlineData("play", false)]
    [InlineData("map_select", false)]
    [InlineData("map_preview", false)]
    [InlineData("reward", false)]
    public void OnlyRootRecoveryScreensCanInterruptAnActiveRun(string state, bool expected)
    {
        Assert.Equal(expected, ExpeditionRunPolicy.CanEnterRecoveryDuringRun(state));
    }

    [Fact]
    public void StartOutranksNavigationAndRewardCollisions()
    {
        DetectorPackManifest manifest = Manifest("start", "reward", "play");
        Dictionary<string, double> scores = new()
        {
            ["start"] = 0.91,
            ["reward"] = 0.94,
            ["play"] = 0.99,
        };

        Assert.Equal("start", ExpeditionRunPolicy.PreferActiveState(manifest, scores, "play"));
    }

    [Fact]
    public void RewardOutranksFalsePlayDuringGameplay()
    {
        DetectorPackManifest manifest = Manifest("reward", "play");
        Dictionary<string, double> scores = new()
        {
            ["reward"] = 0.88,
            ["play"] = 0.97,
        };

        Assert.Equal("reward", ExpeditionRunPolicy.PreferActiveState(manifest, scores, "play"));
    }

    [Theory]
    [InlineData("play")]
    [InlineData("map_select")]
    [InlineData("map_preview")]
    public void NavigationScreensAreIgnoredWithoutARecoveryRoot(string navigationState)
    {
        DetectorPackManifest manifest = Manifest(navigationState);
        Dictionary<string, double> scores = new() { [navigationState] = 0.99 };

        Assert.Null(ExpeditionRunPolicy.PreferActiveState(manifest, scores, navigationState));
    }

    [Fact]
    public void StableRecoveryRootOutranksInMatchFalsePositive()
    {
        DetectorPackManifest manifest = Manifest("lobby", "reward");
        Dictionary<string, double> scores = new()
        {
            ["lobby"] = 0.92,
            ["reward"] = 0.96,
        };

        Assert.Equal("lobby", ExpeditionRunPolicy.PreferActiveState(manifest, scores, "reward"));
    }

    [Fact]
    public void DesiredStateBeatsClassifierOrdering()
    {
        DetectorPackManifest manifest = Manifest("start", "reward");
        Dictionary<string, double> scores = new()
        {
            ["start"] = 0.86,
            ["reward"] = 0.95,
        };

        Assert.Equal("start", ExpeditionRunPolicy.PreferDesiredState(manifest, scores, "start", "reward"));
    }

    [Fact]
    public void RecoveryTransitionStopsAtRecognizedPrestart()
    {
        DetectorPackManifest manifest = Manifest("start", "play");
        Dictionary<string, double> scores = new()
        {
            ["start"] = 0.93,
            ["play"] = 0.98,
        };

        Assert.Equal("start", ExpeditionRunPolicy.RecoveryTransition(manifest, scores, "play"));
    }

    [Fact]
    public void RecoveryTransitionAcceptsStandaloneContinueOnlyAfterTeleportPreview()
    {
        DetectorPackManifest manifest = Manifest("continue");
        Dictionary<string, double> scores = new() { ["continue"] = 0.99 };

        Assert.Null(ExpeditionRunPolicy.RecoveryTransition(manifest, scores, recoveryState: null));
        Assert.Equal(
            "continue",
            ExpeditionRunPolicy.RecoveryTransition(manifest, scores, recoveryState: null, allowStandaloneContinue: true));
    }

    [Fact]
    public void RecoveryTransitionKeepsMapPreviewAheadOfItsGreenActionButton()
    {
        DetectorPackManifest manifest = Manifest("map_preview", "continue");
        Dictionary<string, double> scores = new()
        {
            ["map_preview"] = 0.94,
            ["continue"] = 0.99,
        };

        Assert.Equal(
            "map_preview",
            ExpeditionRunPolicy.RecoveryTransition(manifest, scores, "map_preview", allowStandaloneContinue: true));
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

    private static DetectorPackManifest Manifest(params string[] states) => new()
    {
        PackId = "test-pack",
        Version = "1.0.0",
        GameId = "test-game",
        ModeId = "test-mode",
        MinimumAppVersion = "1.0.0",
        ClientWidth = 808,
        ClientHeight = 611,
        States = states.Select(name => new DetectorStateDefinition
        {
            Name = name,
            Regions = Array.Empty<DetectorRegionReference>(),
            ActionX = 0,
            ActionY = 0,
            Threshold = 0.80,
        }).ToArray(),
        MapSelections = Array.Empty<SelectionDetectorDefinition>(),
        DifficultySelections = Array.Empty<SelectionDetectorDefinition>(),
        NodeHuePrototypes = new Dictionary<string, double>(),
        NodeHueRegion = new ScreenRegion(0, 0, 1, 1),
        EmptyHotbarReferenceFile = "empty.png",
        ExtraActions = new Dictionary<string, int[]>(),
        Files = Array.Empty<DetectorPackFile>(),
        BuiltAt = DateTimeOffset.UnixEpoch,
    };
}
