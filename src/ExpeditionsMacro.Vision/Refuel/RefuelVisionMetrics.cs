using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Refuel;

internal readonly record struct RefuelColorBounds(
    int MinimumX,
    int MinimumY,
    int MaximumX,
    int MaximumY,
    int Count)
{
    public int Width => MaximumX - MinimumX + 1;

    public int Height => MaximumY - MinimumY + 1;

    public double CenterX => (MinimumX + MaximumX) / 2d;
}

internal readonly record struct RefuelColorComponent(
    int MinimumX,
    int MinimumY,
    int MaximumX,
    int MaximumY,
    int Count)
{
    public int Width => MaximumX - MinimumX + 1;

    public int Height => MaximumY - MinimumY + 1;

    public double CenterX => (MinimumX + MaximumX) / 2d;

    public double CenterY => (MinimumY + MaximumY) / 2d;
}

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

    public static RefuelColorBounds? FindColorBounds(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        if (!region.FitsWithin(image.Width, image.Height))
        {
            return null;
        }

        int minimumX = region.Right;
        int minimumY = region.Bottom;
        int maximumX = region.X - 1;
        int maximumY = region.Y - 1;
        int count = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (!predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    continue;
                }

                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                count++;
            }
        }

        return count == 0
            ? null
            : new RefuelColorBounds(
                minimumX,
                minimumY,
                maximumX,
                maximumY,
                count);
    }

    public static IReadOnlyList<RefuelColorComponent>
        FindComponents(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        if (!region.FitsWithin(image.Width, image.Height))
        {
            return [];
        }

        bool[] matches =
            new bool[region.Width * region.Height];
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                int pixel =
                    ((region.Y + y) * image.Width +
                     region.X + x) * 3;
                matches[y * region.Width + x] = predicate(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2]);
            }
        }

        List<RefuelColorComponent> components = [];
        Queue<int> queue = new();
        for (int index = 0; index < matches.Length; index++)
        {
            if (!matches[index]) continue;
            matches[index] = false;
            queue.Enqueue(index);
            int minimumX = region.Width;
            int minimumY = region.Height;
            int maximumX = 0;
            int maximumY = 0;
            int count = 0;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % region.Width;
                int y = current / region.Width;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                count++;
                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
            }

            components.Add(
                new RefuelColorComponent(
                    minimumX + region.X,
                    minimumY + region.Y,
                    maximumX + region.X,
                    maximumY + region.Y,
                    count));

            void Visit(int x, int y)
            {
                if (x < 0 ||
                    y < 0 ||
                    x >= region.Width ||
                    y >= region.Height)
                {
                    return;
                }

                int neighbor = y * region.Width + x;
                if (!matches[neighbor]) return;
                matches[neighbor] = false;
                queue.Enqueue(neighbor);
            }
        }

        return components;
    }

    public static RefuelColorComponent? FindComponent(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> color,
        Func<RefuelColorComponent, bool> accept)
    {
        RefuelColorComponent[] candidates =
            FindComponents(image, region, color)
                .Where(accept)
                .OrderByDescending(
                    component => component.Count)
                .ToArray();
        return candidates.Length == 0
            ? null
            : candidates[0];
    }

    public static double BestHorizontalBandFraction(
        ImageFrame image,
        ScreenRegion region,
        int bandHeight,
        Func<byte, byte, byte, bool> predicate)
    {
        if (!region.FitsWithin(image.Width, image.Height) ||
            bandHeight <= 0 ||
            bandHeight > region.Height)
        {
            return 0;
        }

        double[] rows = new double[region.Height];
        for (int y = 0; y < region.Height; y++)
        {
            int matching = 0;
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel =
                    ((region.Y + y) * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matching++;
                }
            }

            rows[y] = (double)matching / region.Width;
        }

        double best = 0;
        for (int start = 0;
             start <= region.Height - bandHeight;
             start++)
        {
            double total = 0;
            for (int row = 0; row < bandHeight; row++)
            {
                total += rows[start + row];
            }

            best = Math.Max(best, total / bandHeight);
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

    public static bool IsStationStatBar(
        byte red,
        byte green,
        byte blue)
    {
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        return maximum >= 100 &&
            maximum - minimum >= 30;
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
