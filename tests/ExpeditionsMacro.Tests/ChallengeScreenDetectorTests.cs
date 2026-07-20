using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeScreenDetectorTests
{
    [Fact]
    public void FixedChallengeTypes_MapToStableSelectorRows()
    {
        Assert.Equal((320, 244), ChallengeScreenDetector.ActionForType(ChallengeType.Trait));
        Assert.Equal((320, 335), ChallengeScreenDetector.ActionForType(ChallengeType.Stat));
        Assert.Equal((320, 425), ChallengeScreenDetector.ActionForType(ChallengeType.Sprite));
    }

    [Fact]
    public void BlankFrame_DoesNotMatchAChallengeScreen()
    {
        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(Frame());

        Assert.Equal(ChallengeScreenState.None, match.State);
    }

    [Fact]
    public void GameModeSelector_UsesAllFourStableModeHeadings()
    {
        ImageFrame image = Frame();
        Fill(image, 370, 160, 20, 10, 200, 150, 20);
        Fill(image, 370, 60, 12, 10, 20, 160, 200);
        Fill(image, 580, 60, 12, 10, 190, 30, 25);
        Fill(image, 610, 160, 12, 10, 25, 150, 70);
        Fill(image, 355, 150, 440, 2, 20, 20, 20);
        Fill(image, 570, 45, 2, 215, 20, 20, 20);

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(image);

        Assert.Equal(ChallengeScreenState.GameModeSelector, match.State);
        Assert.Equal((480, 205), (match.ActionX, match.ActionY));
    }

    [Fact]
    public void GameModeSelector_WithBrightChallengeArtwork_UsesStableDividers()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "GameModeSelector", "GameModeSelector_05.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.GameModeSelector, match.State);
        Assert.InRange(match.Confidence, ChallengeScreenDetector.Threshold(ChallengeScreenState.GameModeSelector), 1);
        Assert.Equal((480, 205), (match.ActionX, match.ActionY));
    }

    [Fact]
    public void NonSelectorChallengeFixtures_DoNotScoreAsTheGameModeSelector()
    {
        string selectorDirectory = Path.Combine(TestPaths.ChallengeDatasets, "GameModeSelector");
        foreach (string file in Directory.EnumerateFiles(TestPaths.ChallengeDatasets, "*.png", SearchOption.AllDirectories))
        {
            if (Path.GetDirectoryName(file)!.Equals(selectorDirectory, StringComparison.OrdinalIgnoreCase)) continue;

            double score = ChallengeScreenDetector.ScoreStates(ImageCodec.Load(file))[ChallengeScreenState.GameModeSelector];
            Assert.True(
                score < ChallengeScreenDetector.Threshold(ChallengeScreenState.GameModeSelector),
                $"{Path.GetFileName(file)} scored {score:P1} as the game-mode selector.");
        }
    }

    [Fact]
    public void ChallengeList_RequiresSparseIconsInTheFixedRows()
    {
        ImageFrame image = Panel();
        foreach (int centerY in new[] { 244, 335, 425 }) Fill(image, 356, centerY - 10, 18, 18, 20, 170, 210);

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(image);

        Assert.Equal(ChallengeScreenState.ChallengeList, match.State);
        Assert.Null(match.ActionX);
    }

    [Fact]
    public void ChallengeList_WithDimmedUnhoveredRows_UsesTheRepeatedRowStructure()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_11.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.ChallengeList, match.State);
        Assert.InRange(match.Confidence, ChallengeScreenDetector.Threshold(ChallengeScreenState.ChallengeList), 1);
        foreach (ChallengeType type in Enum.GetValues<ChallengeType>())
        {
            (int x, int y) = ChallengeScreenDetector.ActionForType(type);
            Assert.InRange(x, 280, 380);
            Assert.InRange(y, 210, 455);
        }
    }

    [Fact]
    public void UnavailableRegularChallengeList_IsAThirtyMinuteCooldownState()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeListUnavailable", "ChallengeListUnavailable_01.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.ChallengeListUnavailable, match.State);
        Assert.InRange(match.Confidence, ChallengeScreenDetector.Threshold(ChallengeScreenState.ChallengeListUnavailable), 1);
        Assert.Null(match.ActionX);
        Assert.Null(match.ActionY);
    }

    [Fact]
    public void AvailableChallenge_ClicksTheDetectedSelectStageButton()
    {
        ImageFrame image = Panel();
        Button(image, 346, 422, 160, 30, 25, 180, 45);

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(image);

        Assert.Equal(ChallengeScreenState.ChallengeAvailable, match.State);
        Assert.InRange(match.ActionX!.Value, 390, 465);
        Assert.InRange(match.ActionY!.Value, 425, 450);
    }

    [Fact]
    public void Preview_UsesTheGreenStartAndYellowChangeModePair()
    {
        ImageFrame image = Frame();
        Button(image, 426, 362, 160, 29, 25, 180, 45);
        Button(image, 589, 362, 158, 29, 190, 135, 20);

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(image);

        Assert.Equal(ChallengeScreenState.PreviewReady, match.State);
        Assert.InRange(match.ActionX!.Value, 470, 540);
        Assert.InRange(match.ActionY!.Value, 365, 385);
    }

    [Fact]
    public void PrivatePartyPreview_UsesTheGreenStartAndRedDisbandPair()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "PreviewReady", "PreviewReady_03.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.PreviewReady, match.State);
        Assert.InRange(match.ActionX!.Value, 495, 515);
        Assert.InRange(match.ActionY!.Value, 385, 400);
    }

    [Fact]
    public void Detector_RejectsAClientWithUnexpectedDimensions()
    {
        ImageFrame image = new(800, 600, PixelFormat.Rgb24, new byte[800 * 600 * 3], takeOwnership: true);

        Assert.Throws<InvalidDataException>(() => ChallengeScreenDetector.Detect(image));
    }

    [Theory]
    [InlineData("GameModeSelector", ChallengeScreenState.GameModeSelector)]
    [InlineData("ChallengeList", ChallengeScreenState.ChallengeList)]
    [InlineData("ChallengeListUnavailable", ChallengeScreenState.ChallengeListUnavailable)]
    [InlineData("ChallengeAvailable", ChallengeScreenState.ChallengeAvailable)]
    [InlineData("ChallengeCooldown", ChallengeScreenState.ChallengeCooldown)]
    [InlineData("PreviewReady", ChallengeScreenState.PreviewReady)]
    [InlineData("PostMatchPreview", ChallengeScreenState.PostMatchPreview)]
    [InlineData("Victory", ChallengeScreenState.Victory)]
    [InlineData("Defeat", ChallengeScreenState.Defeat)]
    public void ReviewedChallengeFixtures_MatchTheirExpectedState(string dataset, ChallengeScreenState expected)
    {
        string directory = Path.Combine(TestPaths.ChallengeDatasets, dataset);
        foreach (string file in Directory.EnumerateFiles(directory, "*.png"))
        {
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));
            Assert.Equal(expected, match.State);
        }
    }

    [Theory]
    [InlineData("Prestart_RoseKingdom")]
    [InlineData("Prestart_SchoolGrounds")]
    [InlineData("Prestart_FairyKingForest")]
    [InlineData("Prestart_KingsTomb")]
    [InlineData("Prestart_FlowerForest")]
    public void ReviewedMapPrestarts_MatchTheStartDialog(string dataset)
    {
        string directory = Path.Combine(TestPaths.ChallengeDatasets, dataset);
        foreach (string file in Directory.EnumerateFiles(directory, "*.png"))
        {
            Assert.Equal(ChallengeScreenState.Prestart, ChallengeScreenDetector.Detect(ImageCodec.Load(file)).State);
        }
    }

    [Fact]
    public void BlueFlowerForestScenery_DoesNotSuppressTheStartDialog()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "Prestart_FlowerForest", "Prestart_FlowerForest_02.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.Prestart, match.State);
        Assert.InRange(match.Confidence, 0.95, 1);
        Assert.InRange(match.ActionX!.Value, 398, 410);
        Assert.InRange(match.ActionY!.Value, 172, 184);
    }

    [Fact]
    public void WiderVictoryPanel_ClicksTheDetectedRightShiftedCloseButton()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "Victory", "Victory_05.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.Victory, match.State);
        Assert.InRange(match.ActionX!.Value, 675, 690);
        Assert.InRange(match.ActionY!.Value, 145, 160);
    }

    [Fact]
    public void HoveredVictoryCloseButton_RemainsActionable()
    {
        string file = Path.Combine(TestPaths.ChallengeDatasets, "Victory", "Victory_07.png");

        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));

        Assert.Equal(ChallengeScreenState.Victory, match.State);
        Assert.InRange(match.ActionX!.Value, 675, 690);
        Assert.InRange(match.ActionY!.Value, 140, 155);
    }

    [Fact]
    public void ChallengeDetailTooltip_DoesNotMatchATerminalScreen()
    {
        string directory = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeDetailTooltipNegative");
        foreach (string file in Directory.EnumerateFiles(directory, "*.png"))
        {
            ChallengeScreenState state = ChallengeScreenDetector.Detect(ImageCodec.Load(file)).State;
            Assert.True(state is not ChallengeScreenState.Victory and not ChallengeScreenState.Defeat);
        }
    }

    [Theory]
    [InlineData("GameplayNegative")]
    [InlineData("ExpeditionHandoffNegative")]
    public void ChallengeNegativeFixtures_DoNotMatchActionableChallengeScreens(string dataset)
    {
        string directory = Path.Combine(TestPaths.ChallengeDatasets, dataset);
        foreach (string file in Directory.EnumerateFiles(directory, "*.png"))
        {
            ChallengeScreenState state = ChallengeScreenDetector.Detect(ImageCodec.Load(file)).State;
            Assert.True(state is ChallengeScreenState.None or ChallengeScreenState.Prestart);
        }
    }

    [Theory]
    [InlineData("Expedition_Reward_Select")]
    [InlineData("Expedition_Reward_Select2")]
    [InlineData("Expedition_Reward_Select3")]
    [InlineData("Expedition_Reward_Select4")]
    [InlineData("Expedition_Reward_Transition")]
    public void ExpeditionsRewardDatasets_DoNotMatchChallengePanels(string dataset)
    {
        string directory = Path.Combine(TestPaths.Datasets, dataset);
        if (!Directory.Exists(directory)) return;

        foreach (string file in Directory.EnumerateFiles(directory, "*.png"))
        {
            ChallengeScreenMatch match = ChallengeScreenDetector.Detect(ImageCodec.Load(file));
            Assert.Equal(ChallengeScreenState.None, match.State);
        }
    }

    [Fact]
    public void ExpeditionsFixtures_DoNotMatchChallengeOnlyStates()
    {
        ChallengeScreenState[] challengeOnlyStates =
        [
            ChallengeScreenState.ChallengeList,
            ChallengeScreenState.ChallengeListUnavailable,
            ChallengeScreenState.ChallengeAvailable,
            ChallengeScreenState.ChallengeCooldown,
            ChallengeScreenState.PreviewReady,
            ChallengeScreenState.PostMatchPreview,
            ChallengeScreenState.Victory,
        ];
        foreach (string file in Directory.EnumerateFiles(TestPaths.Datasets, "*.png", SearchOption.AllDirectories))
        {
            ChallengeScreenState state = ChallengeScreenDetector.Detect(ImageCodec.Load(file)).State;
            Assert.DoesNotContain(state, challengeOnlyStates);
        }
    }

    private static ImageFrame Panel()
    {
        ImageFrame image = Frame();
        Fill(image, 90, 120, 220, 80, 20, 170, 210);
        Fill(image, 280, 151, 380, 2, 20, 170, 210);
        Fill(image, 675, 140, 2, 330, 20, 170, 210);
        Fill(image, 115, 455, 575, 2, 20, 170, 210);
        return image;
    }

    private static ImageFrame Frame() => new(
        ChallengeScreenDetector.ClientWidth,
        ChallengeScreenDetector.ClientHeight,
        PixelFormat.Rgb24,
        new byte[ChallengeScreenDetector.ClientWidth * ChallengeScreenDetector.ClientHeight * 3],
        takeOwnership: true);

    private static void Button(ImageFrame image, int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        Fill(image, x, y, width, height, red, green, blue);
        Fill(image, x + width / 3, y + height / 3, width / 5, height / 4, 0, 0, 0);
    }

    private static void Fill(ImageFrame image, int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                int pixel = (row * image.Width + column) * 3;
                image.Pixels[pixel] = red;
                image.Pixels[pixel + 1] = green;
                image.Pixels[pixel + 2] = blue;
            }
        }
    }
}
