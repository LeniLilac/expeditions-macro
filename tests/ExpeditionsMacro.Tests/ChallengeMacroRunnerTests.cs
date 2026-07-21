using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeMacroRunnerTests
{
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
