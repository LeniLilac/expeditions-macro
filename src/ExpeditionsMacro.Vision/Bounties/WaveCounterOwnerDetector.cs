using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public readonly record struct WaveCounterOwnerMatch(
    ScreenRegion CounterRegion);

public static class WaveCounterOwnerDetector
{
    private const int MinimumBlueBadgePixels = 24;
    private const int MinimumDarkRailPixels = 128;
    private const int MinimumLabelPixels = 12;

    private static readonly ScreenRegion[] CounterRegions =
    [
        new(389, 48, 16, 11),
        new(421, 28, 16, 11),
    ];

    private static readonly ScreenRegion[] CounterOwnershipRegions =
    [
        new(372, 43, 44, 22),
        new(404, 23, 44, 22),
    ];

    public static WaveCounterOwnerMatch? Detect(
        ImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        int ownedIndex = -1;
        CounterOwnerMetrics[] metrics =
            new CounterOwnerMetrics[
                CounterRegions.Length];
        for (int index = 0;
             index < CounterRegions.Length;
             index++)
        {
            metrics[index] = MeasureOwner(
                image,
                CounterRegions[index],
                CounterOwnershipRegions[index]);
            if (!metrics[index].Owned)
            {
                continue;
            }
            if (ownedIndex >= 0)
            {
                VisionTrace.Emit(
                    "bounty_wave_counter_owner",
                    "ambiguous",
                    0,
                    new { metrics });
                return null;
            }
            ownedIndex = index;
        }
        VisionTrace.Emit(
            "bounty_wave_counter_owner",
            ownedIndex >= 0
                ? $"{CounterRegions[ownedIndex].X}," +
                  $"{CounterRegions[ownedIndex].Y}"
                : "none",
            ownedIndex >= 0 ? 1 : 0,
            new { metrics });
        return ownedIndex >= 0
            ? new WaveCounterOwnerMatch(
                CounterRegions[ownedIndex])
            : null;
    }

    private static CounterOwnerMetrics MeasureOwner(
        ImageFrame image,
        ScreenRegion counter,
        ScreenRegion ownership)
    {
        int blueBadgePixels = 0;
        for (int y = counter.Y - 2;
             y < counter.Y + 15;
             y++)
        {
            for (int x = ownership.X;
                 x <= counter.X;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                int red = image.Pixels[pixel];
                int green = image.Pixels[pixel + 1];
                int blue = image.Pixels[pixel + 2];
                if (blue > 70 &&
                    blue > red + 30 &&
                    blue > green + 15)
                {
                    blueBadgePixels++;
                }
            }
        }

        int darkTopPixels = CountDarkPixels(
            image,
            new ScreenRegion(
                counter.X - 3,
                counter.Y - 3,
                30,
                5));
        int darkBottomPixels = CountDarkPixels(
            image,
            new ScreenRegion(
                counter.X - 3,
                counter.Y + 9,
                30,
                5));
        int labelPixels = CountLabelPixels(
            image,
            counter);
        bool owned =
            blueBadgePixels >=
                MinimumBlueBadgePixels &&
            darkTopPixels >=
                MinimumDarkRailPixels &&
            darkBottomPixels >=
                MinimumDarkRailPixels &&
            labelPixels >= MinimumLabelPixels;
        return new CounterOwnerMetrics(
            owned,
            counter.X,
            counter.Y,
            blueBadgePixels,
            darkTopPixels,
            darkBottomPixels,
            labelPixels);
    }

    private static int CountLabelPixels(
        ImageFrame image,
        ScreenRegion counter)
    {
        int count = 0;
        for (int y = counter.Y + 4;
             y < counter.Y + 9;
             y++)
        {
            for (int x = counter.X + 16;
                 x < counter.X + 27;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                int red = image.Pixels[pixel];
                int green = image.Pixels[pixel + 1];
                int blue = image.Pixels[pixel + 2];
                int maximum = Math.Max(
                    red,
                    Math.Max(green, blue));
                int minimum = Math.Min(
                    red,
                    Math.Min(green, blue));
                int luminance =
                    (299 * red +
                     587 * green +
                     114 * blue) /
                    1000;
                if (luminance > 65 &&
                    maximum - minimum < 55)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static int CountDarkPixels(
        ImageFrame image,
        ScreenRegion region)
    {
        int count = 0;
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
                int luminance =
                    (299 * image.Pixels[pixel] +
                     587 * image.Pixels[pixel + 1] +
                     114 * image.Pixels[pixel + 2]) /
                    1000;
                if (luminance < 80)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private readonly record struct CounterOwnerMetrics(
        bool Owned,
        int CounterX,
        int CounterY,
        int BlueBadgePixels,
        int DarkTopPixels,
        int DarkBottomPixels,
        int LabelPixels);
}
