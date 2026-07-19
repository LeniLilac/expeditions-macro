using ExpeditionsMacro.Automation.Expeditions;

namespace ExpeditionsMacro.Tests;

public sealed class ExtractionTransactionStateTests
{
    [Fact]
    public void RepeatedObservations_CannotAuthorizeDuplicateExtractionClicks()
    {
        ExtractionTransactionState transaction = new();

        Assert.True(transaction.TryBegin());
        Assert.False(transaction.TryBegin());
        Assert.Equal(ExtractionTransactionPhase.AwaitingConfirmation, transaction.Phase);

        Assert.True(transaction.TryConfirm());
        Assert.False(transaction.TryConfirm());
        Assert.False(transaction.TryBegin());
        Assert.Equal(ExtractionTransactionPhase.AwaitingDismissal, transaction.Phase);
    }

    [Fact]
    public void Completion_IsAllowedOnlyAfterBeginAndConfirm()
    {
        ExtractionTransactionState transaction = new();

        Assert.False(transaction.TryConfirm());
        Assert.False(transaction.TryComplete());
        Assert.True(transaction.TryBegin());
        Assert.False(transaction.TryComplete());
        Assert.True(transaction.TryConfirm());
        Assert.True(transaction.TryComplete());
        Assert.False(transaction.TryComplete());
        Assert.Equal(ExtractionTransactionPhase.Completed, transaction.Phase);
    }
}
