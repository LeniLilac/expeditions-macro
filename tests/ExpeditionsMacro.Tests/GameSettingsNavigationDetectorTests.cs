using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Tests;

public sealed class GameSettingsNavigationDetectorTests
{
    [Theory]
    [InlineData(
        "SettingsScale080.png",
        232,
        330,
        377,
        239)]
    [InlineData(
        "SettingsScale100.png",
        188,
        337,
        370,
        222)]
    [InlineData(
        "SettingsScale120.png",
        146,
        344,
        363,
        206)]
    public void SupportedRenderedScales_ExposeLiveMiscAndInputActions(
        string fileName,
        int expectedMiscX,
        int expectedMiscY,
        int expectedInputX,
        int expectedInputY)
    {
        ImageFrame frame = Load(fileName);

        GameSettingsPageMatch selected =
            GameSettingsNavigationDetector
                .DetectSelectedPage(frame);
        GameSettingsNavigationActionMatch misc =
            GameSettingsNavigationDetector
                .DetectPageAction(
                    frame,
                    GameSettingsPage.Miscellaneous);
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(frame);

        Assert.Equal(
            GameSettingsPage.Miscellaneous,
            selected.Page);
        Assert.InRange(
            selected.Confidence,
            0.65,
            1);
        Assert.True(misc.Available);
        Assert.InRange(misc.Confidence, 0.65, 1);
        Assert.Equal(
            (expectedMiscX, expectedMiscY),
            (misc.ActionX, misc.ActionY));
        Assert.True(input.Available);
        Assert.True(input.Focused);
        Assert.InRange(input.Confidence, 0.60, 1);
        Assert.Equal(
            (expectedInputX, expectedInputY),
            (input.ActionX, input.ActionY));
    }

    [Fact]
    public void UnfocusedCurrentInput_RemainsAvailableForClick()
    {
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(
                    Load(
                        "MiscellaneousPageCurrent.png"));

        Assert.True(input.Available);
        Assert.False(input.Focused);
        Assert.Equal(
            (370, 222),
            (input.ActionX, input.ActionY));
    }

    [Theory]
    [InlineData("MiscellaneousPage.png")]
    [InlineData("MiscellaneousPageEventUpdate.png")]
    public void CompactMiscLayouts_UseTheirLiveInputRow(
        string fileName)
    {
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(
                    Load(fileName));

        Assert.True(input.Available);
        Assert.False(input.Focused);
        Assert.Equal(
            (370, 208),
            (input.ActionX, input.ActionY));
    }

    [Theory]
    [InlineData("GameplayPage.png")]
    [InlineData("GraphicsPageCurrent.png")]
    [InlineData("UnitsTop.png")]
    public void OtherPages_ExposeMiscNavigationButNotScaleInput(
        string fileName)
    {
        ImageFrame frame = Load(fileName);

        GameSettingsNavigationActionMatch misc =
            GameSettingsNavigationDetector
                .DetectPageAction(
                    frame,
                    GameSettingsPage.Miscellaneous);
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(frame);

        Assert.True(misc.Available);
        Assert.False(input.Available);
        Assert.False(input.Focused);
        Assert.Equal((0, 0),
            (input.ActionX, input.ActionY));
    }

    [Theory]
    [InlineData("LobbyClosed.png")]
    [InlineData("LobbyEventTheme.png")]
    [InlineData("SettingsOpeningTransition.png")]
    public void NonSettledSettingsStates_ExposeNoActions(
        string fileName)
    {
        ImageFrame frame = Load(fileName);

        Assert.Equal(
            GameSettingsPage.None,
            GameSettingsNavigationDetector
                .DetectSelectedPage(frame).Page);
        Assert.False(
            GameSettingsNavigationDetector
                .DetectPageAction(
                    frame,
                    GameSettingsPage.Miscellaneous)
                .Available);
        Assert.False(
            GameSettingsNavigationDetector
                .DetectUiScaleInput(frame)
                .Available);
    }

    [Fact]
    public void MissingSelectedTab_PreventsBlindPageClick()
    {
        ImageFrame frame = Paint(
            Load("SettingsScale100.png"),
            new ScreenRegion(
                140,
                327,
                97,
                21),
            20,
            20,
            20);

        Assert.Equal(
            GameSettingsPage.None,
            GameSettingsNavigationDetector
                .DetectSelectedPage(frame).Page);
        Assert.False(
            GameSettingsNavigationDetector
                .DetectPageAction(
                    frame,
                    GameSettingsPage.Miscellaneous)
                .Available);
    }

    [Fact]
    public void MissingSliderStructure_PreventsBlindInputClick()
    {
        ImageFrame frame = Paint(
            Load("MiscellaneousPageCurrent.png"),
            new ScreenRegion(
                382,
                217,
                70,
                10),
            10,
            10,
            10);

        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(frame);

        Assert.False(input.Available);
        Assert.False(input.Focused);
        Assert.Equal((0, 0),
            (input.ActionX, input.ActionY));
    }

    private static ImageFrame Paint(
        ImageFrame source,
        ScreenRegion region,
        byte red,
        byte green,
        byte blue)
    {
        byte[] pixels = source.Pixels.ToArray();
        for (int y = region.Y;
             y < region.Bottom;
             y++)
        {
            for (int x = region.X;
                 x < region.Right;
                 x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                pixels[pixel] = red;
                pixels[pixel + 1] = green;
                pixels[pixel + 2] = blue;
            }
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }

    private static ImageFrame Load(
        string name) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.SettingsDatasets,
                name));
}
