using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxSettingsButtonDetectorTests
{
    [Theory]
    [InlineData(
        "LobbyClosed.png",
        RobloxSettingsButtonDetector.NoVoiceActionX)]
    [InlineData(
        "SettingsButtonVoiceClosed.png",
        RobloxSettingsButtonDetector.VoiceActionX)]
    public void ClosedTopBars_LocateTheirGear(
        string fileName,
        int expectedX)
    {
        RobloxSettingsButtonMatch match =
            RobloxSettingsButtonDetector.Detect(
                Load(fileName));

        Assert.Equal(
            RobloxSettingsButtonState.Closed,
            match.State);
        Assert.InRange(match.Confidence, 0.76, 1);
        Assert.Equal(expectedX, match.ActionX);
        Assert.Equal(
            RobloxSettingsButtonDetector.ActionY,
            match.ActionY);
    }

    [Theory]
    [InlineData("SettingsScale080.png")]
    [InlineData("SettingsScale100.png")]
    [InlineData("SettingsScale120.png")]
    public void SelectedGear_IsIndependentOfGameUiScale(
        string fileName)
    {
        RobloxSettingsButtonMatch match =
            RobloxSettingsButtonDetector.Detect(
                Load(fileName));

        Assert.Equal(
            RobloxSettingsButtonState.Selected,
            match.State);
        Assert.Equal(
            RobloxSettingsButtonDetector.NoVoiceActionX,
            match.ActionX);
        Assert.Equal(
            RobloxSettingsButtonDetector.ActionY,
            match.ActionY);
    }

    [Fact]
    public void VoiceControlPixels_AreNotDetectorEvidence()
    {
        ImageFrame frame = PaintRegion(
            Load("SettingsButtonVoiceClosed.png"),
            left: 160,
            top: 14,
            right: 210,
            bottom: 55);

        RobloxSettingsButtonMatch match =
            RobloxSettingsButtonDetector.Detect(frame);

        Assert.Equal(
            RobloxSettingsButtonState.Closed,
            match.State);
        Assert.Equal(
            RobloxSettingsButtonDetector.VoiceActionX,
            match.ActionX);
    }

    [Fact]
    public void EllipsisAndDoorWithoutGear_AreRejected()
    {
        ImageFrame frame = PaintRegion(
            Load("SettingsButtonVoiceClosed.png"),
            left: 262,
            top: 20,
            right: 290,
            bottom: 49);

        RobloxSettingsButtonMatch match =
            RobloxSettingsButtonDetector.Detect(frame);

        Assert.Equal(
            RobloxSettingsButtonState.None,
            match.State);
        Assert.False(match.Available);
    }

    private static ImageFrame Load(string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.SettingsDatasets,
                fileName));

    private static ImageFrame PaintRegion(
        ImageFrame source,
        int left,
        int top,
        int right,
        int bottom)
    {
        byte[] pixels = source.Pixels.ToArray();
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                pixels[pixel] = 20;
                pixels[pixel + 1] = 20;
                pixels[pixel + 2] = 20;
            }
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }
}
