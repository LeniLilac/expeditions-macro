using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeNavigationInputGateTests
{
    [Fact]
    public void StaticScreen_StopsAfterThreeVerifiedInputAttempts()
    {
        ChallengeNavigationInputGate gate = new(stableDetections: 2);

        for (int expected = 1;
             expected <=
             ChallengeNavigationInputGate.MaximumAttemptsPerOwner;
             expected++)
        {
            Assert.Null(Observe(gate));
            ChallengeNavigationInputAttempt attempt =
                AssertAttempt(Observe(gate));
            Assert.Equal(expected, attempt.Number);
        }

        Assert.Null(Observe(gate));
        RobloxUiUnavailableException error =
            Assert.Throws<RobloxUiUnavailableException>(
                () => Observe(gate));

        Assert.Contains(
            "3 Challenge tile attempts",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DelayedStateTransition_AllowsTheNewOwnedAction()
    {
        ChallengeNavigationInputGate gate = new(stableDetections: 2);

        Assert.Null(Observe(gate));
        Assert.Equal(1, AssertAttempt(Observe(gate)).Number);
        Assert.Null(Observe(gate));
        Assert.Equal(2, AssertAttempt(Observe(gate)).Number);

        Assert.Null(
            Observe(
                gate,
                ChallengeNavigationInputOwner.PostMatchPreview,
                (668, 377)));
        ChallengeNavigationInputAttempt transitioned =
            AssertAttempt(
                Observe(
                    gate,
                    ChallengeNavigationInputOwner.PostMatchPreview,
                    (668, 377)));

        Assert.Equal(
            ChallengeNavigationInputOwner.PostMatchPreview,
            transitioned.Owner);
        Assert.Equal(1, transitioned.Number);
    }

    [Fact]
    public void VerifiedStateChanges_ResetTheAttemptBudget()
    {
        ChallengeNavigationInputGate gate = new(stableDetections: 2);

        for (int expected = 1; expected <= 3; expected++)
        {
            Assert.Null(Observe(gate));
            Assert.Equal(expected, AssertAttempt(Observe(gate)).Number);
        }

        Assert.Null(
            Observe(
                gate,
                ChallengeNavigationInputOwner.AfkRecovery,
                (404, 360)));
        Assert.Equal(
            1,
            AssertAttempt(
                Observe(
                    gate,
                    ChallengeNavigationInputOwner.AfkRecovery,
                    (404, 360))).Number);
        Assert.Null(Observe(gate));

        Assert.Equal(1, AssertAttempt(Observe(gate)).Number);
    }

    [Fact]
    public void PendingSlowConfirmation_DoesNotAuthorizeDuplicateInput()
    {
        ChallengeNavigationInputGate gate = new(stableDetections: 3);

        Assert.Null(Observe(gate));
        Assert.True(gate.HasPendingCandidate);
        Assert.Null(Observe(gate));
        Assert.True(gate.HasPendingCandidate);
        Assert.Equal(1, AssertAttempt(Observe(gate)).Number);
        Assert.False(gate.HasPendingCandidate);

        Assert.Null(Observe(gate));
        Assert.True(gate.HasPendingCandidate);
    }

    [Fact]
    public void CancellationBeforeAuthorization_DoesNotAddAnAttempt()
    {
        ChallengeNavigationInputGate gate = new(stableDetections: 2);
        using CancellationTokenSource cancellation = new();

        Assert.Null(Observe(gate));
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => gate.Observe(
                ChallengeNavigationInputOwner.GameModeSelector,
                (480, 205),
                cancellation.Token));

        ChallengeNavigationInputAttempt attempt =
            AssertAttempt(Observe(gate));
        Assert.Equal(1, attempt.Number);
    }

    private static ChallengeNavigationInputAttempt? Observe(
        ChallengeNavigationInputGate gate,
        ChallengeNavigationInputOwner owner =
            ChallengeNavigationInputOwner.GameModeSelector,
        (int X, int Y)? action = null) =>
        gate.Observe(
            owner,
            action ?? (480, 205),
            CancellationToken.None);

    private static ChallengeNavigationInputAttempt AssertAttempt(
        ChallengeNavigationInputAttempt? attempt)
    {
        Assert.True(attempt.HasValue);
        return attempt.Value;
    }
}
