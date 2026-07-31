using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Refuel;

public enum ResourceStationScreenState
{
    None,
    GoldMine,
    ResourceDrill,
    AddFuelDialog,
}

public sealed record ResourceStationScreenMatch(
    ResourceStationScreenState State,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null,
    int? ConfirmActionX = null,
    int? ConfirmActionY = null,
    int? DismissActionX = null,
    int? DismissActionY = null);

public static class ResourceStationScreenDetector
{
    private static readonly ScreenRegion AddFuelSearch =
        new(320, 395, 180, 70);
    private static readonly ScreenRegion Close =
        new(610, 145, 50, 50);
    private static readonly ScreenRegion Accent =
        new(120, 145, 220, 70);
    private static readonly ScreenRegion FirstBuildingStatsBar =
        new(365, 211, 270, 10);
    private static readonly ScreenRegion SecondBuildingStatsBar =
        new(365, 229, 270, 10);
    private static readonly ScreenRegion RewardsRightRail =
        new(630, 255, 10, 130);
    private static readonly ScreenRegion RewardsBottomRail =
        new(515, 380, 125, 10);

    public static ResourceStationScreenMatch Detect(
        ImageFrame image)
    {
        RefuelVisionMetrics.ValidateClient(image);
        AddFuelDialogEvidence dialog =
            AddFuelDialogDetector.Detect(image);
        StationEvidence station = DetectStation(image);
        double goldScore = StationScore(
            station.Common,
            station.GoldAccent);
        double drillScore = StationScore(
            station.Common,
            station.BlueAccent);

        ResourceStationScreenMatch match =
            dialog.Confidence >= 0.76
                ? new ResourceStationScreenMatch(
                    ResourceStationScreenState.AddFuelDialog,
                    dialog.Confidence,
                    dialog.MaxX,
                    dialog.MaxY,
                    dialog.ConfirmX,
                    dialog.ConfirmY,
                    dialog.CancelX,
                    dialog.CancelY)
                : goldScore >= 0.74
                    ? new ResourceStationScreenMatch(
                        ResourceStationScreenState.GoldMine,
                        goldScore,
                        station.AddFuelX,
                        station.AddFuelY,
                        DismissActionX:
                            station.CloseX,
                        DismissActionY:
                            station.CloseY)
                    : drillScore >= 0.74
                        ? new ResourceStationScreenMatch(
                            ResourceStationScreenState.ResourceDrill,
                            drillScore,
                            station.AddFuelX,
                            station.AddFuelY,
                            DismissActionX:
                                station.CloseX,
                            DismissActionY:
                                station.CloseY)
                        : new ResourceStationScreenMatch(
                            ResourceStationScreenState.None,
                            0);
        VisionTrace.Emit(
            "resource_station_screen",
            match.State.ToString(),
            match.Confidence,
            new
            {
                Common = station.Common,
                Dialog = dialog.Confidence,
                station.OffsetX,
                station.OffsetY,
                station.AddFuelPixels,
                station.FirstStatsBar,
                station.SecondStatsBar,
                station.RewardsRightDark,
                station.RewardsBottomDark,
                station.ClosePixels,
                station.GoldAccent,
                station.BlueAccent,
                GoldScore = goldScore,
                DrillScore = drillScore,
                DialogOffsetX = dialog.OffsetX,
                DialogOffsetY = dialog.OffsetY,
                DialogMaxPixels = dialog.MaxPixels,
                DialogConfirmPixels =
                    dialog.ConfirmPixels,
                DialogCancelPixels =
                    dialog.CancelPixels,
                DialogFirstStatsBar =
                    dialog.FirstStatsBar,
                DialogSecondStatsBar =
                    dialog.SecondStatsBar,
                match.ActionX,
                match.ActionY,
                match.ConfirmActionX,
                match.ConfirmActionY,
                match.DismissActionX,
                match.DismissActionY,
            });
        return match;
    }

