using ExpeditionsMacro.Automation.Runtime;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class MatchRuntimePolicyTests
{
    [Fact]
    public void FifteenWaveModesUseTwelveMinuteLimit()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            MatchRuntimePolicy.ChallengeLimit());
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            MatchRuntimePolicy.StageLimit(Story(), null));
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            MatchRuntimePolicy.StageLimit(
                Story() with { RunKind = StoryRunKind.Mastery },
                null));
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            MatchRuntimePolicy.StageLimit(null, Raid()));
    }

    [Fact]
    public void InfiniteStoryHasNoRuntimeLimit()
    {
        TimeSpan? limit = MatchRuntimePolicy.StageLimit(
            Story() with { RunKind = StoryRunKind.Infinite },
            null);

        Assert.Null(limit);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 15)]
    [InlineData(2, 30)]
    [InlineData(3, 45)]
    public void ExpeditionLimitScalesWithBossTarget(
        int bosses,
        int expectedMinutes)
    {
        ExpeditionPreset preset = Expedition() with
        {
            BossesBeforeExtract = bosses,
        };

        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            MatchRuntimePolicy.ExpeditionLimit(preset));
    }

    [Fact]
    public void ExpeditionWithoutExtractionHasNoRuntimeLimit()
    {
        ExpeditionPreset preset = Expedition() with
        {
            ExtractAtCheckpoint = false,
        };

        Assert.Null(MatchRuntimePolicy.ExpeditionLimit(preset));
    }

    [Fact]
    public void ExpiredLimitCreatesRecoverableTimeout()
    {
        TimeoutException error = Assert.Throws<TimeoutException>(() =>
            MatchRuntimePolicy.ThrowIfExceeded(
                TimeSpan.FromMinutes(12),
                TimeSpan.FromMinutes(12),
                "Challenge match"));

        Assert.Contains(
            "same incomplete task retried",
            error.Message,
            StringComparison.Ordinal);
    }

    private static StoryPreset Story() => new()
    {
        Id = "story",
        Name = "Story",
    };

    private static RaidPreset Raid() => new()
    {
        Id = "raid",
        Name = "Raid",
    };

    private static ExpeditionPreset Expedition() => new()
    {
        Id = "expedition",
        Name = "Expedition",
        CameraModelId = "camera",
        PlacementModelId = "placement",
    };
}
