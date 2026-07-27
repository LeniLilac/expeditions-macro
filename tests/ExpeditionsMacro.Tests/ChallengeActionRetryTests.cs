using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeActionRetryTests
{
    [Fact]
    public async Task SlowActionProof_CannotExceedMaximumParkingAttempts()
    {
        DateTimeOffset now =
            new(2026, 7, 27, 18, 42, 0, TimeSpan.Zero);
        DateTimeOffset startedAt = now;
        int parks = 0;
        int captures = 0;
        ImageFrame frame = new(
            1,
            1,
            PixelFormat.Rgb24,
            new byte[3],
            takeOwnership: true);

        (int X, int Y)? action =
            await ChallengeMacroRunner
                .LocateActionAfterParkingAsync(
                    _ =>
                    {
                        parks++;
                        return Task.CompletedTask;
                    },
                    () =>
                    {
                        captures++;
                        now += TimeSpan.FromSeconds(3);
                        return frame;
                    },
                    _ =>
                        now - startedAt >=
                        TimeSpan.FromSeconds(12)
                            ? (404, 177)
                            : null,
                    retryMilliseconds: 0,
                    maximumAttempts: 3,
                    CancellationToken.None,
                    softTimeout: TimeSpan.FromSeconds(5),
                    utcNow: () => now,
                    delay: (_, _) => Task.CompletedTask);

        Assert.Null(action);
        Assert.Equal(3, parks);
        Assert.Equal(3, captures);
    }

    [Fact]
    public async Task SeededAvailableAction_ReacquiresAfterSlowObscuredFrame()
    {
        ImageFrame initialFrame = Frame();
        ImageFrame confirmedFrame = Frame();
        ChallengeScreenMatch initialMatch = Available();
        Queue<ChallengeScreenMatch> observations = new(
        [
            new(ChallengeScreenState.None, 0),
            Available() with { Confidence = 0.95 },
            Available() with
            {
                Confidence = 0.96,
                ActionX = 580,
            },
        ]);
        DateTimeOffset now =
            new(2026, 7, 27, 19, 8, 0, TimeSpan.Zero);
        int captures = 0;

        (ImageFrame Frame, ChallengeScreenMatch Match)? result =
            await ChallengeMacroRunner
                .WaitForStableActionAsync(
                    ChallengeScreenState.ChallengeAvailable,
                    stableDetections: 2,
                    observe: () =>
                    {
                        captures++;
                        now += captures == 1
                            ? TimeSpan.FromSeconds(13)
                            : TimeSpan.FromSeconds(1);
                        return (
                            confirmedFrame,
                            observations.Dequeue());
                    },
                    timeout: TimeSpan.FromSeconds(2),
                    pollMilliseconds: 0,
                    observed: null,
                    CancellationToken.None,
                    utcNow: () => now,
                    delay: (_, _) => Task.CompletedTask,
                    initialObservation: (
                        initialFrame,
                        initialMatch));

        Assert.NotNull(result);
        Assert.Same(confirmedFrame, result.Value.Frame);
        Assert.Equal(580, result.Value.Match.ActionX);
        Assert.Equal(267, result.Value.Match.ActionY);
        Assert.Equal(3, captures);
        Assert.Empty(observations);
    }

    [Fact]
    public async Task SeededAvailableAction_RejectsChangedOwnerAction()
    {
        ImageFrame frame = Frame();
        DateTimeOffset now =
            new(2026, 7, 27, 19, 8, 0, TimeSpan.Zero);
        int captures = 0;

        (ImageFrame Frame, ChallengeScreenMatch Match)? result =
            await ChallengeMacroRunner
                .WaitForStableActionAsync(
                    ChallengeScreenState.ChallengeAvailable,
                    stableDetections: 2,
                    observe: () =>
                    {
                        captures++;
                        now += TimeSpan.FromSeconds(15);
                        return (
                            frame,
                            new ChallengeScreenMatch(
                                ChallengeScreenState
                                    .ChallengeCooldown,
                                0.97,
                                308,
                                437));
                    },
                    timeout: TimeSpan.FromSeconds(2),
                    pollMilliseconds: 0,
                    observed: null,
                    CancellationToken.None,
                    utcNow: () => now,
                    delay: (_, _) => Task.CompletedTask,
                    initialObservation: (
                        frame,
                        Available()));

        Assert.Null(result);
        Assert.Equal(2, captures);
    }

    private static ChallengeScreenMatch Available() =>
        new(
            ChallengeScreenState.ChallengeAvailable,
            0.94,
            579,
            267);

    private static ImageFrame Frame() =>
        new(
            1,
            1,
            PixelFormat.Rgb24,
            new byte[3],
            takeOwnership: true);
}
