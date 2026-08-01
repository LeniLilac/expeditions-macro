using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Navigation;

public enum MatchLobbyDoorLayout
{
    Unknown,
    NoVoiceChat,
    VoiceChat,
}

public readonly record struct MatchLobbyDoorButtonMatch(
    bool Visible,
    double Confidence,
    int ActionX,
    int ActionY,
    MatchLobbyDoorLayout Layout);

public static class MatchLobbyDoorButtonDetector
{
    private const int VoiceChatOffset = 44;
    private const int DoorMinimumX = 260;
    private const int DoorActionY = 35;
    private static readonly ScreenRegion LocalSearch =
        new(-2, 22, 25, 26);
    private static readonly ScreenRegion LocalContext =
        new(-6, 20, 34, 29);

    public static MatchLobbyDoorButtonMatch Detect(
        ImageFrame image)
    {
        ValidateClient(image);
        MatchLobbyDoorButtonMatch noVoice =
            Evaluate(
                image,
                DoorMinimumX,
                MatchLobbyDoorLayout.NoVoiceChat);
        MatchLobbyDoorButtonMatch voice =
            Evaluate(
                image,
                DoorMinimumX + VoiceChatOffset,
                MatchLobbyDoorLayout.VoiceChat);
        MatchLobbyDoorButtonMatch result =
            voice.Confidence > noVoice.Confidence
                ? voice
                : noVoice;
        VisionTrace.Emit(
            "match_lobby_door",
            result.Visible
                ? result.Layout.ToString()
                : "none",
            result.Confidence,
            new
            {
                result.ActionX,
                result.ActionY,
                layout = result.Layout.ToString(),
            });
        return result;
    }

    private static MatchLobbyDoorButtonMatch Evaluate(
        ImageFrame image,
        int expectedMinimumX,
        MatchLobbyDoorLayout layout)
    {
        IReadOnlyList<WhiteComponent> components =
            FindWhiteComponents(
                image,
                Offset(LocalSearch, expectedMinimumX));
        WhiteComponent door = components
            .Where(component =>
                component.Count >= 60 &&
                component.Width is >= 15 and <= 17 &&
                component.Height is >= 18 and <= 21 &&
                Math.Abs(
                    component.MinimumX -
                    expectedMinimumX) <= 1 &&
                component.MinimumY is >= 24 and <= 26 &&
                component.FillRatio is >= 0.20 and <= 0.55)
            .OrderByDescending(component => component.Count)
            .FirstOrDefault();
        WhiteComponent arrow = components
            .Where(component =>
                component.Count >= 16 &&
                component.Width is >= 7 and <= 9 &&
                component.Height is >= 7 and <= 9 &&
                component.MinimumX - expectedMinimumX
                    is >= 12 and <= 15 &&
                component.MinimumY is >= 37 and <= 40 &&
                component.FillRatio is >= 0.25 and <= 0.70)
            .OrderByDescending(component => component.Count)
            .FirstOrDefault();
        DoorStructure structure =
            MeasureDoorStructure(image, expectedMinimumX);
        if (door.Count == 0 ||
            arrow.Count == 0 ||
            !structure.IsComplete)
        {
            return default;
        }

        (double foreground, double background) =
            MeasureOpaqueContrast(
                image,
                Offset(LocalContext, expectedMinimumX));
        double contrast = foreground - background;
        bool visible =
            foreground >= 180 &&
            contrast >= 100;
        if (!visible) return default;

        double structureScore =
            (structure.TopEdge +
             structure.LeftJamb +
             structure.InnerJamb +
             structure.OuterJamb +
             structure.Handle) / 5;
        double geometry =
            0.35 * structureScore +
            0.25 * Ramp(door.FillRatio, 0.20, 0.35) +
            0.15 * Ramp(arrow.FillRatio, 0.25, 0.40) +
            0.25 * Ramp(contrast, 100, 180);
        return new MatchLobbyDoorButtonMatch(
            true,
            Math.Clamp(geometry, 0, 1),
            expectedMinimumX + 10,
            DoorActionY,
            layout);
    }

    private static DoorStructure MeasureDoorStructure(
        ImageFrame image,
        int expectedMinimumX)
    {
        double topEdge = StrongestHorizontalCoverage(
            image,
            expectedMinimumX - 1,
            expectedMinimumX + 15,
            24,
            27,
            15);
        double leftJamb = StrongestVerticalCoverage(
            image,
            expectedMinimumX - 1,
            expectedMinimumX + 1,
            26,
            41);
        double innerJamb = StrongestVerticalCoverage(
            image,
            expectedMinimumX + 8,
            expectedMinimumX + 11,
            28,
            43);
        double outerJamb = StrongestVerticalCoverage(
            image,
            expectedMinimumX + 13,
            expectedMinimumX + 16,
            25,
            35);
        double handle = StrongestVerticalCoverage(
            image,
            expectedMinimumX + 5,
            expectedMinimumX + 8,
            34,
            38);
        double interiorFill = WhiteFraction(
            image,
            new ScreenRegion(
                expectedMinimumX + 3,
                29,
                5,
                5));
        return new DoorStructure(
            topEdge,
            leftJamb,
            innerJamb,
            outerJamb,
            handle,
            interiorFill);
    }

