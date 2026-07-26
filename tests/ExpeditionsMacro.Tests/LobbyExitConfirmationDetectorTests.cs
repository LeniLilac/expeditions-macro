using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class LobbyExitConfirmationDetectorTests
{
    [Fact]
    public void ReviewedConfirmation_ReportsReturnToLobbyAction()
    {
        ImageFrame image = ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                "LobbyExitConfirmation.png"));

        LobbyExitConfirmationMatch match =
            LobbyExitConfirmationDetector.Detect(image);

        Assert.True(match.Visible);
        Assert.InRange(match.Confidence, 0.45, 1);
        Assert.Equal(345, match.ActionX);
        Assert.Equal(328, match.ActionY);
    }

    [Fact]
    public void OrdinaryModeDetails_DoNotBecomeExitConfirmation()
    {
        foreach (string file in Directory.EnumerateFiles(
                     TestPaths.NavigationVariantDatasets,
                     "*.png"))
        {
            if (Path.GetFileName(file).Equals(
                    "LobbyExitConfirmation.png",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Assert.False(
                LobbyExitConfirmationDetector
                    .Detect(ImageCodec.Load(file))
                    .Visible,
                Path.GetFileName(file));
        }
    }
}
