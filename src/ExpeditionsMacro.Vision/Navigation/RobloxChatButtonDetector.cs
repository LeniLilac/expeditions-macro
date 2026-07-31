using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Navigation;

public enum RobloxChatButtonState
{
    None = 0,
    Closed = 1,
    Open = 2,
}

public readonly record struct RobloxChatButtonMatch(
    RobloxChatButtonState State,
    double Confidence,
    int ActionX,
    int ActionY)
{
    public bool Available =>
        State != RobloxChatButtonState.None;
}

public static class RobloxChatButtonDetector
{
    public const int ActionX = 139;
    public const int ActionY = 34;

    private const int TopSearchLeft = 124;
    private const int TopSearchTop = 22;
    private const int TopSearchRight = 152;
    private const int TopSearchBottom = 28;
    private const int SideSearchLeft = 124;
    private const int SideSearchTop = 22;
    private const int SideSearchRight = 133;
    private const int SideSearchBottom = 41;
    private const int FillLeft = 130;
    private const int FillTop = 27;
    private const int FillRight = 137;
    private const int FillBottom = 36;
    private const int TailLeft = 135;
    private const int TailTop = 38;
    private const int TailRight = 149;
    private const int TailBottom = 44;

    public static RobloxChatButtonMatch Detect(
        ImageFrame image)
    {
        Validate(image);
        // Notification badges overlap the upper-right corner,
        // so only badge-independent speech geometry owns state.
        int topRun = LongestHorizontalRun(image);
        int sideRun = LongestVerticalRun(image);
        int fillPixels = CountOpaqueNeutral(
            image,
            FillLeft,
            FillTop,
            FillRight,
            FillBottom);
        int tailPixels = CountOpaqueNeutral(
            image,
            TailLeft,
            TailTop,
            TailRight,
            TailBottom);
        bool ownsSpeechGlyph =
            topRun >= 16 &&
            sideRun >= 12 &&
            tailPixels >= 18;
        RobloxChatButtonState state =
            !ownsSpeechGlyph
                ? RobloxChatButtonState.None
                : fillPixels >= 40
                ? RobloxChatButtonState.Open
                : fillPixels <= 20
                    ? RobloxChatButtonState.Closed
                    : RobloxChatButtonState.None;
        double confidence = state switch
        {
            RobloxChatButtonState.Open =>
                StructureConfidence(
                    topRun,
                    sideRun,
                    tailPixels,
                    Ramp(fillPixels, 40, 68)),
            RobloxChatButtonState.Closed =>
                StructureConfidence(
                    topRun,
                    sideRun,
                    tailPixels,
                    1 - Ramp(fillPixels, 20, 40)),
            _ => 0,
        };
        RobloxChatButtonMatch match = new(
            state,
            confidence,
            state == RobloxChatButtonState.None
                ? 0
                : ActionX,
            state == RobloxChatButtonState.None
                ? 0
                : ActionY);
        VisionTrace.Emit(
            "roblox_chat_button",
            state.ToString(),
            confidence,
            new
            {
                match.ActionX,
                match.ActionY,
                topRun,
                sideRun,
                fillPixels,
                tailPixels,
            });
        return match;
    }

    private static double StructureConfidence(
        int topRun,
        int sideRun,
        int tailPixels,
        double stateConfidence) =>
        Math.Clamp(
            0.25 * Ramp(topRun, 16, 21) +
            0.25 * Ramp(sideRun, 12, 14) +
            0.25 * Ramp(tailPixels, 18, 21) +
            0.25 * stateConfidence,
            0,
            1);

    private static int LongestHorizontalRun(
        ImageFrame image)
    {
        int longest = 0;
        for (int y = TopSearchTop;
             y <= TopSearchBottom;
             y++)
        {
            int current = 0;
            for (int x = TopSearchLeft;
                 x <= TopSearchRight;
                 x++)
            {
                current = IsOpaqueNeutral(image, x, y)
                    ? current + 1
                    : 0;
                longest = Math.Max(longest, current);
            }
        }
        return longest;
    }

    private static int LongestVerticalRun(
        ImageFrame image)
    {
        int longest = 0;
        for (int x = SideSearchLeft;
             x <= SideSearchRight;
             x++)
        {
            int current = 0;
            for (int y = SideSearchTop;
                 y <= SideSearchBottom;
                 y++)
            {
                current = IsOpaqueNeutral(image, x, y)
                    ? current + 1
                    : 0;
                longest = Math.Max(longest, current);
            }
        }
        return longest;
    }

    private static int CountOpaqueNeutral(
        ImageFrame image,
        int left,
        int top,
        int right,
        int bottom)
    {
        int count = 0;
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (IsOpaqueNeutral(image, x, y))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);

    private static bool IsOpaqueNeutral(
        ImageFrame image,
        int x,
        int y)
    {
        int pixel = (y * image.Width + x) * 3;
        byte red = image.Pixels[pixel];
        byte green = image.Pixels[pixel + 1];
        byte blue = image.Pixels[pixel + 2];
        int minimum = Math.Min(
            red,
            Math.Min(green, blue));
        int maximum = Math.Max(
            red,
            Math.Max(green, blue));
        return minimum >= 165 &&
            maximum - minimum <= 45;
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new InvalidDataException(
                "Roblox chat-button detector input must be an RGB 808 by 611 client image.");
        }
    }
}
