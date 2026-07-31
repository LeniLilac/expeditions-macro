using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Navigation;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Vision.Inspection;

internal static class NavigationSettingsInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create() =>
    [
        DetectorInspectionDefinitionFactory.Create(
            "navigation.chat-button",
            "Navigation",
            "Roblox chat button",
            "Distinguishes the fixed outlined closed glyph from the opaque open glyph.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(RobloxChatButtonDetector)],
            image =>
            {
                RobloxChatButtonMatch match =
                    RobloxChatButtonDetector.Detect(
                        image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State !=
                        RobloxChatButtonState.None,
                    actionX:
                        match.Available
                            ? match.ActionX
                            : null,
                    actionY:
                        match.Available
                            ? match.ActionY
                            : null);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "navigation.match-lobby-door",
            "Navigation",
            "Match Lobby door",
            "Checks both fixed top-bar layouts without depending on voice controls.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(MatchLobbyDoorButtonDetector)],
            image =>
            {
                MatchLobbyDoorButtonMatch match =
                    MatchLobbyDoorButtonDetector
                        .Detect(image);
                return DetectorProbeResult.Create(
                    match.Visible
                        ? match.Layout.ToString()
                        : "None",
                    match.Confidence,
                    passed: match.Visible,
                    actionX:
                        match.Visible
                            ? match.ActionX
                            : null,
                    actionY:
                        match.Visible
                            ? match.ActionY
                            : null,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "layout",
                            match.Layout.ToString()),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "navigation.lobby-exit-confirmation",
            "Navigation",
            "Lobby exit confirmation",
            "Requires dark panel, red Return, and neutral Cancel evidence.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(LobbyExitConfirmationDetector)],
            image =>
            {
                LobbyExitConfirmationMatch match =
                    LobbyExitConfirmationDetector
                        .Detect(image);
                return DetectorProbeResult.Create(
                    match.Visible
                        ? "Visible"
                        : "None",
                    match.Confidence,
                    passed: match.Visible,
                    actionX:
                        match.Visible
                            ? match.ActionX
                            : null,
                    actionY:
                        match.Visible
                            ? match.ActionY
                            : null);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "settings.roblox-button",
            "Settings",
            "Roblox Settings button",
            "Classifies the normal or selected gear at both fixed top-bar offsets.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(RobloxSettingsButtonDetector)],
            image =>
            {
                RobloxSettingsButtonMatch match =
                    RobloxSettingsButtonDetector
                        .Detect(image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State !=
                        RobloxSettingsButtonState.None,
                    actionX:
                        match.Available
                            ? match.ActionX
                            : null,
                    actionY:
                        match.Available
                            ? match.ActionY
                            : null);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "settings.panel",
            "Settings",
            "Settings panel",
            "Measures panel ownership, settled geometry, close action, and rendered UI scale.",
            DetectorInspectionDetailLevel.Detailed,
            [typeof(GameSettingsScreenDetector)],
            image =>
            {
                GameSettingsPanelMatch match =
                    GameSettingsScreenDetector
                        .DetectPanel(image);
                return DetectorProbeResult.Create(
                    match.Visible
                        ? match.Settled
                            ? "Visible and settled"
                            : "Opening"
                        : "None",
                    match.Confidence,
                    passed:
                        match.Visible &&
                        match.Settled,
                    actionX:
                        match.Visible
                            ? match.CloseX
                            : null,
                    actionY:
                        match.Visible
                            ? match.CloseY
                            : null,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "visible",
                            match.Visible.ToString(),
                            BooleanValue:
                                match.Visible),
                        new DetectorProbeMetric(
                            "settled",
                            match.Settled.ToString(),
                            BooleanValue:
                                match.Settled),
                        Number(
                            "ui_scale",
                            match.UiScale),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "settings.page",
            "Settings",
            "Selected Settings page",
            "Reads the selected Settings navigation tab at canonical rendered scale.",
            DetectorInspectionDetailLevel.Partial,
            [
                typeof(GameSettingsScreenDetector),
                typeof(GameSettingsNavigationDetector),
            ],
            image =>
            {
                GameSettingsPageMatch match =
                    GameSettingsScreenDetector
                        .DetectPage(image);
                return DetectorProbeResult.Create(
                    match.Page.ToString(),
                    match.Confidence,
                    passed:
                        match.Page !=
                        GameSettingsPage.None,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "page",
                            match.Page.ToString()),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "settings.ui-scale-input",
            "Settings",
            "UI Scale input",
            "Finds the production UI Scale control and its focused state.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(GameSettingsNavigationDetector)],
            image =>
            {
                GameSettingsUiScaleInputMatch match =
                    GameSettingsNavigationDetector
                        .DetectUiScaleInput(image);
                return DetectorProbeResult.Create(
                    match.Available
                        ? match.Focused
                            ? "Focused"
                            : "Available"
                        : "None",
                    match.Confidence,
                    passed: match.Available,
                    actionX:
                        match.Available
                            ? match.ActionX
                            : null,
                    actionY:
                        match.Available
                            ? match.ActionY
                            : null,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "focused",
                            match.Focused.ToString(),
                            BooleanValue:
                                match.Focused),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "settings.required-toggles",
            "Settings",
            "Required setting toggles",
            "Evaluates every required production setting against the currently selected page.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(GameSettingsScreenDetector)],
            image =>
            {
                GameSettingToggleMatch[] matches =
                    Enum.GetValues<RequiredGameSetting>()
                        .Select(setting =>
                            GameSettingsScreenDetector
                                .DetectToggle(
                                    image,
                                    setting))
                        .ToArray();
                GameSettingToggleMatch? live =
                    matches
                        .Where(match =>
                            match.State !=
                            GameSettingToggleState
                                .Unknown)
                        .Select(match =>
                            (GameSettingToggleMatch?)match)
                        .FirstOrDefault();
                return DetectorProbeResult.Create(
                    live is null
                        ? "No toggle owned"
                        : $"{live.Value.Setting}: {live.Value.State}",
                    live?.Confidence,
                    passed: live is not null,
                    actionX:
                        live is null
                            ? null
                            : live.Value.ActionX,
                    actionY:
                        live is null
                            ? null
                            : live.Value.ActionY,
                    metrics: matches
                        .Select(match =>
                            new DetectorProbeMetric(
                                $"toggle.{match.Setting}",
                                $"{match.State}, confidence {match.Confidence:0.000}",
                                match.Confidence))
                        .ToArray());
            },
            "Each setting uses production layout metadata. Only the toggle on the current owned page can report a live state."),
        DetectorInspectionDefinitionFactory.Create(
            "settings.units-scrollbar",
            "Settings",
            "Units scrollbar",
            "Finds the production Units-page scrollbar thumb and its boundary state.",
            DetectorInspectionDetailLevel.ResultOnly,
            [typeof(GameSettingsScreenDetector)],
            image =>
            {
                GameSettingsScrollbarThumb? thumb =
                    GameSettingsScreenDetector
                        .FindUnitsScrollbarThumb(image);
                return DetectorProbeResult.Create(
                    thumb is null
                        ? "None"
                        : thumb.Value.IsAtTop
                            ? "Top"
                            : thumb.Value.IsAtBottom
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
                                "thumb_start_y",
                                thumb.Value.StartY.ToString(
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                thumb.Value.StartY),
                            new DetectorProbeMetric(
                                "thumb_end_y",
                                thumb.Value.EndY.ToString(
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                thumb.Value.EndY),
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
