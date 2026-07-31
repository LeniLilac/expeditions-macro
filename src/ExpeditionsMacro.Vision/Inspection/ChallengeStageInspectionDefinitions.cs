using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Vision.Inspection;

internal static class ChallengeStageInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create() =>
    [
        DetectorInspectionDefinitionFactory.Create(
            "challenge.screen",
            "Challenges",
            "Challenge screen",
            "Scores the complete Challenge navigation and terminal state set.",
            DetectorInspectionDetailLevel.Detailed,
            [typeof(ChallengeScreenDetector)],
            image =>
            {
                IReadOnlyDictionary<
                    ChallengeScreenState,
                    double> scores =
                    ChallengeScreenDetector.ScoreStates(
                        image);
                ChallengeScreenMatch match =
                    ChallengeScreenDetector.Detect(image);
                double? threshold =
                    match.State ==
                    ChallengeScreenState.None
                        ? null
                        : ChallengeScreenDetector.Threshold(
                            match.State);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    threshold,
                    match.State !=
                    ChallengeScreenState.None,
                    match.ActionX,
                    match.ActionY,
                    metrics: scores
                        .Select(pair =>
                            new DetectorProbeMetric(
                                $"state.{pair.Key}",
                                pair.Value.ToString(
                                    "0.000",
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                pair.Value,
                                GateValue:
                                    ChallengeScreenDetector
                                        .Threshold(
                                            pair.Key)))
                        .ToArray());
            }),
        DetectorInspectionDefinitionFactory.Create(
            "challenge.match-state",
            "Challenges",
            "Challenge match state",
            "Runs the hot-loop Challenge terminal and shared-selector detector.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(ChallengeMatchStateDetector)],
            image =>
            {
                ChallengeScreenMatch match =
                    ChallengeMatchStateDetector.Detect(
                        image);
                double? threshold =
                    match.State ==
                    ChallengeScreenState.None
                        ? null
                        : ChallengeScreenDetector.Threshold(
                            match.State);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    threshold,
                    match.State !=
                    ChallengeScreenState.None,
                    match.ActionX,
                    match.ActionY);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "stage.screen",
            "Story and Raid",
            "Stage screen",
            "Runs the production Story, Raid, party, prestart, and terminal classifier.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(StageScreenDetector)],
            image =>
            {
                StageScreenMatch match =
                    StageScreenDetector.Detect(image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != StageScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY);
            },
            "The production result exposes final confidence and a live action, while component thresholds remain algorithm-private."),
        DetectorInspectionDefinitionFactory.Create(
            "stage.match-state",
            "Story and Raid",
            "Stage match state",
            "Runs the reduced hot-loop Story and Raid terminal classifier.",
            DetectorInspectionDetailLevel.ResultOnly,
            [typeof(StageScreenDetector)],
            image =>
            {
                StageScreenMatch match =
                    StageScreenDetector.DetectMatchState(
                        image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != StageScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "stage.story-option",
            "Story and Raid",
            "Story option selection",
            "Reads the selected Act, Infinite, or Mastery row and Act difficulty.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(StageOptionSelectionDetector)],
            image =>
            {
                StoryOptionSelectionMatch match =
                    StageOptionSelectionDetector
                        .DetectStory(image);
                string state =
                    match.RunKind is null
                        ? "None"
                        : match.RunKind ==
                          ExpeditionsMacro.Core.Models
                              .StoryRunKind.Act
                            ? $"Act {match.ActNumber}, {(
                                match.HardMode == true
                                    ? "Hard"
                                    : "Normal")}"
                            : match.RunKind.ToString()!;
                return DetectorProbeResult.Create(
                    state,
                    match.Confidence,
                    passed: match.RunKind is not null,
                    actionX: match.ActionX,
                    actionY: match.ActionY,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "run_kind",
                            match.RunKind?.ToString() ??
                            "None"),
                        new DetectorProbeMetric(
                            "act_number",
                            match.ActNumber.ToString(
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture),
                            match.ActNumber),
                        new DetectorProbeMetric(
                            "hard_mode",
                            match.HardMode?.ToString() ??
                            "Not applicable",
                            BooleanValue:
                                match.HardMode),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "stage.raid-option",
            "Story and Raid",
            "Raid option selection",
            "Reads the selected Raid act from the production option rail.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(StageOptionSelectionDetector)],
            image =>
            {
                RaidOptionSelectionMatch match =
                    StageOptionSelectionDetector
                        .DetectRaid(image);
                return DetectorProbeResult.Create(
                    match.Act?.ToString() ?? "None",
                    match.Confidence,
                    passed: match.Act is not null,
                    actionX: match.ActionX,
                    actionY: match.ActionY,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "raid_act",
                            match.Act?.ToString() ??
                            "None"),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "stage.gameplay-hud",
            "Story and Raid",
            "Gameplay HUD",
            "Combines hotbar structure with live unit-manager and stage-info components.",
            DetectorInspectionDetailLevel.Detailed,
            [typeof(StageGameplayHudDetector)],
            image =>
            {
                StageGameplayHudMatch match =
                    StageGameplayHudDetector.Detect(
                        image);
                return DetectorProbeResult.Create(
                    match.Visible
                        ? "Visible"
                        : "Hidden",
                    match.Confidence,
                    passed: match.Visible,
                    metrics:
                    [
                        Number(
                            "hotbar_support",
                            match.HotbarSupport),
                        Number(
                            "unit_manager_score",
                            match.UnitManagerScore),
                        Number(
                            "stage_info_score",
                            match.StageInfoScore),
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
