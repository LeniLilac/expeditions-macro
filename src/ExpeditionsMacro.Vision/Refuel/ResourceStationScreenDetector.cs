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
    int? ActionY = null);

public static class ResourceStationScreenDetector
{
    private static readonly ScreenRegion Panel =
        new(160, 155, 490, 305);
    private static readonly ScreenRegion Header =
        new(120, 135, 210, 85);
    private static readonly ScreenRegion Close =
        new(615, 145, 55, 55);
    private static readonly ScreenRegion AddFuel =
        new(335, 410, 145, 48);
    private static readonly ScreenRegion ClaimRewards =
        new(505, 405, 140, 55);
    private static readonly ScreenRegion Dialog =
        new(265, 245, 280, 120);
    private static readonly ScreenRegion DialogConfirm =
        new(270, 327, 135, 35);
    private static readonly ScreenRegion DialogTitle =
        new(365, 250, 95, 35);

    public static ResourceStationScreenMatch Detect(
        ImageFrame image)
    {
        RefuelVisionMetrics.ValidateClient(image);
        double panelDark =
            RefuelVisionMetrics.ColorFraction(
                image,
                Panel,
                RefuelVisionMetrics.IsDark);
        double close =
            RefuelVisionMetrics.ColorFraction(
                image,
                Close,
                RefuelVisionMetrics.IsRed);
        double addFuel =
            RefuelVisionMetrics.ColorFraction(
                image,
                AddFuel,
                RefuelVisionMetrics.IsOrange);
        double claim =
            RefuelVisionMetrics.ColorFraction(
                image,
                ClaimRewards,
                RefuelVisionMetrics.IsGreen);
        double common =
            panelDark < 0.52 ||
            close < 0.02 ||
            addFuel < 0.08 ||
            claim < 0.10
                ? 0
                : Math.Clamp(
                    0.52 +
                    0.14 * RefuelVisionMetrics.Ramp(
                        panelDark,
                        0.52,
                        0.82) +
                    0.10 * RefuelVisionMetrics.Ramp(
                        close,
                        0.02,
                        0.14) +
                    0.12 * RefuelVisionMetrics.Ramp(
                        addFuel,
                        0.08,
                        0.42) +
                    0.12 * RefuelVisionMetrics.Ramp(
                        claim,
                        0.10,
                        0.48),
                    0,
                    1);

        double dialogDark =
            RefuelVisionMetrics.ColorFraction(
                image,
                Dialog,
                RefuelVisionMetrics.IsDark);
        double confirm =
            RefuelVisionMetrics.ColorFraction(
                image,
                DialogConfirm,
                RefuelVisionMetrics.IsGreen);
        double title =
            RefuelVisionMetrics.ColorFraction(
                image,
                DialogTitle,
                RefuelVisionMetrics.IsBrightNeutral);
        double dialog =
            panelDark < 0.52 ||
            close < 0.008 ||
            dialogDark < 0.55 ||
            confirm < 0.12 ||
            title < 0.025
                ? 0
                : Math.Clamp(
                    0.38 +
                    0.16 * RefuelVisionMetrics.Ramp(
                        panelDark,
                        0.52,
                        0.82) +
                    0.06 * RefuelVisionMetrics.Ramp(
                        close,
                        0.008,
                        0.12) +
                    0.14 * RefuelVisionMetrics.Ramp(
                        dialogDark,
                        0.55,
                        0.85) +
                    0.15 * RefuelVisionMetrics.Ramp(
                        confirm,
                        0.12,
                        0.50) +
                    0.11 * RefuelVisionMetrics.Ramp(
                        title,
                        0.025,
                        0.13),
                    0,
                    1);

        double gold =
            RefuelVisionMetrics.ColorFraction(
                image,
                Header,
                RefuelVisionMetrics.IsGold);
        double blue =
            RefuelVisionMetrics.ColorFraction(
                image,
                Header,
                RefuelVisionMetrics.IsBlue);
        double goldScore = StationScore(common, gold);
        double drillScore = StationScore(common, blue);

        ResourceStationScreenMatch match =
            dialog >= 0.76
                ? new ResourceStationScreenMatch(
                    ResourceStationScreenState.AddFuelDialog,
                    dialog,
                    ActionX: 516,
                    ActionY: 312)
                : goldScore >= 0.74
                    ? new ResourceStationScreenMatch(
                        ResourceStationScreenState.GoldMine,
                        goldScore,
                        ActionX: 406,
                        ActionY: 438)
                    : drillScore >= 0.74
                        ? new ResourceStationScreenMatch(
                            ResourceStationScreenState.ResourceDrill,
                            drillScore,
                            ActionX: 406,
                            ActionY: 429)
                        : new ResourceStationScreenMatch(
                            ResourceStationScreenState.None,
                            0);
        VisionTrace.Emit(
            "resource_station_screen",
            match.State.ToString(),
            match.Confidence,
            new
            {
                Common = common,
                Dialog = dialog,
                Gold = gold,
                Blue = blue,
                GoldScore = goldScore,
                DrillScore = drillScore,
                match.ActionX,
                match.ActionY,
            });
        return match;
    }

    public static (int X, int Y) MaxFuelAction() =>
        (516, 312);

    public static (int X, int Y) ConfirmFuelAction() =>
        (337, 345);

    private static double StationScore(
        double common,
        double accent) =>
        common == 0 || accent < 0.02
            ? 0
            : Math.Clamp(
                0.78 * common +
                0.22 * RefuelVisionMetrics.Ramp(
                    accent,
                    0.02,
                    0.14),
                0,
                1);
}
