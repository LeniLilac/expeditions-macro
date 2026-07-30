using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Placement;

public enum UpgradeUnitReadinessState
{
    Unknown,
    Unaffordable,
    Affordable,
    Maxed,
}

public sealed record UpgradeUnitReadinessMatch(
    UpgradeUnitReadinessState State,
    double Confidence,
    bool PanelVisible,
    double GreenScore,
    double GrayScore,
    double WideGrayScore);

public static class UpgradeUnitReadinessDetector
{
    private const double MinimumGreenScore = 0.55;
    private const double MinimumGrayScore = 0.35;
    private const double MinimumWideGrayScore = 0.50;

    private static readonly ScreenRegion UpgradeControl =
        new(128, 345, 95, 26);
    private static readonly ScreenRegion WideControlExtension =
        new(224, 345, 5, 26);

    public static UpgradeUnitReadinessMatch Detect(
        ImageFrame image)
    {
        SelectedUnitPanelMatch panel =
            SelectedUnitPanelDetector.Detect(image);
        double green = ColorFraction(
            image,
            UpgradeControl,
            IsUpgradeGreen);
        double gray = ColorFraction(
            image,
            UpgradeControl,
            IsControlGray);
        double wideGray = ColorFraction(
            image,
            WideControlExtension,
            IsControlGray);

        UpgradeUnitReadinessState state =
            Classify(
                panel.PanelVisible,
                green,
                gray,
                wideGray);
        double confidence = state switch
        {
            UpgradeUnitReadinessState.Affordable =>
                Ramp(green, MinimumGreenScore, 0.66),
            UpgradeUnitReadinessState.Maxed =>
                Math.Min(
                    Ramp(gray, MinimumGrayScore, 0.48),
                    Ramp(
                        wideGray,
                        MinimumWideGrayScore,
                        0.65)),
            UpgradeUnitReadinessState.Unaffordable =>
                Math.Min(
                    Ramp(gray, MinimumGrayScore, 0.46),
                    1 - Ramp(
                        wideGray,
                        0.20,
                        MinimumWideGrayScore)),
            _ => 0,
        };
        UpgradeUnitReadinessMatch match = new(
            state,
            confidence,
            panel.PanelVisible,
            green,
            gray,
            wideGray);
        VisionTrace.Emit(
            "upgrade_unit_readiness",
            state.ToString(),
            confidence,
            new
            {
                panel_visible = panel.PanelVisible,
                green,
                gray,
                wide_gray = wideGray,
            });
        return match;
    }

    private static UpgradeUnitReadinessState Classify(
        bool panelVisible,
        double green,
        double gray,
        double wideGray)
    {
        if (!panelVisible)
        {
            return UpgradeUnitReadinessState.Unknown;
        }
        if (green >= MinimumGreenScore)
        {
            return UpgradeUnitReadinessState.Affordable;
        }
        if (gray < MinimumGrayScore)
        {
            return UpgradeUnitReadinessState.Unknown;
        }
        return wideGray >= MinimumWideGrayScore
            ? UpgradeUnitReadinessState.Maxed
            : UpgradeUnitReadinessState.Unaffordable;
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

    private static bool IsUpgradeGreen(
        byte red,
        byte green,
        byte blue) =>
        green >= 75 &&
        green - red >= 25 &&
        green - blue >= 25;

    private static bool IsControlGray(
        byte red,
        byte green,
        byte blue) =>
        Math.Max(red, Math.Max(green, blue)) -
        Math.Min(red, Math.Min(green, blue)) <= 10 &&
        red is >= 35 and <= 80;

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
