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

    private const int RegionLeft = 122;
    private const int RegionTop = 18;
    private const int RegionRight = 156;
    private const int RegionBottom = 50;

    public static RobloxChatButtonMatch Detect(
        ImageFrame image)
    {
        Validate(image);
        int opaqueNeutralPixels = 0;
        for (int y = RegionTop;
             y <= RegionBottom;
             y++)
        {
            for (int x = RegionLeft;
                 x <= RegionRight;
                 x++)
            {
                if (IsOpaqueNeutral(image, x, y))
                {
                    opaqueNeutralPixels++;
                }
            }
        }

        RobloxChatButtonState state =
            opaqueNeutralPixels is >= 230 and <= 380
                ? RobloxChatButtonState.Open
                : opaqueNeutralPixels is >= 60 and <= 170
                    ? RobloxChatButtonState.Closed
                    : RobloxChatButtonState.None;
        double confidence = state switch
        {
            RobloxChatButtonState.Open =>
                Closeness(
                    opaqueNeutralPixels,
                    expected: 303,
                    tolerance: 77),
            RobloxChatButtonState.Closed =>
                Closeness(
                    opaqueNeutralPixels,
                    expected: 90,
                    tolerance: 80),
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
                opaqueNeutralPixels,
            });
        return match;
    }

    private static double Closeness(
        int value,
        int expected,
        int tolerance) =>
        Math.Clamp(
            1 -
            Math.Abs(value - expected) /
            (double)tolerance,
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
