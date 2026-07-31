using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Refuel;

internal readonly record struct AddFuelDialogEvidence(
    double Confidence = 0,
    int OffsetX = 0,
    int OffsetY = 0,
    int MaxX = 0,
    int MaxY = 0,
    int ConfirmX = 0,
    int ConfirmY = 0,
    int CancelX = 0,
    int CancelY = 0,
    int MaxPixels = 0,
    int ConfirmPixels = 0,
    int CancelPixels = 0,
    double FirstStatsBar = 0,
    double SecondStatsBar = 0);

internal static class AddFuelDialogDetector
{
    private static readonly ScreenRegion MaximumSearch =
        new(480, 290, 70, 45);
    private static readonly ScreenRegion ConfirmSearch =
        new(250, 320, 170, 50);
    private static readonly ScreenRegion CancelSearch =
        new(395, 320, 150, 50);
    private static readonly ScreenRegion FirstBuildingStatsBar =
        new(365, 211, 270, 10);
    private static readonly ScreenRegion SecondBuildingStatsBar =
        new(365, 229, 270, 10);
    private static readonly ScreenRegion RewardsRightRail =
        new(630, 255, 10, 130);
    private static readonly ScreenRegion RewardsBottomRail =
        new(515, 380, 125, 10);

    public static AddFuelDialogEvidence Detect(
        ImageFrame image)
    {
        RefuelColorComponent? maximum =
            RefuelVisionMetrics.FindComponent(
                image,
                MaximumSearch,
                RefuelVisionMetrics.IsNeutralGray,
                component =>
                    component.Width is >= 28 and <= 45 &&
                    component.Height is >= 16 and <= 28 &&
                    component.Count >= 400);
        RefuelColorComponent? confirm =
            RefuelVisionMetrics.FindComponent(
                image,
                ConfirmSearch,
                RefuelVisionMetrics.IsGreen,
                component =>
                    component.Width is >= 115 and <= 145 &&
                    component.Height is >= 18 and <= 30 &&
                    component.Count >= 2000);
        RefuelColorComponent? cancel =
            RefuelVisionMetrics.FindComponent(
                image,
                CancelSearch,
                RefuelVisionMetrics.IsNeutralGray,
                component =>
                    component.Width is >= 115 and <= 145 &&
                    component.Height is >= 18 and <= 30 &&
                    component.Count >= 2000);
        if (maximum is not RefuelColorComponent maxAction ||
            confirm is not RefuelColorComponent confirmAction ||
            cancel is not RefuelColorComponent cancelAction)
        {
            return default;
        }

        int maxX = RoundCenter(maxAction.CenterX);
        int maxY = RoundCenter(maxAction.CenterY);
        int offsetX = maxX - 515;
        int offsetY = maxY - 312;
        if (Math.Abs(offsetX) > 12 ||
            Math.Abs(offsetY) > 12 ||
            Math.Abs(
                confirmAction.CenterX -
                (337 + offsetX)) > 5 ||
            Math.Abs(
                cancelAction.CenterX -
                (470 + offsetX)) > 5 ||
            Math.Abs(
                confirmAction.CenterY -
                cancelAction.CenterY) > 3 ||
            confirmAction.CenterY -
            maxAction.CenterY is < 25 or > 40)
        {
            return default;
        }

        double firstStatsBar = StatsBarScore(
            image,
            FirstBuildingStatsBar,
            offsetX,
            offsetY);
        double secondStatsBar = StatsBarScore(
            image,
            SecondBuildingStatsBar,
            offsetX,
            offsetY);
        double rewardsRightDark =
            RefuelVisionMetrics.ColorFraction(
                image,
                RewardsRightRail.Translate(
                    offsetX,
                    offsetY),
                RefuelVisionMetrics.IsDark);
        double rewardsBottomDark =
            RefuelVisionMetrics.ColorFraction(
                image,
                RewardsBottomRail.Translate(
                    offsetX,
                    offsetY),
                RefuelVisionMetrics.IsDark);
        if (firstStatsBar < 0.78 ||
            secondStatsBar < 0.78 ||
            rewardsRightDark < 0.90 ||
            rewardsBottomDark < 0.90)
        {
            return new AddFuelDialogEvidence(
                OffsetX: offsetX,
                OffsetY: offsetY,
                MaxPixels: maxAction.Count,
                ConfirmPixels: confirmAction.Count,
                CancelPixels: cancelAction.Count,
                FirstStatsBar: firstStatsBar,
                SecondStatsBar: secondStatsBar);
        }

        double confidence = Math.Clamp(
            0.58 +
            0.08 * RefuelVisionMetrics.Ramp(
                maxAction.Count,
                400,
                650) +
            0.10 * RefuelVisionMetrics.Ramp(
                confirmAction.Count,
                2000,
                2900) +
            0.08 * RefuelVisionMetrics.Ramp(
                cancelAction.Count,
                2000,
                3000) +
            0.06 * RefuelVisionMetrics.Ramp(
                firstStatsBar,
                0.78,
                0.96) +
            0.06 * RefuelVisionMetrics.Ramp(
                secondStatsBar,
                0.78,
                0.96) +
            0.04 * RefuelVisionMetrics.Ramp(
                rewardsBottomDark,
                0.90,
                1.00),
            0,
            1);
        return new AddFuelDialogEvidence(
            confidence,
            offsetX,
            offsetY,
            maxX,
            maxY,
            RoundCenter(confirmAction.CenterX),
            RoundCenter(confirmAction.CenterY),
            RoundCenter(cancelAction.CenterX),
            RoundCenter(cancelAction.CenterY),
            maxAction.Count,
            confirmAction.Count,
            cancelAction.Count,
            firstStatsBar,
            secondStatsBar);
    }

    private static double StatsBarScore(
        ImageFrame image,
        ScreenRegion region,
        int offsetX,
        int offsetY) =>
        RefuelVisionMetrics.BestHorizontalBandFraction(
            image,
            region.Translate(offsetX, offsetY),
            bandHeight: 2,
            RefuelVisionMetrics.IsStationStatBar);

    private static int RoundCenter(double value) =>
        (int)Math.Round(
            value,
            MidpointRounding.AwayFromZero);
}
