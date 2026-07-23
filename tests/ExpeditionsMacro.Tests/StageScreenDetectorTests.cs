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
        StageNavigationPolicy.RequirePrestartForTeamLoad(new StageScreenMatch(StageScreenState.Prestart, 0.98));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            StageNavigationPolicy.RequirePrestartForTeamLoad(new StageScreenMatch(StageScreenState.GameModeSelector, 0.99)));

        Assert.Contains("requires a confirmed prestart screen", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StorySelector_01.png", StageScreenState.StorySelector)]
    [InlineData("StoryDetail_01.png", StageScreenState.StoryDetail)]
    [InlineData("StoryDetail_Mastery_01.png", StageScreenState.StoryDetail)]
    [InlineData("StoryDetail_Act_Wide_01.png", StageScreenState.StoryDetail)]
    [InlineData("StoryDetail_Infinite_Wide_01.png", StageScreenState.StoryDetail)]
    [InlineData("StoryDetail_Mastery_Wide_01.png", StageScreenState.StoryDetail)]
    [InlineData("RaidSelector_01.png", StageScreenState.RaidSelector)]
    [InlineData("RaidDetail_01.png", StageScreenState.RaidDetail)]
    [InlineData("RaidPartyPreview_01.png", StageScreenState.PreviewReady)]
    [InlineData("StoryPartyPreview_Mastery_01.png", StageScreenState.PreviewReady)]
    [InlineData("StoryPostMatchParty_Mastery_01.png", StageScreenState.PostMatchPreview)]
    [InlineData("RaidPostMatchParty_01.png", StageScreenState.PostMatchPreview)]
    [InlineData("StoryVictory_Act_01.png", StageScreenState.Victory)]
    [InlineData("StoryVictory_Mastery_01.png", StageScreenState.Victory)]
    [InlineData("StoryVictory_Mastery_Current_02.png", StageScreenState.Victory)]
    [InlineData("StoryDefeat_Act_01.png", StageScreenState.Defeat)]
    [InlineData("StoryDefeat_Infinite_01.png", StageScreenState.Defeat)]
    [InlineData("StoryDefeat_Mastery_01.png", StageScreenState.Defeat)]
    [InlineData("RaidVictory_01.png", StageScreenState.Victory)]
    [InlineData("RaidVictory_CompactActions_01.png", StageScreenState.Victory)]
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
    [InlineData("StoryDetail_Mastery_01.png", StageScreenState.StoryDetail)]
    [InlineData("RaidDetail_01.png", StageScreenState.RaidDetail)]
    public void DetailScreens_MapTheLiveSelectStageButton(string fileName, StageScreenState expected)
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.ActionX!.Value, 245, 270);
        Assert.InRange(match.ActionY!.Value, 430, 458);
    }

    [Theory]
    [InlineData("StoryDetail_Act_Wide_01.png")]
    [InlineData("StoryDetail_Infinite_Wide_01.png")]
    [InlineData("StoryDetail_Mastery_Wide_01.png")]
    public void WideStoryDetailScreens_MapTheLiveSelectStageButton(string fileName)
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load(fileName));

        Assert.Equal(StageScreenState.StoryDetail, match.State);
        Assert.InRange(match.ActionX!.Value, 340, 380);
        Assert.InRange(match.ActionY!.Value, 410, 455);
    }

    [Fact]
    public void StoryModeTileAction_UsesTheMapCopyInsteadOfRewardIcons()
    {
        Assert.Equal((420, 105), StageScreenDetector.ModeTileAction(StageMode.Story));
    }

    [Theory]
    [InlineData("RaidPartyPreview_01.png", StageScreenState.PreviewReady, 415, 425)]
    [InlineData("StoryPartyPreview_Mastery_01.png", StageScreenState.PreviewReady, 388, 400)]
    [InlineData("StoryPostMatchParty_Mastery_01.png", StageScreenState.PostMatchPreview, 368, 382)]
    public void BothPartyPreviewFamilies_MapTheLiveStartButton(
        string fileName,
        StageScreenState expected,
        int minimumY,
        int maximumY)
    {
        ImageFrame image = Load(fileName);
        StageScreenMatch match = StageScreenDetector.Detect(image);
        (int X, int Y)? action = StageScreenDetector.PreviewStartAction(image);

        Assert.Equal(expected, match.State);
        Assert.NotNull(action);
        Assert.InRange(action!.Value.X, 475, 485);
        Assert.InRange(action.Value.Y, minimumY, maximumY);
    }

    [Fact]
    public void DisabledPostMatchStart_IsNotLaunchReady()
    {
        ImageFrame image = Load("RaidPostMatchParty_01.png");
        StageScreenMatch match = StageScreenDetector.Detect(image);

        Assert.Equal(StageScreenState.PostMatchPreview, match.State);
        Assert.Null(StageScreenDetector.PreviewStartAction(image));
        Assert.False(StageNavigationPolicy.MatchesExpectedState(
            StageScreenState.PreviewReady,
            match.State,
            hasPreviewStartAction: false));
    }

    [Theory]
    [InlineData("StoryPostMatchParty_Mastery_01.png", 368, 382)]
    [InlineData("RaidPostMatchParty_01.png", 388, 404)]
    public void PostMatchParty_MapsItsOwnChangeGamemodeAction(string fileName, int minimumY, int maximumY)
    {
        ImageFrame image = Load(fileName);
        StageScreenMatch match = StageScreenDetector.Detect(image);
        (int X, int Y)? action = StageScreenDetector.PostMatchChangeModeAction(image);

        Assert.Equal(StageScreenState.PostMatchPreview, match.State);
        Assert.NotNull(action);
        Assert.Equal(action!.Value.X, match.ActionX);
        Assert.Equal(action.Value.Y, match.ActionY);
        Assert.InRange(action.Value.X, 690, 705);
        Assert.InRange(action.Value.Y, minimumY, maximumY);
    }

    [Fact]
    public void PostMatchHud_RemainsDistinctFromThePartyPreview()
    {
        ImageFrame image = ImageCodec.Load(Path.Combine(
            TestPaths.ChallengeDatasets,
            "PostMatchHud",
            "PostMatchHud_01.png"));

        StageScreenMatch match = StageScreenDetector.Detect(image);

        Assert.Equal(StageScreenState.PostMatchHud, match.State);
    }

    [Fact]
    public void SelectorBackAction_UsesTheStableBottomLeftControl()
    {
        Assert.Equal((62, 588), StageScreenDetector.SelectorBackAction);
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
    [InlineData("RaidVictory_CompactActions_01.png")]
    [InlineData("StoryVictory_Mastery_Current_02.png")]
    public void CompactStageVictory_MapsTheLiveCloseControl(string fileName)
    {
        StageScreenMatch match = StageScreenDetector.Detect(Load(fileName));

        Assert.Equal(StageScreenState.Victory, match.State);
        Assert.InRange(match.ActionX!.Value, 650, 665);
        Assert.InRange(match.ActionY!.Value, 155, 170);
    }

    [Theory]
    [InlineData("StoryVictory_Mastery_Current_02.png", StageScreenState.Victory, 210, 240, 420, 452)]
    [InlineData("RaidVictory_CompactActions_01.png", StageScreenState.Victory, 290, 320, 420, 452)]
    [InlineData("StoryDefeat_Mastery_01.png", StageScreenState.Defeat, 190, 225, 420, 455)]
    [InlineData("RaidDefeat_01.png", StageScreenState.Defeat, 190, 225, 420, 455)]
    public void TerminalScreens_MapTheLiveRepeatStageControl(
        string fileName,
        StageScreenState state,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY)
    {
        (int X, int Y)? action = StageScreenDetector.RepeatStageAction(Load(fileName), state);

        Assert.NotNull(action);
        Assert.InRange(action!.Value.X, minimumX, maximumX);
        Assert.InRange(action.Value.Y, minimumY, maximumY);
    }

    [Theory]
    [InlineData("TeamUnits_01.png")]
    [InlineData("TeamList_01.png")]
    [InlineData("TeamList_Aligned_Team1_Current_01.png")]
    [InlineData("TeamList_Aligned_Team2_01.png")]
    [InlineData("TeamList_Aligned_Team3_01.png")]
    [InlineData("TeamList_Aligned_Team4_01.png")]
    [InlineData("TeamList_Aligned_Team5_01.png")]
    [InlineData("TeamList_Aligned_Team6_01.png")]
    [InlineData("TeamList_Aligned_Bottom_01.png")]
    [InlineData("TeamLoadConfirm_01.png")]
    [InlineData("TeamEquipmentConfirm_01.png")]
    [InlineData("TeamEquipmentConfirm_Compact_01.png")]
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
