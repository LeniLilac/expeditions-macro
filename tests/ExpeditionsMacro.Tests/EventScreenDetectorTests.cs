using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class EventScreenDetectorTests
{
    [Theory]
    [InlineData("EventHome.png", EventScreenState.EventHome)]
    [InlineData("ActSelector.png", EventScreenState.ActSelector)]
    [InlineData("Act4Selector.png", EventScreenState.ActSelector)]
    [InlineData("Act1Detail.png", EventScreenState.ActDetail)]
    [InlineData("Act4Detail.png", EventScreenState.ActDetail)]
    [InlineData("Prestart.png", EventScreenState.Prestart)]
    [InlineData("Victory.png", EventScreenState.Victory)]
    [InlineData("VictoryNextStage.png", EventScreenState.Victory)]
    [InlineData("Act4Victory.png", EventScreenState.Victory)]
    [InlineData("Defeat.png", EventScreenState.Defeat)]
    public void ReviewedEventScreens_ReportTheirOwnedState(
        string fileName,
        EventScreenState expected)
    {
        EventScreenMatch match =
            EventScreenDetector.Detect(Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.7, 1);
    }

    [Theory]
    [InlineData("LobbyClosed.png")]
    public void UnrelatedScreens_DoNotBecomeEventNavigation(
        string fileName)
    {
        ImageFrame frame = ImageCodec.Load(
            Path.Combine(
                TestPaths.SettingsDatasets,
                fileName));

        Assert.Equal(
            EventScreenState.None,
            EventScreenDetector.Detect(frame).State);
    }

    [Fact]
    public void EventActions_UseReviewedClientCoordinates()
    {
        Assert.Equal((50, 410),
            EventScreenDetector.LobbyEventAction);
        Assert.Equal((499, 571),
            EventScreenDetector.EventGameModeAction);
        Assert.Equal((238, 437),
            EventScreenDetector.SelectStageAction);
        Assert.False(
            EventScreenDetector.RequiresLaterActScroll(
                EventAct.Act1));
        Assert.False(
            EventScreenDetector.RequiresLaterActScroll(
                EventAct.Act2));
        Assert.True(
            EventScreenDetector.RequiresLaterActScroll(
                EventAct.Act3));
        Assert.True(
            EventScreenDetector.RequiresLaterActScroll(
                EventAct.Act4));
        Assert.Equal(
            (402, 560, 628, 560),
            EventScreenDetector.LaterActScroll);
    }

    [Theory]
    [InlineData(
        "ActSelector.png",
        EventAct.Act1,
        300,
        335,
        350,
        390)]
    [InlineData(
        "ActSelector.png",
        EventAct.Act2,
        540,
        580,
        250,
        300)]
    [InlineData(
        "Act4Selector.png",
        EventAct.Act3,
        315,
        355,
        400,
        450)]
    [InlineData(
        "Act4Selector.png",
        EventAct.Act4,
        565,
        610,
        295,
        345)]
    public void ActEmblems_MapTheirLiveCards(
        string fileName,
        EventAct act,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY)
    {
        ImageFrame frame = Load(fileName);

        (int X, int Y)? action =
            EventScreenDetector.ActAction(
                frame,
                act);

        Assert.NotNull(action);
        Assert.InRange(
            action.Value.X,
            minimumX,
            maximumX);
        Assert.InRange(
            action.Value.Y,
            minimumY,
            maximumY);
    }

    [Theory]
    [InlineData("ActSelector.png", EventAct.Act3)]
    [InlineData("ActSelector.png", EventAct.Act4)]
    [InlineData("Act4Selector.png", EventAct.Act1)]
    [InlineData("Act4Selector.png", EventAct.Act2)]
    public void ActEmblems_RejectCardsOutsideTheVisibleCarousel(
        string fileName,
        EventAct act)
    {
        Assert.Null(
            EventScreenDetector.ActAction(
                Load(fileName),
                act));
    }

    [Fact]
    public void ActFourDetail_LocatesSelectStageAction()
    {
        EventScreenMatch match =
            EventScreenDetector.Detect(
                Load("Act4Detail.png"));

        Assert.Equal(
            EventScreenState.ActDetail,
            match.State);
        Assert.InRange(match.ActionX ?? -1, 180, 305);
        Assert.InRange(match.ActionY ?? -1, 420, 455);
    }

    [Fact]
    public void VictoryWithNextStage_StillMapsRepeatStage()
    {
        ImageFrame frame = Load("VictoryNextStage.png");

        Assert.Equal(
            EventScreenState.Victory,
            EventScreenDetector.Detect(frame).State);
        (int X, int Y)? repeat =
            StageScreenDetector.RepeatStageAction(
                frame,
                StageScreenState.Victory);
        Assert.NotNull(repeat);
        Assert.True(
            repeat!.Value.X is >= 285 and <= 325 &&
            repeat.Value.Y is >= 420 and <= 455,
            $"Repeat Stage mapped to ({repeat.Value.X}, {repeat.Value.Y}).");
    }

    [Fact]
    public void ActFourVictory_MapsFinalRepeatStageAction()
    {
        ImageFrame frame = Load("Act4Victory.png");

        Assert.Equal(
            EventScreenState.Victory,
            EventScreenDetector.Detect(frame).State);
        (int X, int Y)? repeat =
            StageScreenDetector.RepeatStageAction(
                frame,
                StageScreenState.Victory);
        Assert.NotNull(repeat);
        Assert.True(
            repeat!.Value.X is >= 148 and <= 302 &&
            repeat.Value.Y is >= 421 and <= 453,
            $"Repeat Stage mapped to ({repeat.Value.X}, {repeat.Value.Y}).");
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void EventNavigation_DoesNotStealOtherCapturedScreens()
    {
        string[] roots =
        [
            TestPaths.Datasets,
            TestPaths.ChallengeDatasets,
            TestPaths.StageDatasets,
            TestPaths.NavigationVariantDatasets,
            TestPaths.RefuelDatasets,
            TestPaths.SettingsDatasets,
        ];
        List<string> failures = [];
        foreach (string file in roots
                     .Where(Directory.Exists)
                     .SelectMany(root =>
                         Directory.EnumerateFiles(
                             root,
                             "*.png",
                             SearchOption.AllDirectories)))
        {
            ImageFrame frame = ImageCodec.Load(file);
            if (frame.Width != 808 ||
                frame.Height != 611)
            {
                continue;
            }
            EventScreenMatch match =
                EventScreenDetector.Detect(frame);
            if (match.State is
                EventScreenState.EventHome or
                EventScreenState.ActSelector or
                EventScreenState.ActDetail)
            {
                failures.Add(
                    $"{match.State} {match.Confidence:P1} {Path.GetRelativePath(TestPaths.RepositoryRoot, file)}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Event navigation false matches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static ImageFrame Load(string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.EventDatasets,
                name));
}
