using ExpeditionsMacro.Vision.Placement;
using ExpeditionsMacro.Vision.Refuel;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Vision.Inspection;

internal static class TeamPlacementRefuelInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create() =>
    [
        DetectorInspectionDefinitionFactory.Create(
            "teams.screen",
            "Teams",
            "Team screen",
            "Classifies Unit Inventory, Unit Teams, and both confirmation dialogs.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(TeamScreenDetector)],
            image =>
            {
                TeamScreenMatch match =
                    TeamScreenDetector.Detect(image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != TeamScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY);
            },
            "The final state and live action are exposed. Component scores and rejected scrollbar candidates remain internal."),
        DetectorInspectionDefinitionFactory.Create(
            "teams.scrollbar",
            "Teams",
            "Team scrollbar",
            "Finds the live scrollbar thumb relative to the owned Unit Teams Close action.",
            DetectorInspectionDetailLevel.ResultOnly,
            [typeof(TeamScreenDetector)],
            image =>
            {
                TeamScrollbarThumb? thumb =
                    TeamScreenDetector
                        .FindScrollbarThumb(image);
                return DetectorProbeResult.Create(
                    thumb is null
                        ? "None"
                        : TeamScreenDetector
                            .IsScrollbarAtTop(
                                thumb.Value)
                            ? "Top"
                            : TeamScreenDetector
                                .IsScrollbarAtBottom(
                                    thumb.Value)
                                ? "Bottom"
                                : "Between",
                    passed: thumb is not null,
                    actionX: thumb?.X,
                    actionY: thumb?.CenterY,
                    metrics: thumb is null
                        ? []
                        :
                        [
                            new DetectorProbeMetric(
                                "thumb_height",
                                thumb.Value.Height.ToString(
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                thumb.Value.Height),
                            new DetectorProbeMetric(
                                "thumb_center_y",
                                thumb.Value.CenterY.ToString(
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                thumb.Value.CenterY),
                        ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "teams.team-1-load-action",
            "Teams",
            "Team 1 Load action",
            "Requires the scrollbar at Team 1 and a fully visible live green Load action.",
            DetectorInspectionDetailLevel.ResultOnly,
            [typeof(TeamScreenDetector)],
            image =>
            {
                (int X, int Y)? action =
                    TeamScreenDetector
                        .AlignedLoadTeamAction(
                            image,
                            teamSlot: 1,
                            TeamScreenDetector
                                .TopScrollbarCenterY);
                return DetectorProbeResult.Create(
                    action is null
                        ? "Unavailable"
                        : "Available",
                    passed: action is not null,
                    actionX: action?.X,
                    actionY: action?.Y);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "placement.selected-unit",
            "Placement",
            "Selected unit panel",
            "Measures the red Close control, blue Priority First control, and dark panel support.",
            DetectorInspectionDetailLevel.Detailed,
            [typeof(SelectedUnitPanelDetector)],
            image =>
            {
                SelectedUnitPanelMatch match =
                    SelectedUnitPanelDetector.Detect(
                        image);
                return DetectorProbeResult.Create(
                    match.Visible
                        ? "Selected unit"
                        : match.PanelVisible
                            ? "Panel only"
                            : "None",
                    match.Confidence,
                    passed: match.Visible,
                    metrics:
                    [
                        Number(
                            "close_score",
                            match.CloseScore),
                        Number(
                            "first_priority_score",
                            match.FirstPriorityScore),
                        Number(
                            "panel_score",
                            match.PanelScore),
                        new DetectorProbeMetric(
                            "panel_visible",
                            match.PanelVisible.ToString(),
                            BooleanValue:
                                match.PanelVisible),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "placement.upgrade-readiness",
            "Placement",
            "Upgrade readiness",
            "Classifies the selected unit as affordable, unaffordable, maxed, or unknown.",
            DetectorInspectionDetailLevel.Detailed,
            [typeof(UpgradeUnitReadinessDetector)],
            image =>
            {
                UpgradeUnitReadinessMatch match =
                    UpgradeUnitReadinessDetector.Detect(
                        image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State !=
                        UpgradeUnitReadinessState.Unknown,
                    metrics:
                    [
                        Number(
                            "green_score",
                            match.GreenScore),
                        Number(
                            "gray_score",
                            match.GrayScore),
                        Number(
                            "wide_gray_score",
                            match.WideGrayScore),
                        new DetectorProbeMetric(
                            "panel_visible",
                            match.PanelVisible.ToString(),
                            BooleanValue:
                                match.PanelVisible),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "refuel.areas",
            "Refuel",
            "Areas screen",
            "Classifies Areas, Lobby category, Spawn destination, and station entries.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(AreasScreenDetector)],
            image =>
            {
                AreasScreenMatch match =
                    AreasScreenDetector.Detect(image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != AreasScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "refuel.station",
            "Refuel",
            "Resource station",
            "Classifies Gold Mine, Resource Drill, and Add Fuel dialog ownership.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(ResourceStationScreenDetector)],
            image =>
            {
                ResourceStationScreenMatch match =
                    ResourceStationScreenDetector
                        .Detect(image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State !=
                        ResourceStationScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "confirm_action",
                            match.ConfirmActionX is int x &&
                            match.ConfirmActionY is int y
                                ? $"({x}, {y})"
                                : "None"),
                        new DetectorProbeMetric(
                            "dismiss_action",
                            match.DismissActionX is int dx &&
                            match.DismissActionY is int dy
                                ? $"({dx}, {dy})"
                                : "None"),
                    ]);
            }),
    ];

    private static DetectorProbeMetric Number(
        string key,
        double value) =>
        new(
            key,
            value.ToString(
                "0.000",
                System.Globalization.CultureInfo
                    .InvariantCulture),
            value);
}
