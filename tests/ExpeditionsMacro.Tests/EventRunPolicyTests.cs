using ExpeditionsMacro.Automation.Events;
using ExpeditionsMacro.Vision.Events;

namespace ExpeditionsMacro.Tests;

public sealed class EventRunPolicyTests
{
    [Theory]
    [InlineData("afk")]
    [InlineData("disconnect")]
    [InlineData("lobby")]
    public void RootInterruptions_AreRecoverable(
        string state)
    {
        Assert.Equal(
            state,
            EventRunPolicy.RecoveryCandidate(
                EventScreenState.None,
                state));
    }

    [Fact]
    public void PlaySelector_IsRecoverableEvenWithoutRootState()
    {
        Assert.Equal(
            "play",
            EventRunPolicy.RecoveryCandidate(
                EventScreenState.GameModeSelector,
                null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("prestart")]
    [InlineData("reward")]
    public void OrdinaryStates_DoNotInterruptTheMatch(
        string? state)
    {
        Assert.Null(
            EventRunPolicy.RecoveryCandidate(
                EventScreenState.None,
                state));
    }

    [Fact]
    public void FirstTerminalCandidate_GetsBoundedConfirmationGrace()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        EventTerminalRuntimeGuard guard =
            new(
                stableDetections: 3,
                utcNow: () => now);

        Assert.False(
            guard.ShouldEnforceRuntimeLimit(
                hasTerminalCandidate: true,
                confirmationPending: true));

        now += TimeSpan.FromSeconds(50);

        Assert.True(
            guard.ShouldEnforceRuntimeLimit(
                hasTerminalCandidate: true,
                confirmationPending: true));
    }

    [Fact]
    public void LostTerminalCandidate_RestoresRuntimeLimit()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        EventTerminalRuntimeGuard guard =
            new(
                stableDetections: 2,
                utcNow: () => now);

        Assert.False(
            guard.ShouldEnforceRuntimeLimit(
                hasTerminalCandidate: true,
                confirmationPending: true));
        Assert.True(
            guard.ShouldEnforceRuntimeLimit(
                hasTerminalCandidate: false,
                confirmationPending: false));

        now += TimeSpan.FromSeconds(40);

        Assert.False(
            guard.ShouldEnforceRuntimeLimit(
                hasTerminalCandidate: true,
                confirmationPending: true));
    }
}
