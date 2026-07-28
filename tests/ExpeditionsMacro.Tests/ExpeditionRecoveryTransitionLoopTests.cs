using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class ExpeditionRecoveryTransitionLoopTests
{
    [Fact]
    public async Task StaticStateNeverExceedsThePerStateAttemptCap()
    {
        int attempts = 0;

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<RobloxUiUnavailableException>(
                () => ExpeditionRecoveryTransitionLoop.RunAsync(
                    "play",
                    IsComplete,
                    (state, _) =>
                    {
                        Assert.Equal("play", state);
                        attempts++;
                        return Task.FromResult<string?>(null);
                    },
                    CancellationToken.None));

        Assert.Equal(
            ExpeditionRecoveryTransitionLoop.MaximumAttemptsPerState,
            attempts);
        Assert.Contains("'play'", error.Message);
        Assert.Contains("3 consecutive attempts", error.Message);
    }

    [Fact]
    public async Task DelayedTransitionCanSucceedOnTheFinalBoundedAttempt()
    {
        int attempts = 0;

        string result =
            await ExpeditionRecoveryTransitionLoop.RunAsync(
                "continue",
                IsComplete,
                (_, _) =>
                {
                    attempts++;
                    return Task.FromResult<string?>(
                        attempts ==
                            ExpeditionRecoveryTransitionLoop
                                .MaximumAttemptsPerState
                            ? "start"
                            : null);
                },
                CancellationToken.None);

        Assert.Equal("start", result);
        Assert.Equal(
            ExpeditionRecoveryTransitionLoop.MaximumAttemptsPerState,
            attempts);
    }

    [Fact]
    public async Task ARealStateChangeResetsTheConsecutiveStateBudget()
    {
        Dictionary<string, int> attempts =
            new(StringComparer.OrdinalIgnoreCase);

        string result =
            await ExpeditionRecoveryTransitionLoop.RunAsync(
                "afk",
                IsComplete,
                (state, _) =>
                {
                    attempts[state] =
                        attempts.GetValueOrDefault(state) + 1;
                    return Task.FromResult<string?>(
                        (state, attempts[state]) switch
                        {
                            ("afk", 3) => "play",
                            ("play", 3) => "start",
                            _ => null,
                        });
                },
                CancellationToken.None);

        Assert.Equal("start", result);
        Assert.Equal(3, attempts["afk"]);
        Assert.Equal(3, attempts["play"]);
    }

    [Fact]
    public async Task CyclingStatesStillStopsAtTheOverallAttemptCap()
    {
        int attempts = 0;

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<RobloxUiUnavailableException>(
                () => ExpeditionRecoveryTransitionLoop.RunAsync(
                    "play",
                    IsComplete,
                    (state, _) =>
                    {
                        attempts++;
                        return Task.FromResult<string?>(
                            state == "play"
                                ? "map_select"
                                : "play");
                    },
                    CancellationToken.None));

        Assert.Equal(
            ExpeditionRecoveryTransitionLoop.MaximumTotalAttempts,
            attempts);
        Assert.Contains("18 total transition attempts", error.Message);
    }

    [Fact]
    public async Task CancellationAfterAnAttemptPreventsFurtherInput()
    {
        using CancellationTokenSource cancellation = new();
        int inputs = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ExpeditionRecoveryTransitionLoop.RunAsync(
                "map_preview",
                IsComplete,
                (_, _) =>
                {
                    inputs++;
                    cancellation.Cancel();
                    return Task.FromResult<string?>(null);
                },
                cancellation.Token));

        Assert.Equal(1, inputs);
    }

    private static bool IsComplete(string state) =>
        state.Equals(
            "start",
            StringComparison.OrdinalIgnoreCase);
}
