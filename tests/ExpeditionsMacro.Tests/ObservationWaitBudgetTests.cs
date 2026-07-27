using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Imaging;

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
}
