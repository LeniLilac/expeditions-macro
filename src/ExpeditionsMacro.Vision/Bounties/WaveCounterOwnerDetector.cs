using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Bounties;

public readonly record struct WaveCounterOwnerMatch(
    ScreenRegion CounterRegion);

public static class WaveCounterOwnerDetector
{
    private const int MinimumBlueBadgePixels = 24;
    private const int MinimumRailPixels = 128;
    private const int MinimumLabelPixels = 12;
    private const int BaseRailLuminance = 80;
    private const int MaximumRailLuminance = 130;
    private const int MinimumLocalContrast = 15;

    private static readonly ScreenRegion[] CounterRegions =
    [
        new(389, 48, 16, 11),
        new(421, 28, 16, 11),
        new(386, 28, 16, 11),
    ];

    private static readonly ScreenRegion[] CounterOwnershipRegions =
    [
        new(372, 43, 44, 22),
        new(404, 23, 44, 22),
        new(369, 23, 44, 22),
    ];

    private static readonly ScreenRegion[] TopRailRegions =
    [
        new(386, 45, 30, 5),
        new(418, 25, 30, 5),
        new(383, 25, 30, 5),
    ];

    private static readonly ScreenRegion[] TopBackgroundRegions =
    [
        new(386, 40, 30, 5),
        new(418, 20, 30, 5),
        new(383, 20, 30, 5),
    ];

    private static readonly ScreenRegion[] BottomRailRegions =
    [
        new(386, 57, 30, 5),
        new(418, 37, 30, 5),
        new(383, 37, 30, 5),
    ];

    private static readonly ScreenRegion[] BottomBackgroundRegions =
    [
        new(386, 62, 30, 5),
        new(418, 42, 30, 5),
        new(383, 42, 30, 5),
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
                CounterOwnershipRegions[index],
                TopRailRegions[index],
                TopBackgroundRegions[index],
                BottomRailRegions[index],
                BottomBackgroundRegions[index]);
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
        ScreenRegion ownership,
        ScreenRegion topRailRegion,
        ScreenRegion topBackgroundRegion,
        ScreenRegion bottomRailRegion,
        ScreenRegion bottomBackgroundRegion)
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

        RailMetrics topRail = MeasureRail(
            image,
            topRailRegion,
            topBackgroundRegion);
        RailMetrics bottomRail = MeasureRail(
            image,
            bottomRailRegion,
            bottomBackgroundRegion);
        int labelPixels = CountLabelPixels(
            image,
            counter);
        bool owned =
            blueBadgePixels >=
                MinimumBlueBadgePixels &&
            topRail.Pixels >=
                MinimumRailPixels &&
            bottomRail.Pixels >=
                MinimumRailPixels &&
            labelPixels >= MinimumLabelPixels;
        return new CounterOwnerMetrics(
            owned,
            counter.X,
            counter.Y,
            blueBadgePixels,
            topRail.Pixels,
            topRail.Threshold,
            topRail.BackgroundLuminance,
            bottomRail.Pixels,
            bottomRail.Threshold,
            bottomRail.BackgroundLuminance,
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

    private static RailMetrics MeasureRail(
        ImageFrame image,
        ScreenRegion rail,
        ScreenRegion background)
    {
        int backgroundLuminance =
            MaximumRowLuminance(
                image,
                background);
        int threshold = Math.Clamp(
            backgroundLuminance -
                MinimumLocalContrast,
            BaseRailLuminance,
            MaximumRailLuminance);
        int count = 0;
        for (int y = rail.Y;
             y < rail.Bottom;
             y++)
        {
            for (int x = rail.X;
                 x < rail.Right;
                 x++)
            {
                if (Luminance(
                        image,
                        x,
                        y) < threshold)
                {
                    count++;
                }
            }
        }
        return new RailMetrics(
            count,
            threshold,
            backgroundLuminance);
    }

    private static int MaximumRowLuminance(
        ImageFrame image,
        ScreenRegion region)
    {
        int maximum = 0;
        for (int y = region.Y;
             y < region.Bottom;
             y++)
        {
            int row = 0;
            for (int x = region.X;
                 x < region.Right;
                 x++)
            {
                row += Luminance(
                    image,
                    x,
                    y);
            }
            maximum = Math.Max(
                maximum,
                row / region.Width);
        }
        return maximum;
    }

    private static int Luminance(
        ImageFrame image,
        int x,
        int y)
    {
        int pixel =
            (y * image.Width + x) * 3;
        return
            (299 * image.Pixels[pixel] +
             587 * image.Pixels[pixel + 1] +
             114 * image.Pixels[pixel + 2]) /
            1000;
    }

    private readonly record struct CounterOwnerMetrics(
        bool Owned,
        int CounterX,
        int CounterY,
        int BlueBadgePixels,
        int TopRailPixels,
        int TopRailThreshold,
        int TopBackgroundLuminance,
        int BottomRailPixels,
        int BottomRailThreshold,
        int BottomBackgroundLuminance,
        int LabelPixels);

    private readonly record struct RailMetrics(
        int Pixels,
        int Threshold,
        int BackgroundLuminance);
}
