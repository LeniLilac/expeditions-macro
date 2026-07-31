using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Events;

internal static class EventActSelectorHeadingDetector
{
    private static readonly ScreenRegion Heading =
        new(380, 20, 225, 101);

    public static double Score(ImageFrame image)
    {
        int runStart = -1;
        double runSum = 0;
        double runPeak = 0;
        double best = 0;
        for (int y = Heading.Y;
             y <= Heading.Bottom;
             y++)
        {
            double red = y < Heading.Bottom
                ? RowFraction(
                    image,
                    y,
                    IsEventRed)
                : 0;
            if (red >= 0.08)
            {
                runStart = runStart < 0
                    ? y
                    : runStart;
                runSum += red;
                runPeak = Math.Max(runPeak, red);
                continue;
            }

            if (runStart >= 0)
            {
                int runEnd = y - 1;
                int length = runEnd - runStart + 1;
                if (length is >= 10 and <= 30 &&
                    runSum / length >= 0.28 &&
                    runPeak >= 0.45)
                {
                    double white = BestSubtitleRow(
                        image,
                        runEnd);
                    if (white >= 0.08)
                    {
                        best = Math.Max(
                            best,
                            (Ramp(
                                runPeak,
                                0.45,
                                0.60) +
                             Ramp(
                                 white,
                                 0.08,
                                 0.20)) /
                            2);
                    }
                }
                runStart = -1;
                runSum = 0;
                runPeak = 0;
            }
        }
        return best;
    }

    private static double BestSubtitleRow(
        ImageFrame image,
        int titleBottom)
    {
        int top = titleBottom + 8;
        int bottom = Math.Min(
            titleBottom + 19,
            Heading.Bottom);
        if (top >= bottom)
        {
            return 0;
        }

        double best = 0;
        for (int y = top; y < bottom; y++)
        {
            best = Math.Max(
                best,
                RowFraction(
                    image,
                    y,
                    IsNeutralWhite));
        }
        return best;
    }

    private static double RowFraction(
        ImageFrame image,
        int y,
        Func<byte, byte, byte, bool> predicate)
    {
        int matches = 0;
        for (int x = Heading.X;
             x < Heading.Right;
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
        return (double)matches / Heading.Width;
    }

    private static bool IsEventRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 95 &&
        red - green >= 38 &&
        red - blue >= 25;

    private static bool IsNeutralWhite(
        byte red,
        byte green,
        byte blue) =>
        Math.Min(red, Math.Min(green, blue)) >= 170 &&
        Math.Max(red, Math.Max(green, blue)) -
        Math.Min(red, Math.Min(green, blue)) <= 45;

    private static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);
}
