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
                component.Count is >= 68 and <= 84 &&
                component.Width is >= 15 and <= 16 &&
                component.Height is >= 18 and <= 20 &&
                Math.Abs(
                    component.MinimumX -
                    expectedMinimumX) <= 1 &&
                component.MinimumY is >= 25 and <= 26)
            .OrderByDescending(component => component.Count)
            .FirstOrDefault();
        WhiteComponent arrow = components
            .Where(component =>
                component.Count is >= 17 and <= 24 &&
                component.Width is >= 7 and <= 8 &&
                component.Height is >= 7 and <= 8 &&
                component.MinimumX - expectedMinimumX
                    is >= 13 and <= 14 &&
                component.MinimumY is >= 38 and <= 39)
            .OrderByDescending(component => component.Count)
            .FirstOrDefault();
        if (door.Count == 0 ||
            arrow.Count == 0 ||
            !HasDoorHandle(image, expectedMinimumX))
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

        double geometry =
            0.55 * Ramp(door.Count, 68, 77) +
            0.25 * Ramp(arrow.Count, 17, 20) +
            0.20 * Ramp(contrast, 100, 180);
        return new MatchLobbyDoorButtonMatch(
            true,
            Math.Clamp(geometry, 0, 1),
            expectedMinimumX + 10,
            DoorActionY,
            layout);
    }

    private static bool HasDoorHandle(
        ImageFrame image,
        int expectedMinimumX)
    {
        int count = 0;
        for (int y = 34; y <= 38; y++)
        {
            for (int x = expectedMinimumX + 5;
                 x <= expectedMinimumX + 8;
                 x++)
            {
                if (IsOpaqueWhite(image, x, y))
                {
                    count++;
                }
            }
        }
        return count is >= 2 and <= 7;
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
    }
}
