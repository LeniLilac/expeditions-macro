using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class StageScreenDetectorTests
{
    [Fact]
    public void TeamLoadGuard_RequiresPrestartAndRejectsPlaySelector()
    {
        StageMacroRunner.RequirePrestartForTeamLoad(new StageScreenMatch(StageScreenState.Prestart, 0.98));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            StageMacroRunner.RequirePrestartForTeamLoad(new StageScreenMatch(StageScreenState.GameModeSelector, 0.99)));

        Assert.Contains("requires a confirmed prestart screen", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StorySelector_01.png", StageScreenState.StorySelector)]
    [InlineData("StoryDetail_01.png", StageScreenState.StoryDetail)]
    [InlineData("RaidSelector_01.png", StageScreenState.RaidSelector)]
    [InlineData("RaidDetail_01.png", StageScreenState.RaidDetail)]
    [InlineData("RaidPartyPreview_01.png", StageScreenState.PreviewReady)]
    [InlineData("StoryVictory_Act_01.png", StageScreenState.Victory)]
    [InlineData("StoryVictory_Mastery_01.png", StageScreenState.Victory)]
    [InlineData("StoryDefeat_Act_01.png", StageScreenState.Defeat)]
    [InlineData("StoryDefeat_Infinite_01.png", StageScreenState.Defeat)]
    [InlineData("StoryDefeat_Mastery_01.png", StageScreenState.Defeat)]
    [InlineData("RaidVictory_01.png", StageScreenState.Victory)]
    [InlineData("RaidDefeat_01.png", StageScreenState.Defeat)]
    [InlineData("GameModeNegative_01.png", StageScreenState.GameModeSelector)]
    public void ReviewedStageFixtures_MatchTheirExpectedState(string fileName, StageScreenState expected)
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.78, 1);
    }

    [Theory]
    [InlineData("StoryDetail_01.png", StageScreenState.StoryDetail)]
    [InlineData("RaidDetail_01.png", StageScreenState.RaidDetail)]
    public void DetailScreens_MapTheLiveSelectStageButton(string fileName, StageScreenState expected)
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.ActionX!.Value, 245, 270);
        Assert.InRange(match.ActionY!.Value, 440, 458);
    }

    [Fact]
    public void StoryModeTileAction_UsesTheMapCopyInsteadOfRewardIcons()
    {
        Assert.Equal((420, 105), StageScreenDetector.ModeTileAction(StageMode.Story));
    }

    [Fact]
    public void ThreeActionPartyPreview_MapsTheLiveStartButton()
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load("RaidPartyPreview_01.png"));

        Assert.Equal(StageScreenState.PreviewReady, match.State);
        Assert.InRange(match.ActionX!.Value, 475, 485);
        Assert.InRange(match.ActionY!.Value, 415, 425);
    }

    [Theory]
    [InlineData("StoryVictory_Act_01.png")]
    [InlineData("StoryVictory_Mastery_01.png")]
    [InlineData("RaidVictory_01.png")]
    public void VictoryScreens_MapTheLiveCloseControl(string fileName)
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load(fileName));

        Assert.Equal(StageScreenState.Victory, match.State);
        Assert.InRange(match.ActionX!.Value, 670, 690);
        Assert.InRange(match.ActionY!.Value, 135, 170);
    }

    [Theory]
    [InlineData("TeamUnits_01.png")]
    [InlineData("TeamList_01.png")]
    [InlineData("TeamLoadConfirm_01.png")]
    [InlineData("TeamEquipmentConfirm_01.png")]
    public void TeamInterfaces_DoNotStealStageNavigation(string fileName)
    {
        Assert.Equal(StageScreenState.None, StageScreenDetector.Detect(Load(fileName)).State);
    }

    [Fact]
    public void Detector_RejectsUnexpectedClientDimensions()
    {
        ImageFrame image = new(800, 600, PixelFormat.Rgb24, new byte[800 * 600 * 3], takeOwnership: true);

        Assert.Throws<InvalidDataException>(() => StageScreenDetector.Detect(image));
    }

    private static ImageFrame Load(string name) => ImageCodec.Load(Path.Combine(TestPaths.StageDatasets, name));
}
