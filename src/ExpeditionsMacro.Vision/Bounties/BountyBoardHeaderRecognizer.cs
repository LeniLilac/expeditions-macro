using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Bounties;

internal static class BountyBoardHeaderRecognizer
{
    private const int TemplateWidth = 111;
    private const int TemplateHeight = 14;
    private const int ExpectedX = 213;
    private const int ExpectedY = 50;
    private const int SearchRadius = 2;
    private const double MinimumCoverage = 0.78;
    private const double MinimumDice = 0.78;
    private const string PackedTemplate =
        "AQEAAABAAAABAQAAAPD/AAAAADgAgP8AAAAA+OFgEIQIPgDA4WAARAT8OPwc558/" +
        "w+E4/PD3538e/47zj49hcB7//Pv5P49hxzmOgzk4j2HGHYwf7vDjHMfBDRzu8" +
        "OMOxw9nmHGO48AHDmeYcQfj//HPP8dx4AH/8c+/g///8MOf43DggP/ww9+Bvx9" +
        "gQIQgIDCAH2AARAAIAAAAAAAAGAAAAAAAAAAAAAAAAAAGAAAAAAAAAAAAAAAAAAM" +
        "AAAAAAAAA";
    private static readonly byte[] Template =
        DecodeTemplate();
    private static readonly int TemplateInk =
        Template.Count(value => value != 0);

    public static double Score(ImageFrame image)
    {
        double bestCoverage = 0;
        double bestDice = 0;
        for (int offsetY = -SearchRadius;
             offsetY <= SearchRadius;
             offsetY++)
        {
            for (int offsetX = -SearchRadius;
                 offsetX <= SearchRadius;
                 offsetX++)
            {
                (double Coverage, double Dice) candidate =
                    Compare(
                        image,
                        ExpectedX + offsetX,
                        ExpectedY + offsetY);
                if (candidate.Dice > bestDice)
                {
                    bestCoverage = candidate.Coverage;
                    bestDice = candidate.Dice;
                }
            }
        }

        if (bestCoverage < MinimumCoverage ||
            bestDice < MinimumDice)
        {
            return 0;
        }
        return Math.Clamp(
            0.78 +
            0.11 * Ramp(
                bestCoverage,
                MinimumCoverage,
                0.98) +
            0.11 * Ramp(
                bestDice,
                MinimumDice,
                0.96),
            0,
            1);
    }

    private static (double Coverage, double Dice)
        Compare(
        ImageFrame image,
        int left,
        int top)
    {
        int intersection = 0;
        int liveInk = 0;
        for (int y = 0; y < TemplateHeight; y++)
        {
            for (int x = 0; x < TemplateWidth; x++)
            {
                int pixel =
                    ((top + y) * image.Width +
                     left + x) * 3;
                bool live = IsTextInk(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2]);
                bool expected =
                    Template[y * TemplateWidth + x] != 0;
                if (live)
                {
                    liveInk++;
                }
                if (live && expected)
                {
                    intersection++;
                }
            }
        }
        return (
            intersection / (double)TemplateInk,
            2d * intersection /
            Math.Max(1, TemplateInk + liveInk));
    }

    private static bool IsTextInk(
        byte red,
        byte green,
        byte blue)
    {
        int maximum = Math.Max(
            red,
            Math.Max(green, blue));
        int minimum = Math.Min(
            red,
            Math.Min(green, blue));
        return maximum > 210 &&
            maximum - minimum > 40;
    }

    private static byte[] DecodeTemplate()
    {
        byte[] packed = Convert.FromBase64String(
            PackedTemplate);
        byte[] pixels =
            new byte[TemplateWidth * TemplateHeight];
        for (int bit = 0; bit < pixels.Length; bit++)
        {
            if ((packed[bit / 8] &
                 (1 << (bit % 8))) != 0)
            {
                pixels[bit] = 255;
            }
        }
        return pixels;
    }

    private static double Ramp(
        double value,
        double low,
        double high) =>
        Math.Clamp(
            (value - low) /
            Math.Max(0.0001, high - low),
            0,
            1);
}
