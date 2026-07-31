using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Bounties;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class BountyBoardDetectorTests
{
    [Theory]
    [InlineData(
        "BountyBoard_DimmedSlot1_01.png",
        "",
        false)]
    [InlineData(
        "BountyBoard_DimmedSlot2_01.png",
        "1,4",
        false)]
    [InlineData(
        "BountyBoard_DimmedSlot3_01.png",
        "1,2,4",
        false)]
    [InlineData(
        "BountyBoard_DimmedSlot4_01.png",
        "1",
        false)]
    [InlineData(
        "BountyBoard_FourLiveOneDimmed_01.png",
        "1,2,3,4",
        true)]
    public void ReducedDailyLimit_ExposesOnlyLiveCardActions(
        string fileName,
        string expectedSlotList,
        bool rightView)
    {
        BountyBoardMatch match =
            BountyBoardDetector.Detect(
                ImageCodec.Load(
                    Path.Combine(
                        TestPaths.BountyDatasets,
                        fileName)));

        Assert.Equal(
            BountyBoardState.Board,
            match.State);
        IReadOnlyList<BountyLiveSlot> slots =
            BountyBoardLayout.LiveSlots(
                match,
                rightView);
        int[] expectedSlots = string.IsNullOrEmpty(
                expectedSlotList)
            ? []
            : expectedSlotList
                .Split(',')
                .Select(int.Parse)
                .ToArray();
        Assert.Equal(
            expectedSlots,
            slots.Select(value =>
                value.Slot));
        Assert.All(
            slots,
            value => Assert.Equal(
                BountyCardActionKind.Reroll,
                value.Action.Kind));
        for (int slot = 1;
             slot <= 5;
             slot++)
        {
            Assert.Equal(
                expectedSlots.Contains(slot),
                BountyBoardLayout.FindAction(
                    match,
                    slot,
                    rightView,
                    BountyCardActionKind.Reroll)
                is not null);
        }
    }

    [Fact]
    public void BoardOwnership_UsesOnlyAnnotatedHeaderAndButtonRail()
    {
        ImageFrame source = ImageCodec.Load(
            Path.Combine(
                TestPaths.BountyDatasets,
                "BountyBoard_FieldOwnerVariant_01.png"));

        BountyBoardMatch match =
            BountyBoardDetector.Detect(source);

        Assert.Equal(
            BountyBoardState.Board,
            match.State);
        Assert.True(match.BoardButtonRailScore > 0);
        Assert.True(match.BoardHeaderScore > 0);
        Assert.Empty(match.Actions);
    }

    [Fact]
    public void BoardOwnership_UsesBoundedTextFallbackForRecoloredHeader()
    {
        ImageFrame frame = ImageCodec.Load(
            Path.Combine(
                TestPaths.BountyDatasets,
                "BountyBoard_DimmedSlot1_01.png"));
        RecolorBrightHeaderInk(frame);

        BountyBoardMatch match =
            BountyBoardDetector.Detect(frame);

        Assert.Equal(
            BountyBoardState.Board,
            match.State);
        Assert.True(
            match.BoardHeaderUsedTextFallback);
        Assert.True(match.BoardHeaderScore > 0);
    }

    [Theory]
    [InlineData(210, 41, 120, 26)]
    [InlineData(8, 569, 175, 39)]
    public void BoardOwnership_RequiresHeaderAndButtonRail(
        int x,
        int y,
        int width,
        int height)
    {
        ImageFrame frame = ImageCodec.Load(
            Path.Combine(
                TestPaths.BountyDatasets,
                "BountyBoard_DimmedSlot1_01.png"));
        Fill(
            frame,
            x,
            y,
            width,
            height,
            red: 0,
            green: 0,
            blue: 0);

        BountyBoardMatch match =
            BountyBoardDetector.Detect(frame);

        Assert.NotEqual(
            BountyBoardState.Board,
            match.State);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void BoardOwnership_DoesNotMatchOtherRepositoryFixtures()
    {
        List<string> failures = [];
        foreach (string file in
                 Directory.EnumerateFiles(
                     Path.Combine(
                         TestPaths.RepositoryRoot,
                         "datasets"),
                     "*.png",
                     SearchOption.AllDirectories))
        {
            if (file.StartsWith(
                    TestPaths.BountyDatasets,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            ImageFrame frame = ImageCodec.Load(file);
            if (frame.Width != 808 ||
                frame.Height != 611)
            {
                continue;
            }
            if (BountyBoardDetector.Detect(frame).State ==
                BountyBoardState.Board)
            {
                failures.Add(
                    Path.GetRelativePath(
                        TestPaths.RepositoryRoot,
                        file));
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Bounty Board false matches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

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

    private static void RecolorBrightHeaderInk(
        ImageFrame frame)
    {
        for (int y = 41; y < 67; y++)
        {
            for (int x = 210; x < 330; x++)
            {
                int pixel =
                    (y * frame.Width + x) * 3;
                byte red = frame.Pixels[pixel];
                byte green = frame.Pixels[pixel + 1];
                byte blue = frame.Pixels[pixel + 2];
                int maximum = Math.Max(
                    red,
                    Math.Max(green, blue));
                int minimum = Math.Min(
                    red,
                    Math.Min(green, blue));
                if (maximum <= 210 ||
                    maximum - minimum <= 40)
                {
                    continue;
                }
                frame.Pixels[pixel] = 80;
                frame.Pixels[pixel + 1] = 170;
                frame.Pixels[pixel + 2] = 255;
            }
        }
    }
}
