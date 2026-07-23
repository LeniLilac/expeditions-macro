using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Tests;

public sealed class TeamScreenDetectorTests
{
    [Theory]
    [InlineData("TeamUnits_01.png", TeamScreenState.Units)]
    [InlineData("TeamList_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Team1_Current_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Team2_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Team3_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Team4_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Team5_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Team6_01.png", TeamScreenState.Teams)]
    [InlineData("TeamList_Aligned_Bottom_01.png", TeamScreenState.Teams)]
    [InlineData("TeamLoadConfirm_01.png", TeamScreenState.LoadConfirm)]
    [InlineData("TeamEquipmentConfirm_01.png", TeamScreenState.EquipmentConfirm)]
    [InlineData("TeamEquipmentConfirm_Compact_01.png", TeamScreenState.EquipmentConfirm)]
    public void ReviewedTeamFixtures_MatchTheirExpectedState(string fileName, TeamScreenState expected)
    {
        TeamScreenMatch match = TeamScreenDetector.Detect(Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.70, 1);
    }

    [Theory]
    [InlineData("GameModeNegative_01.png")]
    [InlineData("StorySelector_01.png")]
    [InlineData("StoryDetail_01.png")]
    [InlineData("StoryDetail_Mastery_01.png")]
    [InlineData("StoryDetail_Act_Wide_01.png")]
    [InlineData("StoryDetail_Infinite_Wide_01.png")]
    [InlineData("StoryDetail_Mastery_Wide_01.png")]
    [InlineData("RaidSelector_01.png")]
    [InlineData("RaidDetail_01.png")]
    [InlineData("StoryVictory_Act_01.png")]
    [InlineData("StoryDefeat_Act_01.png")]
    [InlineData("RaidVictory_01.png")]
    [InlineData("RaidDefeat_01.png")]
    public void OtherStageScreens_DoNotMatchTheTeamInterface(string fileName)
    {
        Assert.Equal(TeamScreenState.None, TeamScreenDetector.Detect(Load(fileName)).State);
    }

    [Fact]
    public void TeamActions_UseTheReviewedClientRelativeControls()
    {
        Assert.Equal((305, 427), TeamScreenDetector.TeamsTabAction);
        Assert.Equal([0, 30, 59, 89, 118, 148, 156, 156],
            Enumerable.Range(1, 8).Select(TeamScreenDetector.ScrollThumbOffsetY));
        Assert.Equal((345, 331), TeamScreenDetector.LoadConfirmAction);
        Assert.Equal((319, 376), TeamScreenDetector.IncludeEquipmentAction);
    }

    [Theory]
    [InlineData("TeamList_01.png", 1, 645, 655, 236, 590, 605, 260, 270)]
    [InlineData("TeamList_Aligned_Team1_Current_01.png", 1, 624, 632, 240, 575, 585, 262, 272)]
    [InlineData("TeamList_Aligned_Team2_01.png", 2, 624, 632, 270, 575, 585, 262, 272)]
    [InlineData("TeamList_Aligned_Team3_01.png", 3, 624, 632, 299, 575, 585, 262, 272)]
    [InlineData("TeamList_Aligned_Team4_01.png", 4, 624, 632, 329, 575, 585, 262, 272)]
    [InlineData("TeamList_Aligned_Team5_01.png", 5, 624, 632, 358, 575, 585, 262, 272)]
    [InlineData("TeamList_Aligned_Team6_01.png", 6, 624, 632, 388, 575, 585, 260, 270)]
    public void AlignedTeamRows_MapTheirFullyVisibleLoadButton(
        string fileName,
        int teamSlot,
        int minimumThumbX,
        int maximumThumbX,
        int expectedThumbY,
        int minimumActionX,
        int maximumActionX,
        int minimumActionY,
        int maximumActionY)
    {
        ImageFrame image = Load(fileName);

        TeamScrollbarThumb thumb = TeamScreenDetector.FindScrollbarThumb(image)!.Value;
        (int X, int Y) action =
            TeamScreenDetector.AlignedLoadTeamAction(image, teamSlot, expectedThumbY)!.Value;

        Assert.InRange(thumb.X, minimumThumbX, maximumThumbX);
        Assert.InRange(thumb.CenterY, expectedThumbY - 1, expectedThumbY + 1);
        Assert.InRange(thumb.Height, 70, 90);
        Assert.InRange(action.X, minimumActionX, maximumActionX);
        Assert.InRange(action.Y, minimumActionY, maximumActionY);
    }

    [Theory]
    [InlineData(7, 320, 335)]
    [InlineData(8, 408, 420)]
    public void BottomAlignedRows_MapTeamsSevenAndEight(int teamSlot, int minimumY, int maximumY)
    {
        ImageFrame image = Load("TeamList_Aligned_Bottom_01.png");

        TeamScrollbarThumb thumb = TeamScreenDetector.FindScrollbarThumb(image)!.Value;
        (int X, int Y) action =
            TeamScreenDetector.AlignedLoadTeamAction(image, teamSlot, targetThumbCenterY: 396)!.Value;

        Assert.InRange(thumb.CenterY, 395, 397);
        Assert.InRange(action.X, 575, 585);
        Assert.InRange(action.Y, minimumY, maximumY);
    }

    [Fact]
    public void TopPosition_DoesNotExposeTheClippedThirdTeamButton()
    {
        ImageFrame image = Load("TeamList_Aligned_Team1_Current_01.png");

        Assert.Null(TeamScreenDetector.AlignedLoadTeamAction(
            image,
            teamSlot: 3,
            targetThumbCenterY: 299));
    }

    [Fact]
    public void ReopenedListTop_IsDistinctFromAScrolledPosition()
    {
        TeamScrollbarThumb top =
            TeamScreenDetector.FindScrollbarThumb(Load("TeamList_Aligned_Team1_Current_01.png"))!.Value;
        TeamScrollbarThumb scrolled =
            TeamScreenDetector.FindScrollbarThumb(Load("TeamList_Aligned_Team2_01.png"))!.Value;

        Assert.True(TeamScreenDetector.IsScrollbarAtTop(top));
        Assert.False(TeamScreenDetector.IsScrollbarAtTop(scrolled));
    }

    [Theory]
    [InlineData("TeamUnits_01.png")]
    [InlineData("GameModeNegative_01.png")]
    public void NonTeamLists_DoNotExposeAScrollbarThumb(string fileName)
    {
        Assert.Null(TeamScreenDetector.FindScrollbarThumb(Load(fileName)));
    }

    [Theory]
    [InlineData("TeamEquipmentConfirm_01.png", 310, 330, 365, 385)]
    [InlineData("TeamEquipmentConfirm_Compact_01.png", 315, 335, 335, 350)]
    public void EquipmentConfirmation_MapsTheLiveIncludeButton(
        string fileName,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY)
    {
        TeamScreenMatch match = TeamScreenDetector.Detect(Load(fileName));

        Assert.Equal(TeamScreenState.EquipmentConfirm, match.State);
        Assert.InRange(match.ActionX!.Value, minimumX, maximumX);
        Assert.InRange(match.ActionY!.Value, minimumY, maximumY);
    }

    [Fact]
    public void Detector_RejectsUnexpectedClientDimensions()
    {
        ImageFrame image = new(800, 600, PixelFormat.Rgb24, new byte[800 * 600 * 3], takeOwnership: true);

        Assert.Throws<InvalidDataException>(() => TeamScreenDetector.Detect(image));
    }

    private static ImageFrame Load(string name) => ImageCodec.Load(Path.Combine(TestPaths.StageDatasets, name));
}
