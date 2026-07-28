using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Placement;

public sealed record QuickPlacementSelectionMatch(
    bool Visible,
    double Confidence,
    int CyanPixels,
    int LeftTextPixels,
    int CenterTextPixels,
    int RightTextPixels,
    int IconPixels,
    int VerticalOffset);

public static class QuickPlacementSelectionDetector
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;

    private const int MinimumVerticalOffset = -10;
    private const int MaximumVerticalOffset = 0;
    private const int MinimumCyanPixels = 250;
    private const int MaximumCyanPixels = 430;
    private const int MinimumLeftTextPixels = 65;
    private const int MinimumCenterTextPixels = 105;
    private const int MinimumRightTextPixels = 62;
    private const int MinimumIconPixels = 5;

    private static readonly ScreenRegion IndicatorRegion =
        new(350, 486, 109, 22);
    private static readonly ScreenRegion LeftTextRegion =
        new(350, 496, 36, 12);
    private static readonly ScreenRegion CenterTextRegion =
        new(386, 496, 37, 12);
    private static readonly ScreenRegion RightTextRegion =
        new(423, 496, 36, 12);
    private static readonly ScreenRegion IconRegion =
        new(399, 487, 10, 9);

    public static QuickPlacementSelectionMatch Detect(
        ImageFrame image)
    {
        Validate(image);

        QuickPlacementSelectionMatch baseline =
            DetectAtOffset(image, 0);
        QuickPlacementSelectionMatch match = baseline;
        for (int offset = MinimumVerticalOffset;
             offset <= MaximumVerticalOffset;
             offset++)
        {
            if (offset == 0)
            {
                continue;
            }
            QuickPlacementSelectionMatch candidate =
                DetectAtOffset(image, offset);
            if (!candidate.Visible)
            {
                continue;
            }
            if (!match.Visible ||
                candidate.Confidence > match.Confidence ||
                candidate.Confidence == match.Confidence &&
                Math.Abs(candidate.VerticalOffset) <
                Math.Abs(match.VerticalOffset))
            {
                match = candidate;
            }
        }
        VisionTrace.Emit(
            "quick_placement_selection",
            match.Visible ? "visible" : "none",
            match.Confidence,
            new
            {
                cyan = match.CyanPixels,
                left = match.LeftTextPixels,
                center = match.CenterTextPixels,
                right = match.RightTextPixels,
                icon = match.IconPixels,
                vertical_offset = match.VerticalOffset,
            });
        return match;
    }

    private static QuickPlacementSelectionMatch DetectAtOffset(
        ImageFrame image,
        int verticalOffset)
    {
        int cyan = CountCyan(
            image,
            IndicatorRegion.Translate(0, verticalOffset));
        int left = CountCyan(
            image,
            LeftTextRegion.Translate(0, verticalOffset));
        int center = CountCyan(
            image,
            CenterTextRegion.Translate(0, verticalOffset));
        int right = CountCyan(
            image,
            RightTextRegion.Translate(0, verticalOffset));
        int icon = CountCyan(
            image,
            IconRegion.Translate(0, verticalOffset));
        bool visible =
            cyan is >= MinimumCyanPixels and <= MaximumCyanPixels &&
            left >= MinimumLeftTextPixels &&
            center >= MinimumCenterTextPixels &&
            right >= MinimumRightTextPixels &&
            icon >= MinimumIconPixels;
        double confidence = visible
            ? Math.Clamp(
                0.70 +
                0.08 * Ramp(cyan, MinimumCyanPixels, 360) +
                0.06 * Ramp(left, MinimumLeftTextPixels, 90) +
                0.06 * Ramp(center, MinimumCenterTextPixels, 175) +
                0.06 * Ramp(right, MinimumRightTextPixels, 90) +
                0.04 * Ramp(icon, MinimumIconPixels, 8),
                0,
                1)
            : 0;
        return new QuickPlacementSelectionMatch(
            visible,
            confidence,
            cyan,
            left,
            center,
            right,
            icon,
            verticalOffset);
    }

    private static int CountCyan(
        ImageFrame image,
        ScreenRegion region)
    {
        int count = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (IsOpaqueQuickPlacementCyan(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2]))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static bool IsOpaqueQuickPlacementCyan(
        byte red,
        byte green,
        byte blue) =>
        blue >= 170 &&
        green >= 100 &&
        blue - red >= 80 &&
        green - red >= 60;

    private static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);

    private static void Validate(
        ImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width != ClientWidth ||
            image.Height != ClientHeight ||
            image.Format != PixelFormat.Rgb24)
        {
            throw new InvalidDataException(
                "Quick Placement detection requires an 808 by 611 RGB Roblox client frame.");
        }
    }
}
