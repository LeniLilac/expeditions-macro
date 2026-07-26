using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Events;

internal readonly record struct EventActAnchorMatch(
    double Confidence,
    int ActionX,
    int ActionY);

internal static class EventActAnchorDetector
{
    private const int SearchLeft = 180;
    private const int SearchRight = 808;

    private readonly record struct ColoredComponent(
        int Count,
        int Left,
        int Top,
        int Width,
        int Height);

    public static EventActAnchorMatch? Find(
        ImageFrame image,
        EventAct act)
    {
        ValidateClient(image);
        (
            int top,
            int bottom,
            int minimumCount,
            int targetCount) = SearchProfile(act);
        IReadOnlyList<ColoredComponent> components =
            Components(
                image,
                top,
                bottom,
                Predicate(act));
        ColoredComponent? best = components
            .Where(component =>
                component.Count >= minimumCount &&
                component.Width is >= 8 and <= 40 &&
                component.Height is >= 8 and <= 40)
            .OrderByDescending(component =>
                component.Count)
            .FirstOrDefault();
        if (best is not ColoredComponent anchor ||
            anchor.Count < minimumCount)
        {
            VisionTrace.Emit(
                "event_act_anchor",
                "None",
                0,
                new { Act = act.ToString() });
            return null;
        }

        double centerX =
            anchor.Left + (anchor.Width - 1) / 2d;
        double centerY =
            anchor.Top + (anchor.Height - 1) / 2d;
        int actionX = Math.Clamp(
            (int)Math.Round(
                centerX - 76,
                MidpointRounding.AwayFromZero),
            205,
            745);
        int actionY = Math.Clamp(
            (int)Math.Round(
                centerY + 82,
                MidpointRounding.AwayFromZero),
            185,
            500);
        double confidence =
            0.76 +
            0.24 * Ramp(
                anchor.Count,
                minimumCount,
                targetCount);
        EventActAnchorMatch match =
            new(
                confidence,
                actionX,
                actionY);
        VisionTrace.Emit(
            "event_act_anchor",
            act.ToString(),
            match.Confidence,
            new
            {
                AnchorX = centerX,
                AnchorY = centerY,
                anchor.Count,
                match.ActionX,
                match.ActionY,
            });
        return match;
    }

    private static (
        int Top,
        int Bottom,
        int MinimumCount,
        int TargetCount) SearchProfile(
        EventAct act) => act switch
        {
            EventAct.Act1 => (240, 330, 80, 170),
            EventAct.Act2 => (140, 250, 100, 200),
            EventAct.Act3 => (285, 380, 120, 300),
            EventAct.Act4 => (180, 310, 100, 220),
            _ => throw new ArgumentOutOfRangeException(
                nameof(act)),
        };

    private static Func<byte, byte, byte, bool>
        Predicate(EventAct act) => act switch
        {
            EventAct.Act1 => IsPurple,
            EventAct.Act2 => IsGreen,
            EventAct.Act3 => IsCyan,
            EventAct.Act4 => IsYellow,
            _ => throw new ArgumentOutOfRangeException(
                nameof(act)),
        };

    private static IReadOnlyList<ColoredComponent>
        Components(
        ImageFrame image,
        int top,
        int bottom,
        Func<byte, byte, byte, bool> predicate)
    {
        int width = SearchRight - SearchLeft;
        int height = bottom - top;
        bool[] mask = new bool[width * height];
        bool[] visited = new bool[mask.Length];
        int[] queue = new int[mask.Length];
        for (int localY = 0;
             localY < height;
             localY++)
        {
            int y = top + localY;
            for (int localX = 0;
                 localX < width;
                 localX++)
            {
                int x = SearchLeft + localX;
                int pixel =
                    (y * image.Width + x) * 3;
                mask[localY * width + localX] =
                    predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]);
            }
        }

        List<ColoredComponent> components = [];
        for (int start = 0;
             start < mask.Length;
             start++)
        {
            if (!mask[start] || visited[start])
            {
                continue;
            }

            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            visited[start] = true;
            int count = 0;
            int minimumX = width;
            int minimumY = height;
            int maximumX = 0;
            int maximumY = 0;
            while (head < tail)
            {
                int current = queue[head++];
                int x = current % width;
                int y = current / width;
                count++;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                for (int offsetY = -1;
                     offsetY <= 1;
                     offsetY++)
                {
                    for (int offsetX = -1;
                         offsetX <= 1;
                         offsetX++)
                    {
                        if (offsetX == 0 &&
                            offsetY == 0)
                        {
                            continue;
                        }
                        int nextX = x + offsetX;
                        int nextY = y + offsetY;
                        if (nextX < 0 ||
                            nextX >= width ||
                            nextY < 0 ||
                            nextY >= height)
                        {
                            continue;
                        }
                        int next =
                            nextY * width + nextX;
                        if (!mask[next] ||
                            visited[next])
                        {
                            continue;
                        }
                        visited[next] = true;
                        queue[tail++] = next;
                    }
                }
            }

            components.Add(
                new ColoredComponent(
                    count,
                    SearchLeft + minimumX,
                    top + minimumY,
                    maximumX - minimumX + 1,
                    maximumY - minimumY + 1));
        }
        return components;
    }

    private static bool IsPurple(
        byte red,
        byte green,
        byte blue) =>
        red >= 90 &&
        blue >= 110 &&
        red - green >= 30 &&
        blue - green >= 45;

    private static bool IsGreen(
        byte red,
        byte green,
        byte blue) =>
        green >= 100 &&
        green - red >= 40 &&
        green - blue >= 25;

    private static bool IsCyan(
        byte red,
        byte green,
        byte blue) =>
        green >= 85 &&
        blue >= 110 &&
        green - red >= 28 &&
        blue - red >= 45;

    private static bool IsYellow(
        byte red,
        byte green,
        byte blue) =>
        red >= 115 &&
        green >= 95 &&
        red - blue >= 45 &&
        green - blue >= 35;

    private static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);

    private static void ValidateClient(
        ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new InvalidDataException(
                "Event act-anchor input must be an RGB 808 by 611 client image.");
        }
    }
}
