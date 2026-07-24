using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Refuel;

internal static class RefuelVisionMetrics
{
    public static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        if (!region.FitsWithin(image.Width, image.Height)) return 0;
        int matching = 0;
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
                    matching++;
                }
            }
        }

        return (double)matching /
            (region.Width * region.Height);
    }

    public static double BestHorizontalLineFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        if (!region.FitsWithin(image.Width, image.Height)) return 0;
        double best = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            int matching = 0;
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matching++;
                }
            }

            best = Math.Max(
                best,
                (double)matching / region.Width);
        }

        return best;
    }

    public static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) / (maximum - minimum),
            0,
            1);

    public static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        red + green + blue <= 180;

    public static bool IsRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 115 &&
        red - green >= 45 &&
        red - blue >= 35;

    public static bool IsTeal(
        byte red,
        byte green,
        byte blue) =>
        green >= 75 &&
        blue >= 65 &&
        green - red >= 20;

    public static bool IsPurple(
        byte red,
        byte green,
        byte blue) =>
        red >= 75 &&
        blue >= 85 &&
        blue - green >= 18;

    public static bool IsGold(
        byte red,
        byte green,
        byte blue) =>
        red >= 115 &&
        green >= 80 &&
        blue <= 80 &&
        red - blue >= 45;

    public static bool IsBlue(
        byte red,
        byte green,
        byte blue) =>
        blue >= 90 &&
        blue - red >= 30 &&
        blue - green >= 10;

    public static bool IsGreen(
        byte red,
        byte green,
        byte blue) =>
        green >= 115 &&
        green - red >= 25 &&
        green - blue >= 35;

    public static bool IsOrange(
        byte red,
        byte green,
        byte blue) =>
        red >= 140 &&
        green >= 70 &&
        blue <= 65 &&
        red - blue >= 70;

    public static bool IsBrightNeutral(
        byte red,
        byte green,
        byte blue)
    {
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        return minimum >= 165 && maximum - minimum <= 55;
    }

    public static bool IsNeutralGray(
        byte red,
        byte green,
        byte blue)
    {
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        return maximum - minimum <= 35 &&
            maximum is >= 35 and <= 190;
    }

    public static void ValidateClient(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new InvalidDataException(
                "Resource-refuel detector input must be an RGB 808 by 611 Roblox client image.");
        }
    }
}
