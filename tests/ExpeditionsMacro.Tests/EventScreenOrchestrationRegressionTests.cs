using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class EventScreenOrchestrationRegressionTests
{
    [Theory]
    [InlineData(
        "ActSelector_CurrentShifted.png",
        EventScreenState.ActSelector)]
    [InlineData(
        "Act1Detail.png",
        EventScreenState.ActDetail)]
    public void OwnedEventScreen_DoesNotRequireDecorativeHeader(
        string fileName,
        EventScreenState expected)
    {
        ImageFrame frame = Load(fileName).Clone();
        RemoveDecorativeHeader(frame);

        EventScreenMatch match =
            EventScreenDetector.Detect(frame);

        Assert.Equal(expected, match.State);
    }

    [Theory]
    [InlineData(
        "ActSelector_CurrentShifted.png",
        EventScreenState.ActSelector)]
    [InlineData(
        "Act1Detail.png",
        EventScreenState.ActDetail)]
    public void HeaderAbsent_StillRequiresSelectedVillainTab(
        string fileName,
        EventScreenState rejected)
    {
        ImageFrame frame = Load(fileName).Clone();
        RemoveDecorativeHeader(frame);
        FillRegion(
            frame,
            x: 17,
            y: 109,
            width: 11,
            height: 44);
        FillRegion(
            frame,
            x: 17,
            y: 160,
            width: 11,
            height: 44);

        EventScreenMatch match =
            EventScreenDetector.Detect(frame);

        Assert.NotEqual(rejected, match.State);
        Assert.Null(match.ActionX);
        Assert.Null(match.ActionY);
    }

    private static ImageFrame Load(string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.EventDatasets,
                name));

    private static void RemoveDecorativeHeader(
        ImageFrame frame) =>
        FillRegion(
            frame,
            x: 0,
            y: 55,
            width: 180,
            height: 54);

    private static void FillRegion(
        ImageFrame frame,
        int x,
        int y,
        int width,
        int height)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x;
                 column < x + width;
                 column++)
            {
                int pixel =
                    (row * frame.Width + column) * 3;
                frame.Pixels[pixel] = 12;
                frame.Pixels[pixel + 1] = 12;
                frame.Pixels[pixel + 2] = 12;
            }
        }
    }
}
