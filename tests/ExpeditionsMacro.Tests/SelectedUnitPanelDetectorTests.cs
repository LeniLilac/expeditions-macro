using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Tests;

public sealed class SelectedUnitPanelDetectorTests
{
    [Theory]
    [MemberData(nameof(SelectedPanelScreens))]
    public void SelectedUnitPanel_RequiresTheCloseAndInitialPriorityControls(
        string path)
    {
        SelectedUnitPanelMatch match =
            SelectedUnitPanelDetector.Detect(
                ImageCodec.Load(path));

        Assert.True(match.Visible);
        Assert.InRange(match.Confidence, 0.70, 1);
        Assert.InRange(match.CloseScore, 0.18, 1);
        Assert.InRange(match.FirstPriorityScore, 0.32, 1);
        Assert.InRange(match.PanelScore, 0.52, 1);
    }

    [Fact]
    public void OrdinaryUnitHoverPanel_IsNotPlacementProof()
    {
        SelectedUnitPanelMatch match =
            SelectedUnitPanelDetector.Detect(
                LoadStage("SelectedUnitPanelHoverNegative_01.png"));

        Assert.False(match.Visible);
        Assert.True(
            match.CloseScore < 0.18 ||
            match.FirstPriorityScore < 0.32);
    }

    [Fact]
    public void PanelBody_IsSupportingEvidenceRatherThanARequiredAnchor()
    {
        ImageFrame selected =
            LoadStage("SelectedUnitPanel_01.png");
        byte[] pixels = selected.Pixels.ToArray();

        Fill(
            pixels,
            selected.Width,
            x: 9,
            y: 212,
            width: 265,
            height: 182,
            red: 160,
            green: 160,
            blue: 160);
        CopyRegion(
            selected,
            pixels,
            x: 244,
            y: 209,
            width: 29,
            height: 30);
        CopyRegion(
            selected,
            pixels,
            x: 29,
            y: 342,
            width: 52,
            height: 32);

        SelectedUnitPanelMatch match =
            SelectedUnitPanelDetector.Detect(
                new ImageFrame(
                    selected.Width,
                    selected.Height,
                    selected.Format,
                    pixels,
                    takeOwnership: true));

        Assert.True(match.Visible);
        Assert.True(match.PanelScore < 0.52);
    }

    [Fact]
    public void EveryOtherReviewedStageScreen_IsNotPlacementProof()
    {
        string[] falseMatches = Directory
            .EnumerateFiles(TestPaths.StageDatasets, "*.png")
            .Where(path =>
                !string.Equals(
                    Path.GetFileName(path),
                    "SelectedUnitPanel_01.png",
                    StringComparison.OrdinalIgnoreCase))
            .Where(path =>
                SelectedUnitPanelDetector.Detect(
                    ImageCodec.Load(path)).Visible)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        Assert.Empty(falseMatches);
    }

    [Theory]
    [MemberData(nameof(CrossStateScreens))]
    public void OtherModeAndShellStates_AreNotPlacementProof(
        string path)
    {
        Assert.False(
            SelectedUnitPanelDetector.Detect(
                ImageCodec.Load(path)).Visible);
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
            () => SelectedUnitPanelDetector.Detect(image));
    }

    public static TheoryData<string> CrossStateScreens =>
        new()
        {
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "GameplayNegative",
                "GameplayNegative_09.png"),
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "Victory",
                "Victory_01.png"),
            Path.Combine(
                TestPaths.Datasets,
                "Lobby_UI",
                "Lobby_UI_001.png"),
            Path.Combine(
                TestPaths.Datasets,
                "Expedition_Reward_Select",
                "Expedition_Reward_Select_001.png"),
            Path.Combine(
                TestPaths.Datasets,
                "Expedition_Victory_UI",
                "Expedition_Victory_UI_001.png"),
            Path.Combine(
                TestPaths.SettingsDatasets,
                "GraphicsPageCurrent.png"),
            Path.Combine(
                TestPaths.EventDatasets,
                "VictoryNextStage.png"),
        };

    public static TheoryData<string> SelectedPanelScreens =>
        new()
        {
            Path.Combine(
                TestPaths.StageDatasets,
                "SelectedUnitPanel_01.png"),
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "GameplayNegative",
                "GameplayNegative_06.png"),
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "GameplayNegative",
                "GameplayNegative_07.png"),
        };

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

    private static void CopyRegion(
        ImageFrame source,
        byte[] destination,
        int x,
        int y,
        int width,
        int height)
    {
        for (int row = y; row < y + height; row++)
        {
            int offset = (row * source.Width + x) * 3;
            int bytes = width * 3;
            Buffer.BlockCopy(
                source.Pixels,
                offset,
                destination,
                offset,
                bytes);
        }
    }

    private static ImageFrame LoadStage(string fileName) =>
        ImageCodec.Load(
            Path.Combine(TestPaths.StageDatasets, fileName));
}
