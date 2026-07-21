using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeMacroRunnerTests
{
    [Fact]
    public void PrestartPlayAction_IsAvailableForSafeCameraFailureExit()
    {
        string[] captures = Directory.GetFiles(
            TestPaths.ChallengeDatasets,
            "Prestart_*.png",
            SearchOption.AllDirectories);

        Assert.NotEmpty(captures);
        foreach (string path in captures)
        {
            ImageFrame frame = ImageCodec.Load(path);
            (int X, int Y)? play = ChallengeScreenDetector.PlayAction(frame);
            Assert.True(play is not null, $"Play was not located in {path}.");
            Assert.InRange(play.Value.X, 152, 180);
            Assert.InRange(play.Value.Y, 570, 598);
        }
    }

    [Fact]
    public async Task MapRecognition_ParksCursorBeforeDiscardingHighlightedSelectorFrame()
    {
        bool parked = false;
        int captures = 0;
        ImageFrame frame = new(1, 1, PixelFormat.Rgb24, new byte[3], takeOwnership: true);

        ChallengeMapId? map = await ChallengeMacroRunner.RecognizeMapAfterParkingAsync(
            _ =>
            {
                parked = true;
                return Task.CompletedTask;
            },
            () =>
            {
                Assert.True(parked);
                captures++;
                return frame;
            },
            _ => parked ? ChallengeMapId.FairyKingForest : null,
            retryMilliseconds: 0,
            maximumAttempts: 2,
            CancellationToken.None);

        Assert.Equal(ChallengeMapId.FairyKingForest, map);
        Assert.Equal(1, captures);
    }

    [Fact]
    public async Task PrestartAction_ReparksAndRecapturesWhenUnitHoverCoversButton()
    {
        int parks = 0;
        int captures = 0;
        ImageFrame frame = new(1, 1, PixelFormat.Rgb24, new byte[3], takeOwnership: true);

        (int X, int Y)? action = await ChallengeMacroRunner.LocateActionAfterParkingAsync(
            _ =>
            {
                parks++;
                return Task.CompletedTask;
            },
            () =>
            {
                captures++;
                return frame;
            },
            _ => captures >= 2 ? (404, 177) : null,
            retryMilliseconds: 0,
            maximumAttempts: 3,
            CancellationToken.None);

        Assert.NotNull(action);
        Assert.Equal((404, 177), action.Value);
        Assert.Equal(2, parks);
        Assert.Equal(2, captures);
    }

    [Fact]
    public async Task PostMatchPlay_FirstIgnoredClick_IsRedetectedAndRetried()
    {
        ImageFrame hud = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchHud",
            "PostMatchHud_01.png"));
        ImageFrame preview = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchPreview",
            "PostMatchPreview_03.png"));
        List<(int X, int Y)> clicks = [];
        int captures = 0;
        int waits = 0;

        ImageFrame result = await ChallengeMacroRunner.OpenPostMatchPreviewWithRetriesAsync(
            capture: () =>
            {
                captures++;
                return hud.Clone();
            },
            click: (x, y, _) =>
            {
                clicks.Add((x, y));
                return Task.CompletedTask;
            },
            waitForPreview: (_, _) => Task.FromResult<ImageFrame?>(++waits == 1 ? null : preview),
            attemptStarted: null,
            attemptMissed: null,
            CancellationToken.None);

        Assert.Same(preview, result);
        Assert.Equal(2, captures);
        Assert.Equal(2, waits);
        Assert.Equal(2, clicks.Count);
        Assert.All(clicks, click =>
        {
            Assert.InRange(click.X, 158, 170);
            Assert.InRange(click.Y, 570, 592);
        });
    }

    [Fact]
    public async Task PostMatchPlay_LateTransitionBeforeRetry_IsAcceptedWithoutAnotherClick()
    {
        ImageFrame hud = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchHud",
            "PostMatchHud_01.png"));
        ImageFrame preview = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchPreview",
            "PostMatchPreview_03.png"));
        Queue<ImageFrame> captures = new([hud, preview]);
        int clicks = 0;
        int waits = 0;

        ImageFrame result = await ChallengeMacroRunner.OpenPostMatchPreviewWithRetriesAsync(
            capture: () => captures.Dequeue().Clone(),
            click: (_, _, _) =>
            {
                clicks++;
                return Task.CompletedTask;
            },
            waitForPreview: (_, _) =>
            {
                waits++;
                return Task.FromResult<ImageFrame?>(null);
            },
            attemptStarted: null,
            attemptMissed: null,
            CancellationToken.None);

        Assert.Equal(ChallengeScreenState.PostMatchPreview, ChallengeScreenDetector.Detect(result).State);
        Assert.Equal(1, clicks);
        Assert.Equal(1, waits);
        Assert.Empty(captures);
    }
}