    private static double StrongestHorizontalCoverage(
        ImageFrame image,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY,
        int sampleWidth)
    {
        int strongest = 0;
        for (int y = minimumY; y <= maximumY; y++)
        {
            for (int x = minimumX;
                 x <= maximumX - sampleWidth + 1;
                 x++)
            {
                int count = 0;
                for (int sampleX = x;
                     sampleX < x + sampleWidth;
                     sampleX++)
                {
                    if (IsOpaqueWhite(image, sampleX, y))
                    {
                        count++;
                    }
                }
                strongest = Math.Max(strongest, count);
            }
        }
        return strongest / (double)sampleWidth;
    }

    private static double StrongestVerticalCoverage(
        ImageFrame image,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY)
    {
        int strongest = 0;
        for (int x = minimumX; x <= maximumX; x++)
        {
            int count = 0;
            for (int y = minimumY; y <= maximumY; y++)
            {
                if (IsOpaqueWhite(image, x, y))
                {
                    count++;
                }
            }
            strongest = Math.Max(strongest, count);
        }
        return strongest /
            (double)(maximumY - minimumY + 1);
    }

    private static double WhiteFraction(
        ImageFrame image,
        ScreenRegion region)
    {
        int count = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                if (IsOpaqueWhite(image, x, y))
                {
                    count++;
                }
            }
        }
        return count / (double)(region.Width * region.Height);
    }

    private static IReadOnlyList<WhiteComponent>
        FindWhiteComponents(
        ImageFrame image,
        ScreenRegion region)
    {
        bool[] matches =
            new bool[region.Width * region.Height];
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                matches[y * region.Width + x] =
                    IsOpaqueWhite(
                        image,
                        region.X + x,
                        region.Y + y);
            }
        }

        List<WhiteComponent> components = [];
        Queue<int> queue = new();
        for (int index = 0;
             index < matches.Length;
             index++)
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
                new WhiteComponent(
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

    private static (double Foreground, double Background)
        MeasureOpaqueContrast(
        ImageFrame image,
        ScreenRegion region)
    {
        double foreground = 0;
        int foregroundCount = 0;
        double background = 0;
        int backgroundCount = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                double luminance =
                    0.2126 * image.Pixels[pixel] +
                    0.7152 * image.Pixels[pixel + 1] +
                    0.0722 * image.Pixels[pixel + 2];
                if (IsOpaqueWhite(image, x, y))
                {
                    foreground += luminance;
                    foregroundCount++;
                }
                else
                {
                    background += luminance;
                    backgroundCount++;
                }
            }
        }
        return (
            foreground / Math.Max(1, foregroundCount),
            background / Math.Max(1, backgroundCount));
    }

    private static bool IsOpaqueWhite(
        ImageFrame image,
        int x,
        int y)
    {
        int pixel = (y * image.Width + x) * 3;
        byte red = image.Pixels[pixel];
        byte green = image.Pixels[pixel + 1];
        byte blue = image.Pixels[pixel + 2];
        int minimum = Math.Min(red, Math.Min(green, blue));
        int maximum = Math.Max(red, Math.Max(green, blue));
        return minimum >= 165 &&
            maximum - minimum <= 45;
    }

    private static ScreenRegion Offset(
        ScreenRegion region,
        int x) =>
        new(
            region.X + x,
            region.Y,
            region.Width,
            region.Height);

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
                "Match-lobby door detector input must be an RGB 808 by 611 client image.");
        }
    }

    private readonly record struct WhiteComponent(
        int MinimumX,
        int MinimumY,
        int MaximumX,
        int MaximumY,
        int Count)
    {
        public int Width =>
            MaximumX - MinimumX + 1;

        public int Height =>
            MaximumY - MinimumY + 1;

        public double FillRatio =>
            Count / (double)(Width * Height);
    }

    private readonly record struct DoorStructure(
        double TopEdge,
        double LeftJamb,
        double InnerJamb,
        double OuterJamb,
        double Handle,
        double InteriorFill)
    {
        public bool IsComplete =>
            TopEdge >= 0.80 &&
            LeftJamb >= 0.80 &&
            InnerJamb >= 0.75 &&
            OuterJamb >= 0.70 &&
            Handle >= 0.60 &&
            InteriorFill <= 0.16;
    }
}
