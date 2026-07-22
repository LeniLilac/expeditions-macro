using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Tests;

public sealed class TeamScreenDetectorTests
{
    [Theory]
    [InlineData("TeamUnits_01.png", TeamScreenState.Units)]
    [InlineData("TeamList_01.png", TeamScreenState.Teams)]
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
        Assert.Equal((580, 267), TeamScreenDetector.LoadTeamAction(1));
        Assert.Equal((580, 355), TeamScreenDetector.LoadTeamAction(2));
        Assert.Equal((580, 447), TeamScreenDetector.LoadTeamAction(3));
        Assert.Equal((345, 331), TeamScreenDetector.LoadConfirmAction);
        Assert.Equal((319, 376), TeamScreenDetector.IncludeEquipmentAction);
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
