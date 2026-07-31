using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Bounties;

internal readonly record struct BountyBoardEventEntryMatch(
    double Confidence,
    int ActionX,
    int ActionY);

internal static class BountyBoardEventEntryDetector
{
    private static readonly ScreenRegion EventHeader =
        new(0, 55, 180, 55);
    private const int FirstEntryTop = 108;
    private const int EntrySpacing = 51;
    private const int CandidateCount = 9;

    public static BountyBoardEventEntryMatch? Find(
        ImageFrame image)
    {
        double headerNeutral = ColorFraction(
            image,
            EventHeader,
            IsNeutral);
        double headerDark = ColorFraction(
            image,
            EventHeader,
            IsDark);
        if (headerNeutral < 0.012 ||
            headerDark < 0.55)
        {
            return null;
        }
        double ownerScore = Math.Clamp(
            0.78 +
            0.11 * Ramp(
                headerNeutral,
                0.012,
                0.025) +
            0.11 * Ramp(
                headerDark,
                0.55,
                0.82),
            0,
            1);
        BountyBoardEventEntryMatch? best = null;
        for (int index = 0;
             index < CandidateCount;
             index++)
        {
            int top = FirstEntryTop +
                index * EntrySpacing;
            double score = ScoreAt(
                image,
                top);
            if (score == 0 ||
                best is not null &&
                best.Value.Confidence >= score)
            {
                continue;
            }
            best = new(
                Math.Min(
                    score,
                    ownerScore),
                ActionX: 92,
                ActionY: top + 24);
        }
        return best;
    }

    private static double ScoreAt(
        ImageFrame image,
        int top)
    {
        ScreenRegion rail = new(
            13,
            top,
            4,
            44);
        ScreenRegion body = new(
            17,
            top,
            11,
            44);
        ScreenRegion row = new(
            10,
            top - 2,
            168,
            52);
        double railCopper = ColorFraction(
            image,
            rail,
            IsBountyCopper);
        double bodyCopper = ColorFraction(
            image,
            body,
            IsBountyCopper);
        double bodyDark = ColorFraction(
            image,
            body,
            IsDark);
        double rowCopper = ColorFraction(
            image,
            row,
            IsBountyCopper);
        double rowNeutral = ColorFraction(
            image,
            row,
            IsNeutral);

        double unselected = 0;
        if (railCopper >= 0.50 &&
            bodyDark >= 0.80 &&
            rowNeutral >= 0.020)
        {
            unselected = Math.Clamp(
                0.78 +
                0.08 * Ramp(
                    railCopper,
                    0.50,
                    0.68) +
                0.07 * Ramp(
                    bodyDark,
                    0.80,
                    0.96) +
                0.07 * Ramp(
                    rowNeutral,
                    0.020,
                    0.065),
                0,
                1);
        }

        double highlighted = 0;
        if (railCopper >= 0.20 &&
            bodyCopper >= 0.08 &&
            rowCopper >= 0.08 &&
            rowNeutral >= 0.025)
        {
            highlighted = Math.Clamp(
                0.78 +
                0.07 * Ramp(
                    railCopper,
                    0.20,
                    0.58) +
                0.06 * Ramp(
                    bodyCopper,
                    0.08,
                    0.48) +
                0.05 * Ramp(
                    rowCopper,
                    0.08,
                    0.32) +
                0.04 * Ramp(
                    rowNeutral,
                    0.025,
                    0.10),
                0,
                1);
        }
        return Math.Max(
            unselected,
            highlighted);
    }

    private static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        int matches = 0;
        for (int y = region.Y;
             y < region.Bottom;
             y++)
        {
            for (int x = region.X;
                 x < region.Right;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matches++;
                }
            }
        }
        return (double)matches /
            (region.Width * region.Height);
    }

    private static bool IsBountyCopper(
        byte red,
        byte green,
        byte blue) =>
        red is >= 70 and <= 175 &&
        green is >= 35 and <= 110 &&
        blue is >= 18 and <= 90 &&
        red > green * 1.30 &&
        green > blue * 1.20;

    private static bool IsNeutral(
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
        return minimum > 140 &&
            maximum - minimum < 60;
    }

    private static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        (red + green + blue) / 3 < 55;

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
