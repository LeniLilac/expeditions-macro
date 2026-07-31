using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class StageBountyWaveOwnershipTests
{
    [Fact]
    public void DetectOwnedBountyWave_RequiresGameplayHud()
    {
        ImageFrame frame = ImageCodec.Load(
            FixturePath("WaveCounterLegacy.png"));
        ClearGameplayOwner(frame);

        Assert.Null(
            StageMacroRunner
                .DetectOwnedBountyWave(frame));
    }

    [Theory]
    [InlineData("WaveCounterLegacy.png", 67)]
    [InlineData("WaveCounterNoVoice.png", 2)]
    public void DetectOwnedBountyWave_AcceptsReviewedLayouts(
        string fixture,
        int expectedWave)
    {
        ImageFrame frame = ImageCodec.Load(
            FixturePath(fixture));

        Assert.Equal(
            expectedWave,
            StageMacroRunner
                .DetectOwnedBountyWave(frame));
    }

    private static void ClearGameplayOwner(
        ImageFrame frame)
    {
        Array.Clear(
            frame.Pixels,
            525 * frame.Width * 3,
            (frame.Height - 525) *
            frame.Width * 3);
        for (int y = 280; y < 451; y++)
        {
            Array.Clear(
                frame.Pixels,
                (y * frame.Width + 680) * 3,
                (frame.Width - 680) * 3);
        }
    }

    private static string FixturePath(
        string fixture) =>
        Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions",
            "bounties",
            fixture);
}
