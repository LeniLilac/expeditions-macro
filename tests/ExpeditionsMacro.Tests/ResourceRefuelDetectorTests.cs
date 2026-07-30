using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Refuel;

namespace ExpeditionsMacro.Tests;

public sealed class ResourceRefuelDetectorTests
{
    [Theory]
    [InlineData("AreasMenu_01.png", AreasScreenState.Menu, 198, 304)]
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
    [InlineData("AreasMenu_01.png")]
    [InlineData("AreasExpeditions_01.png")]
    [InlineData("AreasLobby_01.png")]
    public void EveryOwnedAreasSurface_ExposesLobbyCategoryAction(
        string file)
    {
        AreasScreenMatch match =
            AreasScreenDetector.Detect(Load(file));

        Assert.NotEqual(AreasScreenState.None, match.State);
        Assert.Equal(198, match.LobbyTabActionX);
        Assert.Equal(252, match.LobbyTabActionY);
    }

    [Theory]
    [InlineData(
        "GoldMine_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        438)]
    [InlineData(
        "GoldMine_MissingFuel_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        438)]
    [InlineData(
        "GoldMine_FuelPresent_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        438)]
    [InlineData(
        "ResourceDrill_01.png",
        ResourceStationScreenState.ResourceDrill,
        406,
        429)]
    [InlineData(
        "ResourceDrill_MissingFuel_01.png",
        ResourceStationScreenState.ResourceDrill,
        406,
        429)]
    [InlineData(
        "ResourceDrill_FuelPresent_01.png",
        ResourceStationScreenState.ResourceDrill,
        406,
        429)]
    [InlineData(
        "GoldMine_AddFuel_01.png",
        ResourceStationScreenState.AddFuelDialog,
        516,
        312)]
    [InlineData(
        "ResourceDrill_AddFuel_01.png",
        ResourceStationScreenState.AddFuelDialog,
        516,
        312)]
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
    [InlineData("GoldMine_AddFuel_01.png")]
    [InlineData("ResourceDrill_AddFuel_01.png")]
    public void AddFuelDialog_ExposesVerifiedMaxConfirmAndDismissActions(
        string file)
    {
        ResourceStationScreenMatch match =
            ResourceStationScreenDetector.Detect(
                Load(file));

        Assert.Equal(
            ResourceStationScreenState.AddFuelDialog,
            match.State);
        Assert.Equal(516, match.ActionX);
        Assert.Equal(312, match.ActionY);
        Assert.Equal(337, match.ConfirmActionX);
        Assert.Equal(345, match.ConfirmActionY);
        Assert.Equal(470, match.DismissActionX);
        Assert.Equal(345, match.DismissActionY);
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
}
