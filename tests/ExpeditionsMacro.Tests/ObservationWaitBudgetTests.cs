using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class ObservationWaitBudgetTests
{
    [Fact]
    public void RequiredObservationsSurviveTheSoftDeadline()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(3),
            minimumObservations: 2,
            () => now);

        Assert.True(budget.ShouldObserve());
        now += TimeSpan.FromSeconds(6);
        budget.MarkObserved();
        Assert.True(budget.ShouldObserve());
        now += TimeSpan.FromSeconds(6);
        budget.MarkObserved();
        Assert.False(budget.ShouldObserve());
    }

    [Fact]
    public void LongSoftTimeoutIsNotShortenedByTheHardDeadline()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ObservationWaitBudget budget = new(
            TimeSpan.FromMinutes(3),
            minimumObservations: 3,
            () => now);

        now += TimeSpan.FromMinutes(2);

        Assert.True(budget.ShouldObserve());
    }

    [Fact]
    public void ShortUiCheckRetainsAMinimumLoadingWindow()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(2),
            minimumObservations: 2,
            () => now);

        budget.MarkObserved();
        budget.MarkObserved();
        now += TimeSpan.FromSeconds(10);

        Assert.True(budget.ShouldObserve());

        now += TimeSpan.FromSeconds(2);

        Assert.False(budget.ShouldObserve());
    }

    [Fact]
    public void ExtendedSoftTimeoutRetainsABoundedObservationGrace()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(35),
            minimumObservations: 3,
            () => now);

        budget.ExtendSoftTimeout(
            TimeSpan.FromMinutes(3));
        now += TimeSpan.FromMinutes(3);
        budget.MarkObserved();

        Assert.True(budget.ShouldObserve());

        now += TimeSpan.FromSeconds(61);

        Assert.False(budget.ShouldObserve());
    }

    [Fact]
    public void PendingRecoveryLoadUsesTheExistingHardGrace()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-30T13:28:05Z");
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(20),
            minimumObservations: 2,
            () => now);
        budget.MarkObserved();
        budget.MarkObserved();
        now += TimeSpan.FromSeconds(40);

        Assert.False(budget.ShouldObserve());
        Assert.True(
            budget.ShouldObserve(
                ExpeditionMacroRunner
                    .IsRecoveryTransitionPending(
                        "map_preview",
                        hasStableCandidate: false)));

        now += TimeSpan.FromSeconds(41);

        Assert.False(
            budget.ShouldObserve(
                ExpeditionMacroRunner
                    .IsRecoveryTransitionPending(
                        "map_preview",
                        hasStableCandidate: false)));
        Assert.False(
            ExpeditionMacroRunner
                .IsRecoveryTransitionPending(
                    string.Empty,
                    hasStableCandidate: false));
    }

    [Fact]
    public async Task SlowContinueActionGetsItsSecondStableObservation()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ImageFrame frame = new(
            1,
            1,
            PixelFormat.Gray8,
            [0]);
        int captures = 0;

        (int X, int Y) action =
            await ExpeditionMacroRunner
                .WaitForStableActionAsync(
                    "continue",
                    initialFrame: null,
                    capture: () =>
                    {
                        captures++;
                        now += TimeSpan.FromSeconds(6);
                        return frame;
                    },
                    isOwned: (_, _) => true,
                    locate: (_, _) => (401, 522),
                    utcNow: () => now,
                    delay: (duration, _) =>
                    {
                        now += duration;
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);

        Assert.Equal((401, 522), action);
        Assert.Equal(2, captures);
    }

    [Fact]
    public async Task StaleManifestCoordinatesOnUnknownFramesDoNotAuthorizeAClick()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ImageFrame frame = new(
            1,
            1,
            PixelFormat.Gray8,
            [0]);
        int locationAttempts = 0;

        await Assert.ThrowsAsync<RobloxUiUnavailableException>(
            () => ExpeditionMacroRunner
                .WaitForStableActionAsync(
                    "continue",
                    initialFrame: frame,
                    capture: () =>
                    {
                        now += TimeSpan.FromSeconds(20);
                        return frame;
                    },
                    isOwned: (_, _) => false,
                    locate: (_, _) =>
                    {
                        locationAttempts++;
                        return (401, 522);
                    },
                    utcNow: () => now,
                    delay: (duration, _) =>
                    {
                        now += duration;
                        return Task.CompletedTask;
                    },
                    CancellationToken.None));

        Assert.Equal(0, locationAttempts);
    }

    [Fact]
    public async Task TwoOwnedFramesAreRequiredAfterAnUnownedFrame()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ImageFrame frame = new(
            1,
            1,
            PixelFormat.Gray8,
            [0]);
        Queue<bool> ownership =
            new([false, true, true]);
        int captures = 0;

        (int X, int Y) action =
            await ExpeditionMacroRunner
                .WaitForStableActionAsync(
                    "continue",
                    initialFrame: null,
                    capture: () =>
                    {
                        captures++;
                        now += TimeSpan.FromSeconds(1);
                        return frame;
                    },
                    isOwned: (_, _) =>
                        ownership.Dequeue(),
                    locate: (_, _) => (401, 522),
                    utcNow: () => now,
                    delay: (duration, _) =>
                    {
                        now += duration;
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);

        Assert.Equal((401, 522), action);
        Assert.Equal(3, captures);
    }

    [Fact]
    public async Task LosingTheOwnerResetsPendingCoordinateEvidence()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ImageFrame frame = new(
            1,
            1,
            PixelFormat.Gray8,
            [0]);
        Queue<bool> ownership =
            new([true, false, true, true]);
        int captures = 0;
        int locations = 0;

        (int X, int Y) action =
            await ExpeditionMacroRunner
                .WaitForStableActionAsync(
                    "continue",
                    initialFrame: null,
                    capture: () =>
                    {
                        captures++;
                        now += TimeSpan.FromSeconds(1);
                        return frame;
                    },
                    isOwned: (_, _) =>
                        ownership.Dequeue(),
                    locate: (_, _) =>
                    {
                        locations++;
                        return (401, 522);
                    },
                    utcNow: () => now,
                    delay: (duration, _) =>
                    {
                        now += duration;
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);

        Assert.Equal((401, 522), action);
        Assert.Equal(4, captures);
        Assert.Equal(3, locations);
    }

    [Fact]
    public async Task InitialOwnedFrameCountsTowardStableActionProof()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
        ImageFrame frame = new(
            1,
            1,
            PixelFormat.Gray8,
            [0]);
        int captures = 0;

        (int X, int Y) action =
            await ExpeditionMacroRunner
                .WaitForStableActionAsync(
                    "continue",
                    initialFrame: frame,
                    capture: () =>
                    {
                        captures++;
                        now += TimeSpan.FromSeconds(6);
                        return frame;
                    },
                    isOwned: (_, _) => true,
                    locate: (_, _) => (401, 522),
                    utcNow: () => now,
                    delay: (duration, _) =>
                    {
                        now += duration;
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);

        Assert.Equal((401, 522), action);
        Assert.Equal(1, captures);
    }

    [Theory]
    [InlineData("map_1", "map_select")]
    [InlineData("map_2", "map_select")]
    [InlineData("map_3", "map_select")]
    [InlineData("difficulty_minus", "map_select")]
    [InlineData("difficulty_plus", "map_select")]
    [InlineData("select_stage", "map_select")]
    [InlineData("extract", "checkpoint")]
    [InlineData("afk", "afk")]
    [InlineData("disconnect", "disconnect")]
    [InlineData("play", "play")]
    [InlineData("map_preview", "map_preview")]
    [InlineData("checkpoint", "checkpoint")]
    [InlineData("continue", "continue")]
    [InlineData("start", "start")]
    [InlineData("reward", "reward")]
    [InlineData("confirm", "confirm")]
    [InlineData("extract_confirm", "extract_confirm")]
    [InlineData("victory", "victory")]
    [InlineData("defeat", "defeat")]
    public void ExpeditionActionsRequireTheirOwningScreen(
        string action,
        string ownerState)
    {
        Assert.Equal(
            ownerState,
            ExpeditionRunPolicy.ActionOwnerState(action));
    }

    [Fact]
    public void UnknownExpeditionActionHasNoImplicitOwner()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ExpeditionRunPolicy.ActionOwnerState(
                "unknown_action"));
    }
}
