using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Tests;

public sealed class QuickPlacementSelectionDetectorTests
{
    [Fact]
    public void HeldQuickPlacementWithASelectedUnit_ReportsVisible()
    {
        QuickPlacementSelectionMatch match =
            QuickPlacementSelectionDetector.Detect(
                LoadStage("QuickPlacementSelection_01.png"));

        Assert.True(match.Visible);
        Assert.InRange(match.Confidence, 0.80, 1);
        Assert.InRange(match.CyanPixels, 250, 430);
        Assert.True(match.LeftTextPixels >= 65);
        Assert.True(match.CenterTextPixels >= 105);
        Assert.True(match.RightTextPixels >= 62);
        Assert.True(match.IconPixels >= 5);
    }

    [Fact]
    public void HeldQuickPlacementWithoutASelectedUnit_HasNoIndicator()
    {
        QuickPlacementSelectionMatch match =
            QuickPlacementSelectionDetector.Detect(
                LoadStage(
                    "QuickPlacementSelectionNegative_01.png"));

        Assert.False(match.Visible);
        Assert.Equal(0, match.Confidence);
        Assert.Equal(0, match.CyanPixels);
    }

    [Fact]
    public void IndicatorWithoutItsIndependentIcon_IsRejected()
    {
        ImageFrame source =
            LoadStage("QuickPlacementSelection_01.png");
        byte[] pixels = source.Pixels.ToArray();
        Fill(
            pixels,
            source.Width,
            x: 399,
            y: 487,
            width: 10,
            height: 9,
            red: 0,
            green: 0,
            blue: 0);

        QuickPlacementSelectionMatch match =
            QuickPlacementSelectionDetector.Detect(
                new ImageFrame(
                    source.Width,
                    source.Height,
                    source.Format,
                    pixels,
                    takeOwnership: true));

        Assert.False(match.Visible);
        Assert.Equal(0, match.IconPixels);
    }

    [Fact]
    public void BroadCyanScenery_IsNotTextOwnership()
    {
        ImageFrame image = Blank();
        Fill(
            image.Pixels,
            image.Width,
            x: 350,
            y: 486,
            width: 109,
            height: 22,
            red: 0,
            green: 175,
            blue: 255);

        QuickPlacementSelectionMatch match =
            QuickPlacementSelectionDetector.Detect(image);

        Assert.False(match.Visible);
        Assert.True(match.CyanPixels > 430);
    }

    [Fact]
    public void EveryOtherReviewedDatasetFrame_IsNegative()
    {
        string datasetRoot = Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions");
        string positive = Path.GetFullPath(
            Path.Combine(
                TestPaths.StageDatasets,
                "QuickPlacementSelection_01.png"));
        string[] falseMatches = Directory
            .EnumerateFiles(
                datasetRoot,
                "*.png",
                SearchOption.AllDirectories)
            .Where(path =>
                !string.Equals(
                    Path.GetFullPath(path),
                    positive,
                    StringComparison.OrdinalIgnoreCase))
            .Where(path =>
                QuickPlacementSelectionDetector
                    .Detect(ImageCodec.Load(path))
                    .Visible)
            .Select(path =>
                Path.GetRelativePath(datasetRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(falseMatches);
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
            () => QuickPlacementSelectionDetector.Detect(image));
    }

    private static ImageFrame Blank() =>
        new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3],
            takeOwnership: true);

    private static void Fill(
        byte[] pixels,
        int imageWidth,
        int x,
        int y,
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                int offset = (row * imageWidth + column) * 3;
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
            }
        }
    }

    private static ImageFrame LoadStage(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                fileName));
}
