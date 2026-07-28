using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision;

namespace ExpeditionsMacro.Tests;

public sealed class TranslationAwareImageScorerTests
{
    [Fact]
    public void Score_CompensatesForSmallProjectionTranslation()
    {
        ImageFrame reference = VisionScorer.PrepareGray(
            VisionScorerTests.Pattern(808, 611),
            160,
            101);
        ImageFrame shifted = Shift(
            reference,
            deltaX: 3,
            deltaY: -2);

        TranslationAwareImageMatch match =
            TranslationAwareImageScorer.Score(
                reference,
                shifted,
                maximumTranslation: 5);

        Assert.True(
            match.Score >= 0.97,
            $"Expected a strong registered match, got {match.Score:P2}.");
        Assert.InRange(
            Math.Abs(match.OffsetX),
            2,
            4);
        Assert.InRange(
            Math.Abs(match.OffsetY),
            1,
            3);
    }

    [Fact]
    public void Score_RejectsNonGrayscaleInput()
    {
        ImageFrame rgb =
            VisionScorerTests.Pattern(80, 50);

        Assert.Throws<ArgumentException>(
            () => TranslationAwareImageScorer.Score(
                rgb,
                rgb));
    }

    private static ImageFrame Shift(
        ImageFrame source,
        int deltaX,
        int deltaY)
    {
        byte[] pixels =
            new byte[source.Pixels.Length];
        for (int y = 0; y < source.Height; y++)
        {
            int sourceY = Math.Clamp(
                y - deltaY,
                0,
                source.Height - 1);
            for (int x = 0; x < source.Width; x++)
            {
                int sourceX = Math.Clamp(
                    x - deltaX,
                    0,
                    source.Width - 1);
                pixels[y * source.Width + x] =
                    source.Pixels[
                        sourceY * source.Width +
                        sourceX];
            }
        }
        return new ImageFrame(
            source.Width,
            source.Height,
            PixelFormat.Gray8,
            pixels,
            takeOwnership: true);
    }
}
