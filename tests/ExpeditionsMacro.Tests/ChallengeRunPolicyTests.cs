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

    [Fact]
    public void DefeatAttempt_IsClearedAtTheNextHalfHourEpoch()
    {
        ChallengeRotationState state = new();
        DateTimeOffset first = new(2026, 7, 19, 12, 10, 0, TimeSpan.FromHours(-4));
        state.Advance(first);
        state.MarkAttempted(ChallengeType.Stat);

        Assert.Contains(ChallengeType.Stat, state.Attempted);
        Assert.True(state.Advance(new DateTimeOffset(2026, 7, 19, 12, 30, 1, TimeSpan.FromHours(-4))));
        Assert.Empty(state.Attempted);
    }

    [Fact]
    public void SeparateScheduledInvocations_SharedStateInfersDailyLimitUntilMidnightUtc()
    {
        ChallengeRotationState operationState = new();
        DateTimeOffset first = new(2026, 7, 19, 20, 10, 0, TimeSpan.Zero);
        DateTimeOffset afterReset = new(2026, 7, 19, 20, 30, 5, TimeSpan.Zero);

        Assert.False(operationState.ObserveAllCooldown(first));
        Assert.True(operationState.ObserveAllCooldown(afterReset));
        Assert.Equal(
            new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            operationState.DailyLimitUntilUtc);

        ChallengeRotationState discardedInvocationState = new();
        Assert.False(discardedInvocationState.ObserveAllCooldown(afterReset));
        Assert.Null(discardedInvocationState.DailyLimitUntilUtc);
    }

    [Fact]
    public void AnyAvailableChallenge_ClearsTheDailyLimitInferenceBaseline()
    {
        ChallengeRotationState state = new();
        DateTimeOffset first = new(2026, 7, 19, 20, 10, 0, TimeSpan.Zero);
        state.ObserveAllCooldown(first);
        state.ObserveAvailability();

        Assert.False(state.ObserveAllCooldown(new DateTimeOffset(2026, 7, 19, 20, 30, 5, TimeSpan.Zero)));
        Assert.Null(state.DailyLimitUntilUtc);
    }

    [Theory]
    [InlineData(true, 0, 3, "PlayMenu")]
    [InlineData(false, 0, 0, "PlayMenu")]
    [InlineData(false, 0, 1, "RepeatStage")]
    [InlineData(false, 1, 1, "PlayMenu")]
    public void TerminalContinuation_OnlyRepeatsDefeatsWithRetriesRemaining(
        bool victory,
        int retriesUsed,
        int configuredRetries,
        string expected)
    {
        Assert.Equal(expected, ChallengeRunPolicy.TerminalContinuation(victory, retriesUsed, configuredRetries).ToString());
    }

    private static ChallengePreset Preset() => new()
    {
        Id = "challenge-test",
        Name = "Challenge test",
        Maps = ChallengePreset.EmptyMapProfiles(),
    };
}
