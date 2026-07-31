using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Events;

internal static class EventEntryDetector
{
    private static readonly ScreenRegion EventHeader =
        new(0, 55, 180, 55);
    private static readonly ScreenRegion VillainCard =
        new(10, 155, 170, 58);
    private static readonly ScreenRegion EventHomeAction =
        new(430, 548, 135, 42);
    private static readonly ScreenRegion EventHomeActionSeparator =
        new(435, 550, 126, 5);
    private static readonly ScreenRegion EventHomeActionTopBorder =
        new(435, 555, 126, 2);
    private static readonly ScreenRegion EventHomeActionLabel =
        new(455, 566, 90, 11);
    private static readonly ScreenRegion EventHomeActionBottomBorderBand =
        new(435, 582, 126, 4);

    public static double HomeScore(
        ImageFrame image)
    {
        double selectedVillainTab = Math.Max(
            ColoredTabScore(
                image,
                top: 109,
                selected: true,
                colorPredicate: IsEventRed),
            ColoredTabScore(
                image,
                top: 160,
                selected: true,
                colorPredicate: IsEventRed));
        double homeAction =
            HomeActionScore(image);
        if (selectedVillainTab == 0 ||
            homeAction == 0)
        {
            return 0;
        }
        return Math.Clamp(
            0.72 +
            0.14 * homeAction +
            0.14 * selectedVillainTab,
            0,
            1);
    }

    private static double HomeActionScore(
        ImageFrame image)
    {
        // The Act carousel crosses this fixed rectangle in red. Only the
        // isolated button borders and neutral label authorize its action.
        double actionRed = ColorFraction(
            image,
            EventHomeAction,
            IsEventRed);
        double separatorRed = ColorFraction(
            image,
            EventHomeActionSeparator,
            IsEventRed);
        double topBorderRed = BestHorizontalLineFraction(
            image,
            EventHomeActionTopBorder,
            IsEventRed);
        double labelWhite = ColorFraction(
            image,
            EventHomeActionLabel,
            IsNeutralWhite);
        double bottomBorderRed = BestHorizontalLineFraction(
            image,
            EventHomeActionBottomBorderBand,
            IsEventRed);
        if (actionRed < 0.55 ||
            separatorRed > 0.20 ||
            topBorderRed < 0.75 ||
            labelWhite < 0.05 ||
            bottomBorderRed < 0.75)
        {
            return 0;
        }

        double borderShape = (
            Ramp(topBorderRed, 0.75, 0.95) +
            Ramp(bottomBorderRed, 0.75, 0.95) +
            Ramp(0.20 - separatorRed, 0, 0.20)) /
            3;
        return Math.Clamp(
            0.66 +
            0.12 * Ramp(
                actionRed,
                0.55,
                0.82) +
            0.12 * Ramp(
                labelWhite,
                0.05,
                0.18) +
            0.10 * borderShape,
            0,
            1);
    }

    public static double CatalogScore(
        ImageFrame image)
    {
        double cyanHeader = ColorFraction(
            image,
            EventHeader,
            IsEventCyan);
        double villainDark = ColorFraction(
            image,
            VillainCard,
            IsDark);
        double selectedStarterTab =
            ColoredTabScore(
                image,
                top: 109,
                selected: true,
                colorPredicate: IsEventCyan);
        double unselectedVillainTab =
            ColoredTabScore(
                image,
                top: 160,
                selected: false,
                colorPredicate: IsEventRed);
        if (cyanHeader < 0.20 ||
            selectedStarterTab == 0 ||
            unselectedVillainTab == 0 ||
            villainDark < 0.75)
        {
            return 0;
        }
        return Math.Clamp(
            0.72 +
            0.10 * Ramp(
                cyanHeader,
                0.20,
                0.42) +
            0.06 * selectedStarterTab +
            0.06 * unselectedVillainTab +
            0.08 * Ramp(
                villainDark,
                0.75,
                0.92),
            0,
            1);
    }

    private static double ColoredTabScore(
        ImageFrame image,
        int top,
        bool selected,
        Func<byte, byte, byte, bool>
            colorPredicate)
    {
        double railColor = ColorFraction(
            image,
            new ScreenRegion(
                13,
                top,
                4,
                44),
            colorPredicate);
        double bodyColor = ColorFraction(
            image,
            new ScreenRegion(
                17,
                top,
                11,
                44),
            colorPredicate);
        double chevronWhite = ColorFraction(
            image,
            new ScreenRegion(
                18,
                top + 14,
                6,
                18),
            IsNeutralWhite);
        if (railColor < 0.55)
        {
            return 0;
        }

        if (selected)
        {
            if (bodyColor < 0.55 ||
                chevronWhite < 0.025)
            {
                return 0;
            }
            return Math.Clamp(
                0.72 +
                0.08 * Ramp(
                    railColor,
                    0.55,
                    0.80) +
                0.12 * Ramp(
                    bodyColor,
                    0.55,
                    0.90) +
                0.08 * Ramp(
                    chevronWhite,
                    0.025,
                    0.07),
                0,
                1);
        }

        if (bodyColor > 0.12 ||
            chevronWhite > 0.015)
        {
            return 0;
        }
        return Math.Clamp(
            0.72 +
            0.12 * Ramp(
                railColor,
                0.55,
                0.80) +
            0.10 * Ramp(
                0.12 - bodyColor,
                0,
                0.12) +
            0.06 * Ramp(
                0.015 - chevronWhite,
                0,
                0.015),
            0,
            1);
    }

    private static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        int matches = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
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

    private static double BestHorizontalLineFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        double best = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            best = Math.Max(
                best,
                ColorFraction(
                    image,
                    new ScreenRegion(
                        region.X,
                        y,
                        region.Width,
                        1),
                    predicate));
        }
        return best;
    }

    private static bool IsEventRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 95 &&
        red - green >= 38 &&
        red - blue >= 25;

    private static bool IsEventCyan(
        byte red,
        byte green,
        byte blue) =>
        blue >= 100 &&
        green >= 70 &&
        blue - red >= 30;

    private static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        red + green + blue <= 175;

    private static bool IsNeutralWhite(
        byte red,
        byte green,
        byte blue) =>
        red >= 180 &&
        green >= 180 &&
        blue >= 180 &&
        Math.Max(
            red,
            Math.Max(
                green,
                blue)) -
        Math.Min(
            red,
            Math.Min(
                green,
                blue)) <= 35;

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
