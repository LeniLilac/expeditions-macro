using ExpeditionsMacro.Automation.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class StableScreenActionWaiterTests
{
    [Fact]
    public async Task SlowObservation_CompletesPendingStableAction()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int observations = 0;

        StableScreenAction<TestObservation>? result =
            await StableScreenActionWaiter.WaitAsync(
                desiredState: "prestart",
                stableDetections: 2,
                observe: () =>
                {
                    observations++;
                    return new TestObservation(
                        "prestart",
                        (410, 520));
                },
                stateFor: static observation =>
                    observation.State,
                actionFor: static observation =>
                    observation.Action,
                timeout: TimeSpan.FromSeconds(3),
                pollInterval:
                    TimeSpan.FromMilliseconds(150),
                CancellationToken.None,
                utcNow: () => now,
                delay: (duration, _) =>
                {
                    now += TimeSpan.FromSeconds(20);
                    return Task.CompletedTask;
                });

        Assert.NotNull(result);
        Assert.Equal(2, observations);
        Assert.Equal((410, 520), (
            result.Value.X,
            result.Value.Y));
    }

    [Fact]
    public async Task StateOrMovingAction_ResetsConfiguredStability()
    {
        Queue<TestObservation> observations = new(
        [
            new("other", (100, 200)),
            new("terminal", (100, 200)),
            new("terminal", (108, 200)),
            new("terminal", (109, 201)),
            new("terminal", (110, 202)),
        ]);

        StableScreenAction<TestObservation>? result =
            await StableScreenActionWaiter.WaitAsync(
                desiredState: "terminal",
                stableDetections: 3,
                observe: observations.Dequeue,
                stateFor: static observation =>
                    observation.State,
                actionFor: static observation =>
                    observation.Action,
                timeout: TimeSpan.FromSeconds(12),
                pollInterval: TimeSpan.Zero,
                CancellationToken.None,
                delay: static (_, _) =>
                    Task.CompletedTask);

        Assert.NotNull(result);
        Assert.Equal((110, 202), (
            result.Value.X,
            result.Value.Y));
        Assert.Empty(observations);
    }

    [Fact]
    public async Task MissingAction_StopsAtHardBoundary()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int observations = 0;

        StableScreenAction<TestObservation>? result =
            await StableScreenActionWaiter.WaitAsync(
                desiredState: "prestart",
                stableDetections: 2,
                observe: () =>
                {
                    observations++;
                    return new TestObservation(
                        "prestart",
                        null);
                },
                stateFor: static observation =>
                    observation.State,
                actionFor: static observation =>
                    observation.Action,
                timeout: TimeSpan.FromSeconds(3),
                pollInterval: TimeSpan.Zero,
                CancellationToken.None,
                utcNow: () => now,
                delay: (_, _) =>
                {
                    now += TimeSpan.FromSeconds(50);
                    return Task.CompletedTask;
                });

        Assert.Null(result);
        Assert.Equal(1, observations);
    }

    private readonly record struct TestObservation(
        string State,
        (int X, int Y)? Action);
}
