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
}
