using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Tests;

public sealed class ExpeditionObservationWaitTests
{
    [Fact]
    public async Task SeededPreviewCountsBeforeTheFirstSlowCapture()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse("2026-07-27T12:00:00Z");
        ImageFrame initial = Frame(1);
        ImageFrame fresh = Frame(2);
        int captures = 0;

        ImageFrame? result =
            await ExpeditionMacroRunner.WaitForStablePlayMenuAsync(
                initial,
                () =>
                {
                    captures++;
                    now += TimeSpan.FromSeconds(6);
                    return fresh;
                },
                static _ => Preview(),
                static _ => (401, 522),
                TimeSpan.FromSeconds(3),
                stableDetections: 2,
                () => now,
                (duration, _) =>
                {
                    now += duration;
                    return Task.CompletedTask;
                },
                pollMilliseconds: 180,
                CancellationToken.None);

        Assert.Same(fresh, result);
        Assert.Equal(1, captures);
    }

    [Fact]
    public async Task SlowPreviewCompletesTheConfiguredStabilityCount()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse("2026-07-27T12:00:00Z");
        int captures = 0;

        ImageFrame? result =
            await ExpeditionMacroRunner.WaitForStablePlayMenuAsync(
                initialFrame: null,
                () =>
                {
                    captures++;
                    now += TimeSpan.FromSeconds(6);
                    return Frame((byte)captures);
                },
                static _ => Preview(),
                static _ => (401, 522),
                TimeSpan.FromSeconds(3),
                stableDetections: 3,
                () => now,
                (duration, _) =>
                {
                    now += duration;
                    return Task.CompletedTask;
                },
                pollMilliseconds: 180,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, captures);
    }

    [Fact]
    public async Task PendingActionGeometrySurvivesTheSoftDeadline()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse("2026-07-27T12:00:00Z");
        (int X, int Y)[] actions =
        [
            (401, 522),
            (420, 522),
            (420, 522),
        ];
        int captures = 0;

        ImageFrame? result =
            await ExpeditionMacroRunner.WaitForStablePlayMenuAsync(
                initialFrame: null,
                () =>
                {
                    captures++;
                    now += TimeSpan.FromSeconds(6);
                    return Frame((byte)captures);
                },
                static _ => Preview(),
                _ => actions[captures - 1],
                TimeSpan.FromSeconds(3),
                stableDetections: 2,
                () => now,
                (duration, _) =>
                {
                    now += duration;
                    return Task.CompletedTask;
                },
                pollMilliseconds: 180,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, captures);
    }

    [Fact]
    public async Task MissingPreviewStillStopsAtTheBoundedDeadline()
    {
        DateTimeOffset now =
            DateTimeOffset.Parse("2026-07-27T12:00:00Z");
        int captures = 0;

        ImageFrame? result =
            await ExpeditionMacroRunner.WaitForStablePlayMenuAsync(
                initialFrame: null,
                () =>
                {
                    captures++;
                    now += TimeSpan.FromSeconds(20);
                    return Frame((byte)captures);
                },
                static _ =>
                    new ChallengeScreenMatch(
                        ChallengeScreenState.None,
                        0),
                static _ => null,
                TimeSpan.FromSeconds(3),
                stableDetections: 2,
                () => now,
                (duration, _) =>
                {
                    now += duration;
                    return Task.CompletedTask;
                },
                pollMilliseconds: 180,
                CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(2, captures);
    }

    private static ChallengeScreenMatch Preview() =>
        new(
            ChallengeScreenState.PostMatchPreview,
            0.99);

    private static ImageFrame Frame(byte value) =>
        new(
            1,
            1,
            PixelFormat.Gray8,
            [value]);
}
