using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public static class BountyNoGoldRecognizer
{
    private const int TemplateWidth = 173;
    private const int TemplateHeight = 9;
    private const double RequiredScore = 0.90;
    private const double RequiredBackdropFraction = 0.15;
    private const int BackdropHorizontalPadding = 6;
    private const int BackdropVerticalPadding = 4;

    private static readonly byte[] Template =
        Expand(
            "EQAAAICIYRhwIBAAAABQAIFAAAAAcAIAAACQcRwHDwQiAAAACiQACAAAAmom4TDGM9u2Mbh43nHmXMEfGc8l51HmJzzvfSSKIvafj33u3S+wp+P/5HnL7ITlvY1s28a4s5FNvNgFkmTsnSw5iP2QjLERRVGw9za2iRG7wZKevZ9kBuEcEuc84jgOnJuHHOHCJVDS8ZyTSAQAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAIAA");

    public static double Score(ImageFrame image)
    {
        Validate(image);
        double best = 0;
        for (int y = 34; y <= 45; y++)
        {
            for (int x = 310; x <= 325; x++)
            {
                int intersection = 0;
                int observed = 0;
                for (int localY = 0;
                     localY < TemplateHeight;
                     localY++)
                {
                    for (int localX = 0;
                         localX < TemplateWidth;
                         localX++)
                    {
                        bool live = IsNeutralText(
                            image,
                            x + localX,
                            y + localY);
                        bool expected =
                            Template[
                                localY * TemplateWidth +
                                localX] != 0;
                        if (live)
                        {
                            observed++;
                        }
                        if (live && expected)
                        {
                            intersection++;
                        }
                    }
                }

                const int expectedPixels = 497;
                double score =
                    2d * intersection /
                    Math.Max(
                        1,
                        observed + expectedPixels);
                // Neutral text alone does not own the field-observed red alert.
                if (score >= RequiredScore &&
                    score > best &&
                    HasAlertBackdrop(
                        image,
                        x,
                        y))
                {
                    best = score;
                }
            }
        }

        double result = best >= RequiredScore
            ? best
            : 0;
        VisionTrace.Emit(
            "bounty_no_gold",
            result > 0 ? "no_gold" : "none",
            result);
        return result;
    }

    private static bool HasAlertBackdrop(
        ImageFrame image,
        int textX,
        int textY)
    {
        int left = textX -
            BackdropHorizontalPadding;
        int top = textY -
            BackdropVerticalPadding;
        int width = TemplateWidth +
            2 * BackdropHorizontalPadding;
        int height = TemplateHeight +
            2 * BackdropVerticalPadding;
        int redPixels = 0;
        for (int y = top;
             y < top + height;
             y++)
        {
            for (int x = left;
                 x < left + width;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                byte red =
                    image.Pixels[pixel];
                byte green =
                    image.Pixels[pixel + 1];
                byte blue =
                    image.Pixels[pixel + 2];
                if (red >= 105 &&
                    red - green >= 35 &&
                    red - blue >= 25)
                {
                    redPixels++;
                }
            }
        }
        return (double)redPixels /
            (width * height) >=
            RequiredBackdropFraction;
    }

    private static bool IsNeutralText(
        ImageFrame image,
        int x,
        int y)
    {
        int pixel =
            (y * image.Width + x) * 3;
        byte red = image.Pixels[pixel];
        byte green = image.Pixels[pixel + 1];
        byte blue = image.Pixels[pixel + 2];
        int maximum = Math.Max(
            red,
            Math.Max(green, blue));
        int minimum = Math.Min(
            red,
            Math.Min(green, blue));
        return minimum > 170 &&
            maximum - minimum < 45;
    }

    private static byte[] Expand(string encoded)
    {
        byte[] packed =
            Convert.FromBase64String(encoded);
        byte[] pixels =
            new byte[
                TemplateWidth * TemplateHeight];
        for (int bit = 0;
             bit < pixels.Length;
             bit++)
        {
            if ((packed[bit / 8] &
                 (1 << (bit % 8))) != 0)
            {
                pixels[bit] = 255;
            }
        }
        return pixels;
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new ArgumentException(
                "Bounty banner detection requires an 808 by 611 RGB client capture.",
                nameof(image));
        }
    }
}
