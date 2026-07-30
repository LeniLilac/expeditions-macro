using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Tests;

public sealed class UpgradeUnitReadinessDetectorTests
{
    [Theory]
    [InlineData(
        "UpgradeUnitAffordable_01.png",
        UpgradeUnitReadinessState.Affordable)]
    [InlineData(
        "UpgradeUnitAffordable_02.png",
        UpgradeUnitReadinessState.Affordable)]
    [InlineData(
        "UpgradeUnitUnaffordable_01.png",
        UpgradeUnitReadinessState.Unaffordable)]
    [InlineData(
        "UpgradeUnitMaxed_01.png",
        UpgradeUnitReadinessState.Maxed)]
    public void ReviewedUpgradeControls_MapToTheirExactState(
        string fileName,
        UpgradeUnitReadinessState expected)
    {
        UpgradeUnitReadinessMatch match =
            UpgradeUnitReadinessDetector.Detect(
                LoadStage(fileName));

        Assert.Equal(expected, match.State);
        Assert.True(match.PanelVisible);
    }

    [Fact]
    public void MaxedControl_IsWiderThanUnaffordableControl()
    {
        UpgradeUnitReadinessMatch maxed =
            UpgradeUnitReadinessDetector.Detect(
                LoadStage("UpgradeUnitMaxed_01.png"));
        UpgradeUnitReadinessMatch unaffordable =
            UpgradeUnitReadinessDetector.Detect(
                LoadStage(
                    "UpgradeUnitUnaffordable_01.png"));

        Assert.InRange(maxed.WideGrayScore, 0.50, 1);
        Assert.InRange(
            unaffordable.WideGrayScore,
            0,
            0.20);
    }

    [Fact]
    public void EveryOtherReviewedStageScreen_IsNotActionable()
    {
        string[] falseMatches = Directory
            .EnumerateFiles(
                TestPaths.StageDatasets,
                "*.png")
            .Where(path =>
                !Path.GetFileName(path).StartsWith(
                    "UpgradeUnit",
                    StringComparison.OrdinalIgnoreCase))
            .Where(path =>
                UpgradeUnitReadinessDetector
                    .Detect(ImageCodec.Load(path))
                    .State is
                    UpgradeUnitReadinessState.Affordable or
                    UpgradeUnitReadinessState.Maxed)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        Assert.Empty(falseMatches);
    }

    [Theory]
    [MemberData(nameof(CrossStateScreens))]
    public void OtherOwnedScreens_AreUnknown(
        string path)
    {
        UpgradeUnitReadinessMatch match =
            UpgradeUnitReadinessDetector.Detect(
                ImageCodec.Load(path));

        Assert.Equal(
            UpgradeUnitReadinessState.Unknown,
            match.State);
        Assert.False(match.PanelVisible);
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
            () => UpgradeUnitReadinessDetector
                .Detect(image));
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
                TestPaths.SettingsDatasets,
                "GraphicsPageCurrent.png"),
            Path.Combine(
                TestPaths.EventDatasets,
                "VictoryNextStage.png"),
        };

    private static ImageFrame LoadStage(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                fileName));
}
