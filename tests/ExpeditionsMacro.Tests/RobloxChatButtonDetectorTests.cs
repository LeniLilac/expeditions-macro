using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxChatButtonDetectorTests
{
    [Theory]
    [InlineData(
        "ChatClosed.png",
        RobloxChatButtonState.Closed)]
    [InlineData(
        "ChatOpen.png",
        RobloxChatButtonState.Open)]
    public void ReviewedIndicators_ReportTheLiveChatState(
        string fileName,
        RobloxChatButtonState expected)
    {
        RobloxChatButtonMatch match =
            RobloxChatButtonDetector.Detect(
                Load(fileName));

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.98, 1);
        Assert.Equal(
            RobloxChatButtonDetector.ActionX,
            match.ActionX);
        Assert.Equal(
            RobloxChatButtonDetector.ActionY,
            match.ActionY);
    }

    [Fact]
    public void MissingChatGlyph_IsNotActionable()
    {
        ImageFrame frame = new(
            808,
            611,
            PixelFormat.Rgb24,
            new byte[808 * 611 * 3]);

        RobloxChatButtonMatch match =
            RobloxChatButtonDetector.Detect(frame);

        Assert.Equal(
            RobloxChatButtonState.None,
            match.State);
        Assert.False(match.Available);
        Assert.Equal(0, match.ActionX);
        Assert.Equal(0, match.ActionY);
    }

    [Fact]
    public void AdjacentOpaqueControls_AreNotChatEvidence()
    {
        ImageFrame source = Load("ChatClosed.png");
        byte[] pixels = source.Pixels.ToArray();
        for (int y = 10; y < 58; y++)
        {
            for (int x = 163; x < 300; x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                pixels[pixel] = 255;
                pixels[pixel + 1] = 255;
                pixels[pixel + 2] = 255;
            }
        }
        ImageFrame changed = new(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);

        Assert.Equal(
            RobloxChatButtonState.Closed,
            RobloxChatButtonDetector
                .Detect(changed)
                .State);
    }

    private static ImageFrame Load(
        string fileName) =>
        ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                fileName));
}
