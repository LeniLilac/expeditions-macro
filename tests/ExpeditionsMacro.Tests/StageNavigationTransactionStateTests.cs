using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class StageNavigationTransactionStateTests
{
    [Fact]
    public void StaticVerifiedActionStopsAfterThreeAttempts()
    {
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity action =
            new("afk", "Return to Lobby");

        for (int attempt = 1;
             attempt <=
                StageNavigationTransactionState
                    .MaximumAttemptsPerAction;
             attempt++)
        {
            transaction.ObserveVerified(action);
            Assert.Equal(
                attempt,
                transaction.BeginAttempt(
                    action,
                    CancellationToken.None));
        }

        transaction.ObserveVerified(action);
        RobloxUiUnavailableException error =
            Assert.Throws<RobloxUiUnavailableException>(
                () => transaction.BeginAttempt(
                    action,
                    CancellationToken.None));

        Assert.Equal(3, transaction.Attempts);
        Assert.Contains("'afk'", error.Message);
        Assert.Contains(
            "3 verified 'Return to Lobby' attempts",
            error.Message);
    }

    [Fact]
    public void DelayedTransitionCanSucceedOnTheFinalBoundedAttempt()
    {
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity action =
            new("PostMatchPreview", "Change Gamemode");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            transaction.ObserveVerified(action);
            _ = transaction.BeginAttempt(
                action,
                CancellationToken.None);
        }

        transaction.ObserveVerifiedState("GameModeSelector");

        Assert.Equal(0, transaction.Attempts);
        Assert.False(transaction.ConfirmationPending);
    }

    [Fact]
    public void VerifiedStateTransitionResetsTheConsecutiveBudget()
    {
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity storyBack =
            new("StorySelector", "Back");
        StageNavigationActionIdentity raidBack =
            new("RaidSelector", "Back");

        transaction.ObserveVerified(storyBack);
        _ = transaction.BeginAttempt(
            storyBack,
            CancellationToken.None);
        transaction.ObserveVerified(storyBack);
        _ = transaction.BeginAttempt(
            storyBack,
            CancellationToken.None);

        transaction.ObserveVerifiedState("RaidSelector");
        transaction.ObserveVerified(raidBack);

        Assert.Equal(
            1,
            transaction.BeginAttempt(
                raidBack,
                CancellationToken.None));
    }

    [Fact]
    public void PendingConfirmationCannotDuplicateInput()
    {
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity action =
            new("disconnect", "Reconnect");
        transaction.ObserveVerified(action);
        _ = transaction.BeginAttempt(
            action,
            CancellationToken.None);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(
                () => transaction.BeginAttempt(
                    action,
                    CancellationToken.None));

        Assert.True(transaction.ConfirmationPending);
        Assert.Equal(1, transaction.Attempts);
        Assert.Contains("still waiting", error.Message);
    }

    [Fact]
    public void StableReproofCompletesPendingConfirmationBeforeRetry()
    {
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity action =
            new("StorySelector", "Back");
        transaction.ObserveVerified(action);
        _ = transaction.BeginAttempt(
            action,
            CancellationToken.None);

        transaction.ObserveVerified(action);

        Assert.False(transaction.ConfirmationPending);
        Assert.Equal(
            2,
            transaction.BeginAttempt(
                action,
                CancellationToken.None));
    }

    [Fact]
    public void PendingSlowConfirmationBlocksAlternateNavigationInput()
    {
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity action =
            StageNavigationTransactionState.ForVerifiedNavigation(
                StageScreenState.PostMatchPreview,
                hasChangeModeAction: true)!.Value;
        transaction.ObserveVerified(action);
        _ = transaction.BeginAttempt(
            action,
            CancellationToken.None);

        Assert.Equal(
            GameModeHandoffCommand.Wait,
            StageNavigationPolicy.SelectGameModeHandoffCommand(
                StageScreenState.PostMatchPreview,
                hasStageChangeModeAction: false,
                selectorEvidencePending:
                    transaction.ConfirmationPending));

        transaction.ObserveVerified(action);

        Assert.Equal(
            GameModeHandoffCommand.ChangeGamemode,
            StageNavigationPolicy.SelectGameModeHandoffCommand(
                StageScreenState.PostMatchPreview,
                hasStageChangeModeAction: true,
                selectorEvidencePending:
                    transaction.ConfirmationPending));
    }

    [Fact]
    public void CancellationBeforeAnAttemptDoesNotAuthorizeInput()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        StageNavigationTransactionState transaction = new();
        StageNavigationActionIdentity action =
            new("RaidSelector", "Back");
        transaction.ObserveVerified(action);

        Assert.ThrowsAny<OperationCanceledException>(
            () => transaction.BeginAttempt(
                action,
                cancellation.Token));

        Assert.Equal(0, transaction.Attempts);
        Assert.False(transaction.ConfirmationPending);
    }
}
