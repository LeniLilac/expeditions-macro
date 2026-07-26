using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Navigation;

public readonly record struct LobbyExitConfirmationMatch(
    bool Visible,
    double Confidence,
    int ActionX,
    int ActionY);

public static class LobbyExitConfirmationDetector
{
    private static readonly ScreenRegion Panel =
        new(280, 265, 250, 85);
    private static readonly ScreenRegion ReturnButton =
        new(288, 314, 115, 28);
    private static readonly ScreenRegion CancelButton =
        new(405, 314, 115, 28);

    public static LobbyExitConfirmationMatch Detect(
        ImageFrame image)
    {
        ValidateClient(image);
        double panelDark = ColorFraction(
            image,
            Panel,
            IsPanelDark);
        double returnRed = ColorFraction(
            image,
            ReturnButton,
            IsReturnRed);
        double cancelNeutral = ColorFraction(
            image,
            CancelButton,
            IsNeutral);
        bool visible =
            panelDark >= 0.58 &&
            returnRed >= 0.38 &&
            cancelNeutral >= 0.38;
        double confidence = visible
            ? Math.Clamp(
                0.35 * Ramp(panelDark, 0.58, 0.90) +
                0.35 * Ramp(returnRed, 0.38, 0.80) +
                0.30 * Ramp(cancelNeutral, 0.38, 0.82),
                0,
                1)
            : 0;
        LobbyExitConfirmationMatch match = new(
            visible,
            confidence,
            345,
            328);
        VisionTrace.Emit(
            "lobby_exit_confirmation",
            visible ? "visible" : "none",
            confidence,
            new
            {
                match.ActionX,
                match.ActionY,
            });
        return match;
    }

    private static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        int matches = 0;
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
                    matches++;
                }
            }
        }
        return (double)matches /
            (region.Width * region.Height);
    }

    private static bool IsPanelDark(
        byte red,
        byte green,
        byte blue) =>
        red + green + blue <= 190;

    private static bool IsReturnRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 115 &&
        red - green >= 35 &&
        red - blue >= 25;

    private static bool IsNeutral(
        byte red,
        byte green,
        byte blue) =>
        red is >= 65 and <= 210 &&
        Math.Abs(red - green) <= 18 &&
        Math.Abs(red - blue) <= 18;

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
                "Lobby-exit detector input must be an RGB 808 by 611 client image.");
        }
    }
}
