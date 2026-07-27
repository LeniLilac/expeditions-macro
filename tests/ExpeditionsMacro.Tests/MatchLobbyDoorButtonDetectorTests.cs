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

    [Fact]
    public void MissingArrow_RejectsOtherwiseLiveTopBar()
    {
        ImageFrame image = LoadNavigation(
            "MatchLobbyDoor_NoVoiceChat.png");
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
}