    private static StationEvidence DetectStation(
        ImageFrame image)
    {
        RefuelColorComponent? addFuel =
            RefuelVisionMetrics.FindComponent(
            image,
            AddFuelSearch,
            RefuelVisionMetrics.IsOrange,
            component =>
                component.Width is >= 120 and <= 145 &&
                component.Height is >= 20 and <= 35 &&
                component.Count >= 1000);
        if (addFuel is not RefuelColorComponent action)
        {
            return default;
        }

        int actionX = RoundCenter(action.CenterX);
        int actionY = RoundCenter(action.CenterY);
        int offsetX = actionX - 406;
        int offsetY = actionY - 430;
        if (Math.Abs(offsetX) > 12 ||
            Math.Abs(offsetY) > 12)
        {
            return default;
        }

        RefuelColorComponent? close =
            RefuelVisionMetrics.FindComponent(
            image,
            Close.Translate(offsetX, offsetY),
            RefuelVisionMetrics.IsRed,
            component =>
                component.Width is >= 18 and <= 30 &&
                component.Height is >= 16 and <= 30 &&
                component.Count >= 180);
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
        double goldAccent = AccentScore(
            image,
            offsetX,
            offsetY,
            RefuelVisionMetrics.IsGold);
        double blueAccent = AccentScore(
            image,
            offsetX,
            offsetY,
            RefuelVisionMetrics.IsBlue);
        if (close is not RefuelColorComponent closeAction ||
            firstStatsBar < 0.78 ||
            secondStatsBar < 0.78 ||
            rewardsRightDark < 0.90 ||
            rewardsBottomDark < 0.90)
        {
            return new StationEvidence(
                OffsetX: offsetX,
                OffsetY: offsetY,
                AddFuelPixels: action.Count,
                FirstStatsBar: firstStatsBar,
                SecondStatsBar: secondStatsBar,
                RewardsRightDark: rewardsRightDark,
                RewardsBottomDark: rewardsBottomDark,
                GoldAccent: goldAccent,
                BlueAccent: blueAccent);
        }

        double common = Math.Clamp(
            0.56 +
            0.10 * RefuelVisionMetrics.Ramp(
                action.Count,
                1000,
                2800) +
            0.10 * RefuelVisionMetrics.Ramp(
                firstStatsBar,
                0.78,
                0.96) +
            0.10 * RefuelVisionMetrics.Ramp(
                secondStatsBar,
                0.78,
                0.96) +
            0.07 * RefuelVisionMetrics.Ramp(
                rewardsRightDark,
                0.90,
                1.00) +
            0.07 * RefuelVisionMetrics.Ramp(
                rewardsBottomDark,
                0.90,
                1.00) +
            0.10 * RefuelVisionMetrics.Ramp(
                closeAction.Count,
                180,
                300),
            0,
            1);
        return new StationEvidence(
            common,
            offsetX,
            offsetY,
            actionX,
            actionY,
            RoundCenter(closeAction.CenterX),
            RoundCenter(closeAction.CenterY),
            action.Count,
            firstStatsBar,
            secondStatsBar,
            rewardsRightDark,
            rewardsBottomDark,
            closeAction.Count,
            goldAccent,
            blueAccent);
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

    private static double AccentScore(
        ImageFrame image,
        int offsetX,
        int offsetY,
        Func<byte, byte, byte, bool> color)
    {
        RefuelColorComponent? accent =
            RefuelVisionMetrics.FindComponent(
            image,
            Accent.Translate(offsetX, offsetY),
            color,
            component =>
                component.Width >= 140 &&
                component.Height >= 20 &&
                component.Count >= 1200);
        return accent is not RefuelColorComponent component
            ? 0
            : Math.Clamp(
                0.74 +
                0.26 * RefuelVisionMetrics.Ramp(
                    component.Count,
                    1200,
                    2900),
                0,
                1);
    }

    private static double StationScore(
        double common,
        double accent) =>
        common == 0 || accent == 0
            ? 0
            : Math.Clamp(
                0.78 * common +
                0.22 * accent,
                0,
                1);

    private static int RoundCenter(double value) =>
        (int)Math.Round(
            value,
            MidpointRounding.AwayFromZero);

    private readonly record struct StationEvidence(
        double Common = 0,
        int OffsetX = 0,
        int OffsetY = 0,
        int AddFuelX = 0,
        int AddFuelY = 0,
        int CloseX = 0,
        int CloseY = 0,
        int AddFuelPixels = 0,
        double FirstStatsBar = 0,
        double SecondStatsBar = 0,
        double RewardsRightDark = 0,
        double RewardsBottomDark = 0,
        int ClosePixels = 0,
        double GoldAccent = 0,
        double BlueAccent = 0);

}
