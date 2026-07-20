using ExpeditionsMacro.Automation.Expeditions;

namespace ExpeditionsMacro.Tests;

public sealed class ConfirmationDismissalStateTests
{
    [Fact]
    public void PersistentDialog_AllowsExactlyThreeVerifiedAttempts()
    {
        ConfirmationDismissalState transaction = new();

        for (int attempt = 1; attempt <= ConfirmationDismissalState.MaximumAttempts; attempt++)
        {
            Assert.True(transaction.TryBeginAttempt());
            Assert.Equal(attempt, transaction.Attempts);
            Assert.True(transaction.TryMarkStillVisible());
        }

        Assert.Equal(ConfirmationDismissalPhase.Exhausted, transaction.Phase);
        Assert.False(transaction.TryBeginAttempt());
        Assert.False(transaction.TryComplete());
    }

    [Fact]
    public void DismissedDialog_CannotAuthorizeAnotherClick()
    {
        ConfirmationDismissalState transaction = new();

        Assert.True(transaction.TryBeginAttempt());
        Assert.False(transaction.TryBeginAttempt());
        Assert.True(transaction.TryComplete());

        Assert.Equal(ConfirmationDismissalPhase.Completed, transaction.Phase);
        Assert.False(transaction.TryBeginAttempt());
        Assert.False(transaction.TryMarkStillVisible());
    }
}
