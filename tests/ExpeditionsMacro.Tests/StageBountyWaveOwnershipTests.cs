using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class StageBountyWaveOwnershipTests
{
    [Theory]
    [InlineData("WaveCounterLegacy.png")]
    [InlineData("WaveCounterNoVoice.png")]
    [InlineData("WaveCounterType3.png")]
    public void DetectOwnedBountyWave_RequiresGameplayHud(
        string fixture)
    {
        ImageFrame frame = ImageCodec.Load(
            FixturePath(fixture));
        ClearGameplayOwner(frame);

        Assert.Null(
            StageMacroRunner
                .DetectOwnedBountyWave(frame));
    }

    [Theory]
    [InlineData("WaveCounterLegacy.png", 67)]
    [InlineData("WaveCounterNoVoice.png", 2)]
    [InlineData("WaveCounterType3.png", 37)]
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
