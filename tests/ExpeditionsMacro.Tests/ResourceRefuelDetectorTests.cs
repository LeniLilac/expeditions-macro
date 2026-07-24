using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Refuel;

namespace ExpeditionsMacro.Tests;

public sealed class ResourceRefuelDetectorTests
{
    [Theory]
    [InlineData("AreasMenu_01.png", AreasScreenState.Menu, 198, 304)]
    [InlineData("AreasExpeditions_01.png", AreasScreenState.Expeditions, 322, 264)]
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
    [InlineData(
        "GoldMine_01.png",
        ResourceStationScreenState.GoldMine,
        406,
        438)]
    [InlineData(
        "ResourceDrill_01.png",
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
