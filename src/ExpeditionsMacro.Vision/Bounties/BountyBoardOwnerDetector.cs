using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Bounties;

internal readonly record struct BountyBoardOwnerMatch(
    double ButtonRailScore,
    double HeaderImageScore,
    double HeaderTextScore)
{
    public bool IsMatch =>
        ButtonRailScore > 0 &&
        Math.Max(
            HeaderImageScore,
            HeaderTextScore) > 0;

    public bool UsedTextFallback =>
        HeaderImageScore <= 0 &&
        HeaderTextScore > 0;

    public double Confidence =>
        !IsMatch
            ? 0
            : Math.Clamp(
                0.76 +
                0.12 * ButtonRailScore +
                0.12 * Math.Max(
                    HeaderImageScore,
                    HeaderTextScore),
                0,
                1);
}

internal static class BountyBoardOwnerDetector
{
    internal static readonly ScreenRegion HeaderRegion =
        new(210, 41, 120, 26);
    internal static readonly ScreenRegion ButtonRailRegion =
        new(8, 569, 175, 39);
    private static readonly ScreenRegion BackInterior =
        new(18, 578, 81, 20);
    private static readonly ScreenRegion CalendarInterior =
        new(111, 578, 62, 20);

    private const double MinimumBackNeutralFraction = 0.78;
    private const double MinimumBackGlyphFraction = 0.025;
    private const double MinimumBrightBackSurfaceFraction = 0.58;
    private const double MinimumDarkBackGlyphFraction = 0.025;
    private const double MinimumCalendarBlueFraction = 0.68;
    private const double MinimumCalendarGlyphFraction = 0.05;
    private const double MinimumHeaderGoldFraction = 0.10;
    private const int MinimumHeaderGoldSpan = 100;
    private const int MinimumHeaderStrongRows = 5;
    private const int MinimumHeaderPixelsPerStrongRow = 40;

    public static BountyBoardOwnerMatch Detect(
        ImageFrame image)
    {
        double rail = ButtonRailScore(image);
        if (rail <= 0)
        {
            return default;
        }

        double imageScore = HeaderImageScore(image);
        double textScore = imageScore > 0
            ? 0
            : BountyBoardHeaderRecognizer.Score(image);
        return new BountyBoardOwnerMatch(
            rail,
            imageScore,
            textScore);
    }

    private static double ButtonRailScore(
        ImageFrame image)
    {
        double backNeutral = ColorFraction(
            image,
            BackInterior,
            IsBackNeutral);
        double backGlyph = ColorFraction(
            image,
            BackInterior,
            IsBrightNeutral);
        double darkBackGlyph = ColorFraction(
            image,
            BackInterior,
            IsDarkNeutral);
        double calendarBlue = ColorFraction(
            image,
            CalendarInterior,
            IsCalendarBlue);
        double calendarGlyph = ColorFraction(
            image,
            CalendarInterior,
            IsBrightNeutral);
        bool legacyBack =
            backNeutral >= MinimumBackNeutralFraction &&
            backGlyph >= MinimumBackGlyphFraction;
        bool brightBack =
            backGlyph >= MinimumBrightBackSurfaceFraction &&
            darkBackGlyph >= MinimumDarkBackGlyphFraction;
        if ((!legacyBack && !brightBack) ||
            calendarBlue < MinimumCalendarBlueFraction ||
            calendarGlyph < MinimumCalendarGlyphFraction)
        {
            return 0;
        }

        double backScore = legacyBack
            ? 0.06 * Ramp(
                  backNeutral,
                  MinimumBackNeutralFraction,
                  0.94) +
              0.04 * Ramp(
                  backGlyph,
                  MinimumBackGlyphFraction,
                  0.07)
            : 0.06 * Ramp(
                  backGlyph,
                  MinimumBrightBackSurfaceFraction,
                  0.76) +
              0.04 * Ramp(
                  darkBackGlyph,
                  MinimumDarkBackGlyphFraction,
                  0.06);
        return Math.Clamp(
            0.78 +
            backScore +
            0.08 * Ramp(
                calendarBlue,
                MinimumCalendarBlueFraction,
                0.84) +
            0.04 * Ramp(
                calendarGlyph,
                MinimumCalendarGlyphFraction,
                0.13),
            0,
            1);
    }

    private static double HeaderImageScore(
        ImageFrame image)
    {
        int gold = 0;
        int minimumX = HeaderRegion.Right;
        int maximumX = HeaderRegion.X;
        int strongRows = 0;
        for (int y = HeaderRegion.Y;
             y < HeaderRegion.Bottom;
             y++)
        {
            int rowGold = 0;
            for (int x = HeaderRegion.X;
                 x < HeaderRegion.Right;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                if (!IsTitleGold(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    continue;
                }
                gold++;
                rowGold++;
                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
            }
            if (rowGold >=
                MinimumHeaderPixelsPerStrongRow)
            {
                strongRows++;
            }
        }

        double fraction = gold /
            (double)(HeaderRegion.Width *
                     HeaderRegion.Height);
        int span = gold == 0
            ? 0
            : maximumX - minimumX + 1;
        if (fraction < MinimumHeaderGoldFraction ||
            span < MinimumHeaderGoldSpan ||
            strongRows < MinimumHeaderStrongRows)
        {
            return 0;
        }

        return Math.Clamp(
            0.78 +
            0.08 * Ramp(
                fraction,
                MinimumHeaderGoldFraction,
                0.18) +
            0.07 * Ramp(
                span,
                MinimumHeaderGoldSpan,
                114) +
            0.07 * Ramp(
                strongRows,
                MinimumHeaderStrongRows,
                7),
            0,
            1);
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
        return matches /
            (double)(region.Width * region.Height);
    }

    private static bool IsBackNeutral(
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
        int average = (red + green + blue) / 3;
        return maximum - minimum < 30 &&
            average is > 35 and < 180;
    }

    private static bool IsCalendarBlue(
        byte red,
        byte green,
        byte blue) =>
        blue > 80 &&
        blue > red * 1.12 &&
        blue > green * 1.02;

    private static bool IsBrightNeutral(
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
        return minimum > 150 &&
            maximum - minimum < 70;
    }

    private static bool IsDarkNeutral(
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
        return maximum < 100 &&
            maximum - minimum < 35;
    }

    private static bool IsTitleGold(
        byte red,
        byte green,
        byte blue) =>
        red > 150 &&
        green > 70 &&
        green < 220 &&
        blue < 100 &&
        red > green * 1.15;

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
