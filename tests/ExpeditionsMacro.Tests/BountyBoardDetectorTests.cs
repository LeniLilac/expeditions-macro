using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class BountyBoardDetectorTests
{
    [Theory]
    [InlineData("EventHome.png", 234)]
    [InlineData("ActSelector.png", 234)]
    [InlineData(
        "EventHome_BeginnerPathPresent_01.png",
        285)]
    [InlineData(
        "EventCatalog_BeginnerPathSelected_Current_01.png",
        285)]
    public void EventEntry_FindsTheUnhoveredLiveBountyRow(
        string fixture,
        int expectedY)
    {
        BountyBoardMatch match =
            BountyBoardDetector.Detect(
                Load(fixture));

        Assert.Equal(
            BountyBoardState.EventCatalog,
            match.State);
        Assert.Equal(
            (92, expectedY),
            match.EventAction);
        Assert.True(
            match.Confidence >= 0.78);
    }

    [Fact]
    public void EventEntry_RemainsOwnedWhenTheBountyRowIsHighlighted()
    {
        ImageFrame frame = Load(
            "EventHome.png");
        Fill(
            frame,
            x: 17,
            y: 210,
            width: 11,
            height: 44,
            red: 113,
            green: 67,
            blue: 41);

        BountyBoardMatch match =
            BountyBoardDetector.Detect(
                frame);

        Assert.Equal(
            BountyBoardState.EventCatalog,
            match.State);
        Assert.Equal(
            (92, 234),
            match.EventAction);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void EventEntry_DoesNotMatchOtherCapturedStates()
    {
        string root = Path.GetDirectoryName(
            TestPaths.EventDatasets)!;
        List<string> failures = [];
        foreach (string directory in
                 Directory.EnumerateDirectories(root)
                     .Where(directory =>
                         !string.Equals(
                             directory,
                             TestPaths.EventDatasets,
                             StringComparison
                                 .OrdinalIgnoreCase)))
        {
            foreach (string file in
                     Directory.EnumerateFiles(
                         directory,
                         "*.png",
                         SearchOption.AllDirectories))
            {
                ImageFrame frame =
                    ImageCodec.Load(file);
                if (frame.Width != 808 ||
                    frame.Height != 611)
                {
                    continue;
                }
                BountyBoardMatch match =
                    BountyBoardDetector.Detect(
                        frame);
                if (match.State ==
                    BountyBoardState.EventCatalog)
                {
                    failures.Add(
                        Path.GetRelativePath(
                            TestPaths.RepositoryRoot,
                            file));
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Bounty event-entry false matches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static ImageFrame Load(
        string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.EventDatasets,
                name));

    private static void Fill(
        ImageFrame frame,
        int x,
        int y,
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        for (int row = y;
             row < y + height;
             row++)
        {
            for (int column = x;
                 column < x + width;
                 column++)
            {
                int pixel =
                    (row * frame.Width + column) * 3;
                frame.Pixels[pixel] = red;
                frame.Pixels[pixel + 1] = green;
                frame.Pixels[pixel + 2] = blue;
            }
        }
    }
}
