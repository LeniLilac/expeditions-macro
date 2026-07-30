using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Refuel;

public enum AreasScreenState
{
    None,
    Menu,
    Expeditions,
    Lobby,
}

public sealed record AreasScreenMatch(
    AreasScreenState State,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null,
    int? LobbyTabActionX = null,
    int? LobbyTabActionY = null);

public static class AreasScreenDetector
{
    private static readonly ScreenRegion Panel =
        new(145, 150, 520, 315);
    private static readonly ScreenRegion Header =
        new(100, 130, 220, 75);
    private static readonly ScreenRegion Close =
        new(630, 140, 45, 50);
    private static readonly ScreenRegion BottomEdge =
        new(145, 435, 520, 30);
    private static readonly ScreenRegion ExpeditionTab =
        new(148, 288, 98, 38);
    private static readonly ScreenRegion LobbyTab =
        new(148, 238, 98, 38);
    private static readonly ScreenRegion ExpeditionHeading =
        new(250, 178, 180, 35);
    private static readonly ScreenRegion LobbyHeading =
        new(250, 178, 180, 35);
    private static readonly ScreenRegion HubCard =
        new(250, 205, 140, 70);
    private static readonly ScreenRegion LobbySpawnCard =
        new(250, 345, 140, 80);
    private static readonly ScreenRegion[] NavigationButtons =
    [
        new(150, 188, 94, 28),
        new(150, 218, 94, 28),
        new(150, 248, 94, 28),
        new(150, 278, 94, 28),
        new(150, 298, 94, 28),
    ];

    public static AreasScreenMatch Detect(ImageFrame image)
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
        double accent = Math.Max(
            RefuelVisionMetrics.ColorFraction(
                image,
                Header,
                RefuelVisionMetrics.IsPurple),
            RefuelVisionMetrics.ColorFraction(
                image,
                Header,
                RefuelVisionMetrics.IsTeal));
        double bottom = Math.Max(
            RefuelVisionMetrics.BestHorizontalLineFraction(
                image,
                BottomEdge,
                RefuelVisionMetrics.IsPurple),
            RefuelVisionMetrics.BestHorizontalLineFraction(
                image,
                BottomEdge,
                RefuelVisionMetrics.IsTeal));
        int supportedNavigationButtons =
            NavigationButtons.Count(region =>
                RefuelVisionMetrics.ColorFraction(
                    image,
                    region,
                    RefuelVisionMetrics.IsNeutralGray) >= 0.12);
        double structure =
            panelDark < 0.48 ||
            close < 0.025 ||
            accent < 0.018 ||
            bottom < 0.55 ||
            supportedNavigationButtons < 4
                ? 0
                : Math.Clamp(
                    0.58 +
                    0.14 * RefuelVisionMetrics.Ramp(
                        panelDark,
                        0.48,
                        0.82) +
                    0.10 * RefuelVisionMetrics.Ramp(
                        close,
                        0.025,
                        0.16) +
                    0.10 * RefuelVisionMetrics.Ramp(
                        accent,
                        0.018,
                        0.12) +
                    0.08 * RefuelVisionMetrics.Ramp(
                        bottom,
                        0.55,
                        0.95),
                    0,
                    1);

        double selectedTab =
            RefuelVisionMetrics.ColorFraction(
                image,
                ExpeditionTab,
                RefuelVisionMetrics.IsTeal);
        double heading =
            RefuelVisionMetrics.ColorFraction(
                image,
                ExpeditionHeading,
                RefuelVisionMetrics.IsTeal);
        double hubCard =
            RefuelVisionMetrics.ColorFraction(
                image,
                HubCard,
                RefuelVisionMetrics.IsTeal);
        double expeditions =
            structure == 0 ||
            selectedTab < 0.045 ||
            heading < 0.016 ||
            hubCard < 0.025
                ? 0
                : Math.Clamp(
                    0.52 * structure +
                    0.20 * RefuelVisionMetrics.Ramp(
                        selectedTab,
                        0.045,
                        0.20) +
                    0.16 * RefuelVisionMetrics.Ramp(
                        heading,
                        0.016,
                        0.10) +
                    0.12 * RefuelVisionMetrics.Ramp(
                        hubCard,
                        0.025,
                        0.16),
                    0,
                    1);

        double lobbySelectedTab =
            RefuelVisionMetrics.ColorFraction(
                image,
                LobbyTab,
                RefuelVisionMetrics.IsTeal);
        double lobbyHeading =
            RefuelVisionMetrics.ColorFraction(
                image,
                LobbyHeading,
                RefuelVisionMetrics.IsTeal);
        double lobbySpawn =
            RefuelVisionMetrics.ColorFraction(
                image,
                LobbySpawnCard,
                RefuelVisionMetrics.IsTeal);
        double lobby =
            structure == 0 ||
            lobbySelectedTab < 0.25 ||
            lobbyHeading < 0.04 ||
            lobbySpawn < 0.035
                ? 0
                : Math.Clamp(
                    0.52 * structure +
                    0.20 * RefuelVisionMetrics.Ramp(
                        lobbySelectedTab,
                        0.25,
                        0.55) +
                    0.16 * RefuelVisionMetrics.Ramp(
                        lobbyHeading,
                        0.04,
                        0.08) +
                    0.12 * RefuelVisionMetrics.Ramp(
                        lobbySpawn,
                        0.035,
                        0.07),
                    0,
                    1);

        const int lobbyTabX = 198;
        const int lobbyTabY = 252;
        AreasScreenMatch match = lobby >= 0.76
            ? new AreasScreenMatch(
                AreasScreenState.Lobby,
                lobby,
                ActionX: 318,
                ActionY: 388,
                LobbyTabActionX: lobbyTabX,
                LobbyTabActionY: lobbyTabY)
            : expeditions >= 0.76
            ? new AreasScreenMatch(
                AreasScreenState.Expeditions,
                expeditions,
                ActionX: 322,
                ActionY: 264,
                LobbyTabActionX: lobbyTabX,
                LobbyTabActionY: lobbyTabY)
            : structure >= 0.74
                ? new AreasScreenMatch(
                    AreasScreenState.Menu,
                    structure,
                    ActionX: 198,
                    ActionY: 304,
                    LobbyTabActionX: lobbyTabX,
                    LobbyTabActionY: lobbyTabY)
                : new AreasScreenMatch(
                    AreasScreenState.None,
                    0);
        VisionTrace.Emit(
            "areas_screen",
            match.State.ToString(),
            match.Confidence,
            new
            {
                PanelDark = panelDark,
                Close = close,
                Accent = accent,
                Bottom = bottom,
                SupportedNavigationButtons =
                    supportedNavigationButtons,
                SelectedTab = selectedTab,
                Heading = heading,
                HubCard = hubCard,
                LobbySelectedTab = lobbySelectedTab,
                LobbyHeading = lobbyHeading,
                LobbySpawn = lobbySpawn,
                match.ActionX,
                match.ActionY,
                match.LobbyTabActionX,
                match.LobbyTabActionY,
            });
        return match;
    }
}
