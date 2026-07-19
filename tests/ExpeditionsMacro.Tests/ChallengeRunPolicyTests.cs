using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeRunPolicyTests
{
    [Theory]
    [InlineData(12, 12, 12, 30)]
    [InlineData(12, 29, 12, 30)]
    [InlineData(12, 30, 13, 0)]
    [InlineData(12, 59, 13, 0)]
    public void NextReset_UsesTheGlobalHalfHourBoundaries(
        int hour,
        int minute,
        int expectedHour,
        int expectedMinute)
    {
        DateTimeOffset now = new(2026, 7, 19, hour, minute, 12, TimeSpan.FromHours(-4));

        DateTimeOffset next = ChallengeRunPolicy.NextGlobalReset(now);

        Assert.Equal(expectedHour, next.Hour);
        Assert.Equal(expectedMinute, next.Minute);
        Assert.Equal(0, next.Second);
    }

    [Fact]
    public void NextType_PreservesTheFixedSelectorOrder()
    {
        ChallengePreset preset = Preset();

        Assert.Equal(ChallengeType.Trait, ChallengeRunPolicy.NextType(preset, new HashSet<ChallengeType>()));
        Assert.Equal(ChallengeType.Stat, ChallengeRunPolicy.NextType(preset, new HashSet<ChallengeType> { ChallengeType.Trait }));
        Assert.Equal(ChallengeType.Sprite, ChallengeRunPolicy.NextType(preset, new HashSet<ChallengeType> { ChallengeType.Trait, ChallengeType.Stat }));
        Assert.Null(ChallengeRunPolicy.NextType(preset, new HashSet<ChallengeType>(Enum.GetValues<ChallengeType>())));
    }

    [Fact]
    public void DelayedPlacement_RequiresAConfiguredModelAndElapsedDelay()
    {
        ChallengeMapProfile profile = new()
        {
            Map = ChallengeMapId.SchoolGrounds,
            DelayedPlacementModelId = "defenders",
            DelayedPlacementSeconds = 45,
        };

        Assert.False(ChallengeRunPolicy.IsDelayedPlacementDue(profile, TimeSpan.FromSeconds(44)));
        Assert.True(ChallengeRunPolicy.IsDelayedPlacementDue(profile, TimeSpan.FromSeconds(45)));
        Assert.False(ChallengeRunPolicy.IsDelayedPlacementDue(profile with { DelayedPlacementModelId = string.Empty }, TimeSpan.FromMinutes(2)));
    }

    private static ChallengePreset Preset() => new()
    {
        Id = "challenge-test",
        Name = "Challenge test",
        Maps = ChallengePreset.EmptyMapProfiles(),
    };
}
