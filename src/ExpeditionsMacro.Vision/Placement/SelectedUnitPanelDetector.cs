using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Placement;

public sealed record SelectedUnitPanelMatch(
    bool Visible,
    bool PanelVisible,
    double Confidence,
    double CloseScore,
    double FirstPriorityScore,
    double PanelScore);

public static class SelectedUnitPanelDetector
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;
    private const double MinimumCloseScore = 0.18;
    private const double MinimumFirstPriorityScore = 0.32;
    private const double MinimumPanelScore = 0.52;

    private static readonly ScreenRegion CloseControl =
        new(244, 209, 29, 30);
    private static readonly ScreenRegion FirstPriorityControl =
        new(29, 342, 52, 32);
    private static readonly ScreenRegion PanelBody =
        new(9, 212, 265, 182);

    public static SelectedUnitPanelMatch Detect(
        ImageFrame image)
    {
        Validate(image);
        double close = ColorFraction(
            image,
            CloseControl,
            IsCloseRed);
        double first = ColorFraction(
            image,
            FirstPriorityControl,
            IsPriorityBlue);
        double panel = ColorFraction(
            image,
            PanelBody,
            IsDark);
        bool panelVisible =
            close >= MinimumCloseScore &&
            panel >= MinimumPanelScore;
        bool visible =
            close >= MinimumCloseScore &&
            first >= MinimumFirstPriorityScore;
        double confidence = Math.Clamp(
            0.50 * Ramp(close, MinimumCloseScore, 0.26) +
            0.40 * Ramp(
                first,
                MinimumFirstPriorityScore,
                0.48) +
            0.10 * Ramp(panel, 0.52, 0.76),
            0,
            1);
        SelectedUnitPanelMatch match = new(
            visible,
            panelVisible,
            confidence,
            close,
            first,
            panel);
        VisionTrace.Emit(
            "selected_unit_panel",
            visible ? "visible" : "none",
            confidence,
            new
            {
                close,
                first_priority = first,
                panel,
                panel_visible = panelVisible,
            });
        return match;
    }

    private static void Validate(ImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width != ClientWidth ||
            image.Height != ClientHeight ||
            image.Format != PixelFormat.Rgb24)
        {
            throw new InvalidDataException(
                "Selected-unit detection requires an 808 by 611 RGB Roblox client frame.");
        }
    }

    private static double ColorFraction(
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

    private static bool IsCloseRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 130 &&
        red - green >= 55 &&
        red - blue >= 45 &&
        green <= 120;

    private static bool IsPriorityBlue(
        byte red,
        byte green,
        byte blue) =>
        blue >= 90 &&
        blue - red >= 30 &&
        blue >= green &&
        red <= 100;

    private static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        red + green + blue <= 210;

    private static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);
}
