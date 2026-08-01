using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class MatchLobbyDoorButtonDetectorTests
{
    [Theory]
    [InlineData(
        "MatchLobbyDoor_NoVoiceChat.png",
        MatchLobbyDoorLayout.NoVoiceChat,
        270)]
    [InlineData(
        "MatchLobbyDoor_VoiceChat.png",
        MatchLobbyDoorLayout.VoiceChat,
        314)]
    [InlineData(
        "MatchLobbyDoor_HighContrastNoVoice.png",
        MatchLobbyDoorLayout.NoVoiceChat,
        270)]
    public void ReviewedTopBars_ReportTheDoorAtItsLiveOffset(
        string fileName,
        MatchLobbyDoorLayout expectedLayout,
        int expectedX)
    {
        ImageFrame image = LoadNavigation(fileName);

        MatchLobbyDoorButtonMatch match =
            MatchLobbyDoorButtonDetector.Detect(image);

        Assert.True(match.Visible);
        Assert.InRange(match.Confidence, 0.75, 1);
        Assert.Equal(expectedLayout, match.Layout);
        Assert.Equal(expectedX, match.ActionX);
        Assert.Equal(35, match.ActionY);
    }

    [Theory]
    [InlineData("MatchLobbyDoor_NoVoiceChat.png")]
    [InlineData("MatchLobbyDoor_HighContrastNoVoice.png")]
    public void MissingArrow_RejectsOtherwiseLiveTopBar(
        string fileName)
    {
        ImageFrame image = LoadNavigation(fileName);
        for (int y = 37; y <= 46; y++)
        {
            for (int x = 272; x <= 281; x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                image.Pixels[pixel] = 0;
                image.Pixels[pixel + 1] = 0;
                image.Pixels[pixel + 2] = 0;
            }
        }

        Assert.False(
            MatchLobbyDoorButtonDetector
                .Detect(image)
                .Visible);
    }

    [Fact]
    public void HighContrastDoor_OnePixelJambThickeningRemainsOwned()
    {
        ImageFrame image = LoadNavigation(
            "MatchLobbyDoor_HighContrastNoVoice.png");
        for (int y = 26; y <= 35; y++)
        {
            SetOpaqueWhite(image, 275, y);
        }

        MatchLobbyDoorButtonMatch match =
            MatchLobbyDoorButtonDetector.Detect(image);

        Assert.True(match.Visible);
        Assert.Equal(MatchLobbyDoorLayout.NoVoiceChat, match.Layout);
        Assert.Equal(270, match.ActionX);
    }

    [Fact]
    public void HighContrastDoor_OnePixelOuterJambShiftRemainsOwned()
    {
        ImageFrame image = LoadNavigation(
            "MatchLobbyDoor_HighContrastNoVoice.png");
        for (int y = 27; y <= 35; y++)
        {
            SetBlack(image, 274, y);
            SetOpaqueWhite(image, 275, y);
        }
        SetOpaqueWhite(image, 275, 26);

        MatchLobbyDoorButtonMatch match =
            MatchLobbyDoorButtonDetector.Detect(image);

        Assert.True(match.Visible);
        Assert.Equal(MatchLobbyDoorLayout.NoVoiceChat, match.Layout);
        Assert.Equal(270, match.ActionX);
    }

    [Theory]
    [InlineData("MatchLobbyDoor_NoVoiceChat.png")]
    [InlineData("MatchLobbyDoor_HighContrastNoVoice.png")]
    public void MissingHandle_RejectsOtherwiseLiveTopBar(
        string fileName)
    {
        ImageFrame image = LoadNavigation(fileName);
        for (int y = 34; y <= 38; y++)
        {
            for (int x = 265; x <= 268; x++)
            {
                SetBlack(image, x, y);
            }
        }

        Assert.False(
            MatchLobbyDoorButtonDetector
                .Detect(image)
                .Visible);
    }

    [Theory]
    [InlineData("MatchLobbyDoor_NoVoiceChat.png")]
    [InlineData("MatchLobbyDoor_HighContrastNoVoice.png")]
    public void FilledDoorInterior_RejectsOtherwiseLiveTopBar(
        string fileName)
    {
        ImageFrame image = LoadNavigation(fileName);
        for (int y = 29; y <= 33; y++)
        {
            for (int x = 263; x <= 267; x++)
            {
                SetOpaqueWhite(image, x, y);
            }
        }

        Assert.False(
            MatchLobbyDoorButtonDetector
                .Detect(image)
                .Visible);
    }

    [Fact]
    public void LobbySettingsAndModeDetails_DoNotBecomeDoor()
    {
        IEnumerable<string> files =
            Directory.EnumerateFiles(
                TestPaths.SettingsDatasets,
                "*.png")
            .Concat(
                Directory.EnumerateFiles(
                    TestPaths.NavigationVariantDatasets,
                    "*.png")
                .Where(path =>
                    !Path.GetFileName(path).StartsWith(
                        "MatchLobbyDoor_",
                        StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(path).Equals(
                        "LobbyExitConfirmation.png",
                        StringComparison.OrdinalIgnoreCase)))
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(
                        TestPaths.Datasets,
                        "Lobby_UI"),
                    "*.png"))
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(
                        TestPaths.Datasets,
                        "Lobby_UI2"),
                    "*.png"));

        foreach (string file in files)
        {
            Assert.False(
                MatchLobbyDoorButtonDetector
                    .Detect(ImageCodec.Load(file))
                    .Visible,
                file);
        }
    }

    private static ImageFrame LoadNavigation(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                fileName));

    private static void SetOpaqueWhite(
        ImageFrame image,
        int x,
        int y) =>
        SetRgb(image, x, y, 255);

    private static void SetBlack(
        ImageFrame image,
        int x,
        int y) =>
        SetRgb(image, x, y, 0);

    private static void SetRgb(
        ImageFrame image,
        int x,
        int y,
        byte value)
    {
        int pixel = (y * image.Width + x) * 3;
        image.Pixels[pixel] = value;
        image.Pixels[pixel + 1] = value;
        image.Pixels[pixel + 2] = value;
    }
}
