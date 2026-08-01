using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Refuel;

namespace ExpeditionsMacro.Tests;

public sealed class ResourceRefuelDetectorTests
{
    [Theory]
    [InlineData("AreasMenu_01.png", AreasScreenState.Menu, 198, 312)]
    [InlineData("AreasMenu_UpgradeSelected_RedScene_01.png", AreasScreenState.Menu, 198, 305)]
    [InlineData("AreasExpeditions_01.png", AreasScreenState.Expeditions, 322, 264)]
    [InlineData("AreasLobby_01.png", AreasScreenState.Lobby, 318, 388)]
    public void AreasFixtures_MapTheirVerifiedNextAction(
        string file,
        AreasScreenState expected,
        int actionX,
        int actionY)
    {
        AreasScreenMatch match =
            AreasScreenDetector.Detect(Load(file));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.74, 1);
        Assert.Equal(actionX, match.ActionX);
        Assert.Equal(actionY, match.ActionY);
    }

    [Theory]
    [InlineData("AreasMenu_01.png", 258)]
    [InlineData("AreasMenu_UpgradeSelected_RedScene_01.png", 251)]
    [InlineData("AreasExpeditions_01.png", 252)]
    [InlineData("AreasLobby_01.png", 252)]
    public void EveryOwnedAreasSurface_ExposesLobbyCategoryAction(
        string file,
        int actionY)
    {
        AreasScreenMatch match =
            AreasScreenDetector.Detect(Load(file));

        Assert.NotEqual(AreasScreenState.None, match.State);
        Assert.Equal(198, match.LobbyTabActionX);
        Assert.Equal(actionY, match.LobbyTabActionY);
    }

    [Theory]
    [InlineData(
        "GoldMine_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        439)]
    [InlineData(
        "GoldMine_MissingFuel_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        430)]
    [InlineData(
        "GoldMine_FuelPresent_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        430)]
    [InlineData(
        "ResourceDrill_01.png",
        ResourceStationScreenState.ResourceDrill,
        406,
        430)]
    [InlineData(
        "ResourceDrill_MissingFuel_01.png",
        ResourceStationScreenState.ResourceDrill,
        406,
        430)]
    [InlineData(
        "ResourceDrill_FuelPresent_01.png",
        ResourceStationScreenState.ResourceDrill,
        406,
        430)]
    [InlineData(
        "GoldMine_AddFuel_01.png",
        ResourceStationScreenState.AddFuelDialog,
        515,
        312)]
    [InlineData(
        "ResourceDrill_AddFuel_01.png",
        ResourceStationScreenState.AddFuelDialog,
        515,
        313)]
    public void StationFixtures_MapTheirVerifiedNextAction(
        string file,
        ResourceStationScreenState expected,
        int actionX,
        int actionY)
    {
        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(Load(file));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.74, 1);
        Assert.Equal(actionX, match.ActionX);
        Assert.Equal(actionY, match.ActionY);
    }

    [Theory]
    [InlineData("GoldMine_AddFuel_01.png", 312, 344)]
    [InlineData("ResourceDrill_AddFuel_01.png", 313, 345)]
    public void AddFuelDialog_ExposesVerifiedMaxConfirmAndDismissActions(
        string file,
        int maximumY,
        int lowerActionY)
    {
        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(
                Load(file));

        Assert.Equal(
            ResourceStationScreenState.AddFuelDialog,
            match.State);
        Assert.Equal(515, match.ActionX);
        Assert.Equal(maximumY, match.ActionY);
        Assert.Equal(337, match.ConfirmActionX);
        Assert.Equal(lowerActionY, match.ConfirmActionY);
        Assert.Equal(470, match.DismissActionX);
        Assert.Equal(lowerActionY, match.DismissActionY);
    }

    [Theory]
    [InlineData("GoldMine_01.png", 635, 178)]
    [InlineData("GoldMine_MissingFuel_01.png", 635, 171)]
    [InlineData("GoldMine_FuelPresent_01.png", 635, 171)]
    [InlineData("ResourceDrill_01.png", 635, 171)]
    [InlineData("ResourceDrill_MissingFuel_01.png", 635, 171)]
    [InlineData("ResourceDrill_FuelPresent_01.png", 635, 171)]
    public void StationFixtures_ExposeTheirLiveCloseAction(
        string file,
        int actionX,
        int actionY)
    {
        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(Load(file));

        Assert.NotEqual(
            ResourceStationScreenState.None,
            match.State);
        Assert.Equal(actionX, match.DismissActionX);
        Assert.Equal(actionY, match.DismissActionY);
    }

    [Fact]
    public void OtherPanelFamilies_DoNotOwnAreasActions()
    {
        string[] files =
        [
            Path.Combine(
                TestPaths.SettingsDatasets,
                "GameplayPage.png"),
            Path.Combine(
                TestPaths.ChallengeDatasets,
                "ChallengeList",
                "ChallengeList_02.png"),
            Path.Combine(
                TestPaths.StageDatasets,
                "StoryDetail_Act_Wide_01.png"),
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                "Lobby_StoryDetail_01.png"),
        ];

        foreach (string file in files)
        {
            AreasScreenMatch match =
                AreasScreenDetector.Detect(
                    ImageCodec.Load(file));

            Assert.True(
                match.State == AreasScreenState.None,
                $"{Path.GetFileName(file)} classified as " +
                $"{match.State} ({match.Confidence:F3}).");
        }
    }

    [Fact]
    public void DecorativeBottomBorder_DoesNotOwnAreas()
    {
        ImageFrame withoutBorder = Paint(
            Load("AreasMenu_01.png"),
            new ScreenRegion(145, 435, 520, 30),
            red: 0,
            green: 0,
            blue: 0);

        AreasScreenMatch match =
            AreasScreenDetector.Detect(withoutBorder);

        Assert.Equal(AreasScreenState.Menu, match.State);
        Assert.Equal(198, match.ActionX);
        Assert.Equal(312, match.ActionY);
    }

    [Theory]
    [InlineData("GoldMine_MissingFuel_01.png", 210)]
    [InlineData("GoldMine_MissingFuel_01.png", 228)]
    [InlineData("GoldMine_AddFuel_01.png", 210)]
    [InlineData("GoldMine_AddFuel_01.png", 228)]
    public void EitherMissingBuildingStatsBar_RejectsStationOwnership(
        string file,
        int regionY)
    {
        ImageFrame missingBar = Paint(
            Load(file),
            new ScreenRegion(360, regionY, 280, 12),
            red: 0,
            green: 0,
            blue: 0);

        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(
                missingBar);

        Assert.Equal(
            ResourceStationScreenState.None,
            match.State);
    }

    [Fact]
    public void RewardContents_DoNotOwnStationStructure()
    {
        ImageFrame changedRewards = Paint(
            Load("ResourceDrill_MissingFuel_01.png"),
            new ScreenRegion(520, 260, 105, 115),
            red: 235,
            green: 235,
            blue: 235);

        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(
                changedRewards);

        Assert.Equal(
            ResourceStationScreenState.ResourceDrill,
            match.State);
        Assert.Equal(406, match.ActionX);
        Assert.Equal(430, match.ActionY);
    }

    [Fact]
    public void AreasActions_FollowTranslatedLiveLayout()
    {
        ImageFrame original = Load("AreasMenu_01.png");
        AreasScreenMatch baseline =
            AreasScreenDetector.Detect(original);

        AreasScreenMatch translated =
            AreasScreenDetector.Detect(
                Translate(original, deltaX: 3, deltaY: 2));

        Assert.Equal(baseline.State, translated.State);
        Assert.Equal(
            baseline.ActionX + 3,
            translated.ActionX);
        Assert.Equal(
            baseline.ActionY + 2,
            translated.ActionY);
        Assert.Equal(
            baseline.LobbyTabActionX + 3,
            translated.LobbyTabActionX);
        Assert.Equal(
            baseline.LobbyTabActionY + 2,
            translated.LobbyTabActionY);
    }

    [Fact]
    public void StationActions_FollowTranslatedLiveLayout()
    {
        ImageFrame original =
            Load("GoldMine_MissingFuel_01.png");
        ResourceStationScreenMatch baseline =
            ResourceStationScreenDetector.Detect(original);

        ResourceStationScreenMatch translated =
            ResourceStationScreenDetector.Detect(
                Translate(original, deltaX: 3, deltaY: 4));

        Assert.Equal(baseline.State, translated.State);
        Assert.Equal(
            baseline.ActionX + 3,
            translated.ActionX);
        Assert.Equal(
            baseline.ActionY + 4,
            translated.ActionY);
        Assert.Equal(
            baseline.DismissActionX + 3,
            translated.DismissActionX);
        Assert.Equal(
            baseline.DismissActionY + 4,
            translated.DismissActionY);
    }

    [Fact]
    public void AddFuelActions_FollowTranslatedLiveLayout()
    {
        ImageFrame original =
            Load("GoldMine_AddFuel_01.png");
        ResourceStationScreenMatch baseline =
            ResourceStationScreenDetector.Detect(original);

        ResourceStationScreenMatch translated =
            ResourceStationScreenDetector.Detect(
                Translate(original, deltaX: 3, deltaY: 4));

        Assert.Equal(baseline.State, translated.State);
        Assert.Equal(
            baseline.ActionX + 3,
            translated.ActionX);
        Assert.Equal(
            baseline.ActionY + 4,
            translated.ActionY);
        Assert.Equal(
            baseline.ConfirmActionX + 3,
            translated.ConfirmActionX);
        Assert.Equal(
            baseline.ConfirmActionY + 4,
            translated.ConfirmActionY);
        Assert.Equal(
            baseline.DismissActionX + 3,
            translated.DismissActionX);
        Assert.Equal(
            baseline.DismissActionY + 4,
            translated.DismissActionY);
    }

    [Theory]
    [InlineData("Lobby_UI", "Lobby_UI_001.png")]
    [InlineData("Play_UI", "Play_UI_001.png")]
    [InlineData(
        "Expedition_Victory_UI",
        "Expedition_Victory_UI_001.png")]
    public void ExistingMacroStates_DoNotLookLikeRefuelInterfaces(
        string directory,
        string file)
    {
        ImageFrame image = ImageCodec.Load(Path.Combine(
            TestPaths.Datasets,
            directory,
            file));

        Assert.Equal(
            AreasScreenState.None,
            AreasScreenDetector.Detect(image).State);
        Assert.Equal(
            ResourceStationScreenState.None,
            ResourceStationScreenDetector.Detect(image).State);
    }

    [Fact]
    public void ChallengeListCorpus_DoesNotLookLikeAResourceStation()
    {
        string directory = Path.Combine(
            TestPaths.ChallengeDatasets,
            "ChallengeList");

        foreach (string file in Directory.EnumerateFiles(
                     directory,
                     "*.png",
                     SearchOption.TopDirectoryOnly))
        {
            ResourceStationScreenMatch match =
                ResourceStationScreenDetector.Detect(
                    ImageCodec.Load(file));

            Assert.True(
                match.State == ResourceStationScreenState.None,
                $"{Path.GetFileName(file)} classified as " +
                $"{match.State} ({match.Confidence:F3}).");
        }
    }

    [Theory]
    [InlineData("TeamEquipmentConfirm_Compact_01.png")]
    [InlineData("TeamLoadConfirm_01.png")]
    [InlineData("TeamLoadConfirm_Bottom_Team7_01.png")]
    public void TeamConfirmationDialogs_DoNotLookLikeAddFuel(
        string file)
    {
        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(
                ImageCodec.Load(Path.Combine(
                    TestPaths.StageDatasets,
                    file)));

        Assert.Equal(
            ResourceStationScreenState.None,
            match.State);
    }

    [Fact]
    public void Detectors_RejectUnexpectedClientDimensions()
    {
        ImageFrame image = new(
            800,
            600,
            PixelFormat.Rgb24,
            new byte[800 * 600 * 3],
            takeOwnership: true);

        Assert.Throws<InvalidDataException>(
            () => AreasScreenDetector.Detect(image));
        Assert.Throws<InvalidDataException>(
            () => ResourceStationScreenDetector.Detect(image));
    }

    private static ImageFrame Load(string file) =>
        ImageCodec.Load(
            Path.Combine(TestPaths.RefuelDatasets, file));

    private static ImageFrame Paint(
        ImageFrame source,
        ScreenRegion region,
        byte red,
        byte green,
        byte blue)
    {
        ImageFrame result = source.Clone();
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * result.Width + x) * 3;
                result.Pixels[pixel] = red;
                result.Pixels[pixel + 1] = green;
                result.Pixels[pixel + 2] = blue;
            }
        }

        return result;
    }

    private static ImageFrame Translate(
        ImageFrame source,
        int deltaX,
        int deltaY)
    {
        byte[] pixels =
            new byte[source.Width * source.Height * 3];
        int sourceX = Math.Max(0, -deltaX);
        int sourceY = Math.Max(0, -deltaY);
        int targetX = Math.Max(0, deltaX);
        int targetY = Math.Max(0, deltaY);
        int width =
            source.Width - Math.Abs(deltaX);
        int height =
            source.Height - Math.Abs(deltaY);
        int rowBytes = width * 3;
        for (int row = 0; row < height; row++)
        {
            int sourceOffset =
                ((sourceY + row) * source.Width +
                 sourceX) * 3;
            int targetOffset =
                ((targetY + row) * source.Width +
                 targetX) * 3;
            Buffer.BlockCopy(
                source.Pixels,
                sourceOffset,
                pixels,
                targetOffset,
                rowBytes);
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            PixelFormat.Rgb24,
            pixels,
            takeOwnership: true);
    }
}
