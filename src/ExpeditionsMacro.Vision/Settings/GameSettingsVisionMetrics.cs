using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Settings;

internal readonly record struct SettingsColorComponent(
    int MinimumX,
    int MinimumY,
    int MaximumX,
    int MaximumY,
    int Count,
    double CenterX,
    double CenterY)
{
    public int Width => MaximumX - MinimumX + 1;

    public int Height => MaximumY - MinimumY + 1;
}

internal static class GameSettingsVisionMetrics
{
    public static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
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

    public static IReadOnlyList<SettingsColorComponent>
        FindComponents(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
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

        List<SettingsColorComponent> components = [];
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
            long sumX = 0;
            long sumY = 0;
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
                sumX += x + region.X;
                sumY += y + region.Y;
                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
            }

            components.Add(
                new SettingsColorComponent(
                    minimumX + region.X,
                    minimumY + region.Y,
                    maximumX + region.X,
                    maximumY + region.Y,
                    count,
                    (double)sumX / count,
                    (double)sumY / count));

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

    public static (int StartY, int EndY)
        LongestVerticalRun(
        ImageFrame image,
        int x,
        int minimumY,
        int maximumY,
        Func<byte, byte, byte, bool> predicate,
        int maximumGap = 0)
    {
        int bestStart = 0;
        int bestEnd = -1;
        int currentStart = -1;
        int lastMatch = -1;
        int gap = 0;
        for (int y = minimumY; y <= maximumY; y++)
        {
            int pixel = (y * image.Width + x) * 3;
            bool matches = predicate(
                image.Pixels[pixel],
                image.Pixels[pixel + 1],
                image.Pixels[pixel + 2]);
            if (matches)
            {
                if (currentStart < 0)
                {
                    currentStart = y;
                }

                lastMatch = y;
                gap = 0;
                continue;
            }

            if (currentStart < 0)
            {
                continue;
            }

            gap++;
            if (gap <= maximumGap)
            {
                continue;
            }

            if (lastMatch - currentStart >
                bestEnd - bestStart)
            {
                bestStart = currentStart;
                bestEnd = lastMatch;
            }

            currentStart = -1;
            lastMatch = -1;
            gap = 0;
        }

        if (currentStart >= 0 &&
            lastMatch - currentStart >
            bestEnd - bestStart)
        {
            bestStart = currentStart;
            bestEnd = lastMatch;
        }

        return (bestStart, bestEnd);
    }

    public static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        red + green + blue <= 210;

    public static bool IsCyan(
        byte red,
        byte green,
        byte blue) =>
        green >= 95 &&
        blue >= 90 &&
        green - red >= 30 &&
        blue - red >= 30;

    public static bool IsGreen(
        byte red,
        byte green,
        byte blue) =>
        green >= 90 &&
        green - red >= 20 &&
        green - blue >= 20;

    public static bool IsRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 140 &&
        red - green >= 65 &&
        red - blue >= 50 &&
        green <= 100;

    public static bool IsScrollbarBlue(
        byte red,
        byte green,
        byte blue) =>
        red <= 25 &&
        green is >= 45 and <= 175 &&
        blue is >= 80 and <= 245 &&
        blue - green >= 25;

    public static bool IsNeutralTabSurface(
        byte red,
        byte green,
        byte blue)
    {
        int maximum =
            Math.Max(red, Math.Max(green, blue));
        int minimum =
            Math.Min(red, Math.Min(green, blue));
        return maximum - minimum <= 8 &&
            minimum >= 30 &&
            maximum <= 105;
    }

    public static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) / (maximum - minimum),
            0,
            1);
}
