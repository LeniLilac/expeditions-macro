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
    [InlineData("Act1Detail.png", EventScreenState.ActDetail)]
    [InlineData("Prestart.png", EventScreenState.Prestart)]
    [InlineData("Victory.png", EventScreenState.Victory)]
    [InlineData("VictoryNextStage.png", EventScreenState.Victory)]
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
        Assert.Equal((270, 410),
            EventScreenDetector.ActAction(EventAct.Act1));
        Assert.Equal((465, 280),
            EventScreenDetector.ActAction(EventAct.Act2));
        Assert.Equal((700, 420),
            EventScreenDetector.ActAction(EventAct.Act3));
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
