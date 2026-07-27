using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Tests;

public sealed class ExpeditionGameModeHandoffLoopTests
{
    [Fact]
    public async Task StaticPartyNeverExceedsThreeChangeGamemodeClicks()
    {
        Harness harness = new(
            Enumerable.Repeat(
                ChallengeScreenState.PostMatchPreview,
                32));
        harness.CaptureDuration = TimeSpan.FromSeconds(10);

        TimeoutException error =
            await Assert.ThrowsAsync<TimeoutException>(
                () => harness.RunAsync());

        Assert.Equal(
            ExpeditionGameModeHandoffLoop
                .MaximumChangeGamemodeAttempts,
            harness.Clicks);
        Assert.Equal([2, 4, 6], harness.ClickCaptureCounts);
        Assert.Equal([1, 2, 3], harness.ClickAttempts);
        Assert.Contains(
            "PostMatchPreview",
            error.Message,
            StringComparison.Ordinal);
        Assert.True(
            harness.Now - harness.StartedAt <=
                TimeSpan.FromSeconds(150));
    }

    [Fact]
    public async Task DelayedTransitionCanFinishAfterTheClickCap()
    {
        Harness harness = new(
        [
            .. Enumerable.Repeat(
                ChallengeScreenState.PostMatchPreview,
                12),
            ChallengeScreenState.GameModeSelector,
            ChallengeScreenState.GameModeSelector,
        ]);

        ChallengeScreenMatch result =
            await harness.RunAsync();

        Assert.Equal(
            ChallengeScreenState.GameModeSelector,
            result.State);
        Assert.Equal(
            ExpeditionGameModeHandoffLoop
                .MaximumChangeGamemodeAttempts,
            harness.Clicks);
    }

    [Fact]
    public async Task PendingConfirmationCannotDuplicateTheClick()
    {
        Harness harness = new(
        [
            ChallengeScreenState.PostMatchPreview,
            ChallengeScreenState.PostMatchPreview,
            ChallengeScreenState.PostMatchPreview,
            ChallengeScreenState.GameModeSelector,
            ChallengeScreenState.GameModeSelector,
        ]);

        ChallengeScreenMatch result =
            await harness.RunAsync();

        Assert.Equal(
            ChallengeScreenState.GameModeSelector,
            result.State);
        Assert.Equal(1, harness.Clicks);
        Assert.Equal([2], harness.ClickCaptureCounts);
    }

    [Fact]
    public async Task CancellationAfterClickPreventsAnotherInput()
    {
        using CancellationTokenSource cancellation = new();
        Harness harness = new(
            Enumerable.Repeat(
                ChallengeScreenState.PostMatchPreview,
                12),
            cancellation);
        harness.CancelAfterClick = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.RunAsync());

