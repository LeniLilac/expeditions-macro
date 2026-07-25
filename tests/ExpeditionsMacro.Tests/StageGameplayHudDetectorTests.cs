using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class StageGameplayHudDetectorTests
{
    [Fact]
    public void OrdinaryGameplay_HasStableStageHud()
    {
        StageGameplayHudMatch match = StageGameplayHudDetector.Detect(
            ImageCodec.Load(Path.Combine(
                TestPaths.ChallengeDatasets,
                "GameplayNegative",
                "GameplayNegative_09.png")));

        Assert.True(match.Visible);
        Assert.InRange(match.HotbarSupport, 0.50, 1);
        Assert.InRange(match.UnitManagerScore, 0.70, 1);
        Assert.InRange(match.StageInfoScore, 0.70, 1);
    }

    [Fact]
    public void RaidUnitDropPopup_HidesTheStageHud()
    {
        ImageFrame image = LoadStage("RaidDropPopup_01.png");
        StageGameplayHudMatch match =
            StageGameplayHudDetector.Detect(image);

        Assert.False(match.Visible);
        Assert.Equal(
            StageScreenState.None,
            StageScreenDetector.Detect(image).State);
        Assert.True(
            match.HotbarSupport < 0.50 ||
            match.UnitManagerScore < 0.70 ||
            match.StageInfoScore < 0.70);
    }

    [Theory]
    [InlineData("RaidVictory_CompactActions_01.png")]
    [InlineData("RaidDefeat_01.png")]
    public void TerminalPanels_AreNotGameplayHud(string fileName)
    {
        Assert.False(
            StageGameplayHudDetector.Detect(
                LoadStage(fileName)).Visible);
    }

    [Fact]
    public void Detector_RejectsUnexpectedClientDimensions()
    {
        ImageFrame image = new(
            800,
            600,
            PixelFormat.Rgb24,
            new byte[800 * 600 * 3],
            takeOwnership: true);

        Assert.Throws<InvalidDataException>(
            () => StageGameplayHudDetector.Detect(image));
    }

    private static ImageFrame LoadStage(string fileName) =>
        ImageCodec.Load(Path.Combine(TestPaths.StageDatasets, fileName));
}