        Assert.Equal(1, harness.Clicks);
    }

    [Fact]
    public async Task VerifiedStateChangeResetsThePartyClickBudget()
    {
        Harness harness = new(
        [
            .. Enumerable.Repeat(
                ChallengeScreenState.PostMatchPreview,
                6),
            ChallengeScreenState.Victory,
            ChallengeScreenState.Victory,
            ChallengeScreenState.PostMatchPreview,
            ChallengeScreenState.GameModeSelector,
            ChallengeScreenState.GameModeSelector,
        ]);
        harness.PlayMenuFrame =
            Frame(ChallengeScreenState.PostMatchPreview);

        ChallengeScreenMatch result =
            await harness.RunAsync();

        Assert.Equal(
            ChallengeScreenState.GameModeSelector,
            result.State);
        Assert.Equal(4, harness.Clicks);
        Assert.Equal([1, 2, 3, 1], harness.ClickAttempts);
        Assert.Equal(1, harness.PlayMenuOpens);
    }

    [Fact]
    public async Task UnclassifiedFlickerDoesNotResetThePartyClickBudget()
    {
        Harness harness = new(
        [
            .. Enumerable.Repeat(
                ChallengeScreenState.PostMatchPreview,
                6),
            ChallengeScreenState.None,
            ChallengeScreenState.None,
            .. Enumerable.Repeat(
                ChallengeScreenState.PostMatchPreview,
                4),
            ChallengeScreenState.GameModeSelector,
            ChallengeScreenState.GameModeSelector,
        ]);

        ChallengeScreenMatch result =
            await harness.RunAsync();

        Assert.Equal(
            ChallengeScreenState.GameModeSelector,
            result.State);
        Assert.Equal(
            ExpeditionGameModeHandoffLoop
                .MaximumChangeGamemodeAttempts,
            harness.Clicks);
    }

    private sealed class Harness
    {
        private readonly Queue<ChallengeScreenState> _states;
        private readonly CancellationTokenSource? _cancellation;
        private ChallengeScreenState _lastState;

        public Harness(
            IEnumerable<ChallengeScreenState> states,
            CancellationTokenSource? cancellation = null)
        {
            _states = new Queue<ChallengeScreenState>(states);
            _cancellation = cancellation;
            StartedAt = DateTimeOffset.Parse(
                "2026-07-27T12:00:00Z");
            Now = StartedAt;
        }

        public DateTimeOffset StartedAt { get; }

        public DateTimeOffset Now { get; private set; }

        public int Captures { get; private set; }

        public int Clicks { get; private set; }

        public List<int> ClickCaptureCounts { get; } = [];

        public List<int> ClickAttempts { get; } = [];

        public int PlayMenuOpens { get; private set; }

        public bool CancelAfterClick { get; set; }

        public TimeSpan CaptureDuration { get; set; } =
            TimeSpan.FromSeconds(1);

        public ImageFrame? PlayMenuFrame { get; set; }

        public Task<ChallengeScreenMatch> RunAsync() =>
            ExpeditionGameModeHandoffLoop.RunAsync(
                initialFrame: null,
                Capture,
                Detect,
                LocateChangeGamemode,
                ClickChangeGamemodeAsync,
                OpenPlayMenuAsync,
                TimeSpan.FromSeconds(90),
                stableDetections: 2,
                pollMilliseconds: 180,
                () => Now,
                DelayAsync,
                _cancellation?.Token ?? CancellationToken.None);

        private ImageFrame Capture()
        {
            Captures++;
            Now += CaptureDuration;
            if (_states.TryDequeue(out ChallengeScreenState state))
            {
                _lastState = state;
            }

            return Frame(_lastState);
        }

        private static ChallengeScreenMatch Detect(
            ImageFrame frame)
        {
            ChallengeScreenState state =
                (ChallengeScreenState)frame.Pixels[0];
            return new ChallengeScreenMatch(
                state,
                state == ChallengeScreenState.None ? 0 : 0.99);
        }

        private static (int X, int Y)?
            LocateChangeGamemode(ImageFrame frame) =>
            (ChallengeScreenState)frame.Pixels[0] ==
                ChallengeScreenState.PostMatchPreview
                ? (401, 522)
                : null;

        private Task ClickChangeGamemodeAsync(
            (int X, int Y) action,
            int attempt,
            ChallengeScreenMatch match,
            CancellationToken cancellationToken)
        {
            Assert.Equal((401, 522), action);
            Assert.InRange(
                attempt,
                1,
                ExpeditionGameModeHandoffLoop
                    .MaximumChangeGamemodeAttempts);
            Assert.Equal(
                ChallengeScreenState.PostMatchPreview,
                match.State);
            cancellationToken.ThrowIfCancellationRequested();
            Clicks++;
            ClickCaptureCounts.Add(Captures);
            ClickAttempts.Add(attempt);
            if (CancelAfterClick)
            {
                _cancellation!.Cancel();
            }

            return Task.CompletedTask;
        }

        private Task<ImageFrame?> OpenPlayMenuAsync(
            ChallengeScreenMatch match,
            CancellationToken cancellationToken)
        {
            Assert.True(
                match.State is
                    ChallengeScreenState.Victory or
                    ChallengeScreenState.Defeat);
            cancellationToken.ThrowIfCancellationRequested();
            PlayMenuOpens++;
            return Task.FromResult(PlayMenuFrame);
        }

        private Task DelayAsync(
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Now += duration;
            return Task.CompletedTask;
        }
    }

    private static ImageFrame Frame(
        ChallengeScreenState state) =>
        new(
            1,
            1,
            PixelFormat.Gray8,
            [(byte)state]);
}
