using ExpeditionsMacro.Vision.Bounties;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Vision.Inspection;

internal static class EventBountyInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create() =>
    [
        DetectorInspectionDefinitionFactory.Create(
            "event.screen",
            "Events",
            "Event screen",
            "Classifies Villain Invasion catalog, home, Act, party, prestart, and terminal screens.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(EventScreenDetector)],
            image =>
            {
                EventScreenMatch match =
                    EventScreenDetector.Detect(image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != EventScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY);
            },
            "The final state and live action are production-owned. Several Event component gates are not emitted as typed checks."),
        DetectorInspectionDefinitionFactory.Create(
            "event.match-state",
            "Events",
            "Event match state",
            "Runs the reduced Event terminal and shared-selector hot loop.",
            DetectorInspectionDetailLevel.ResultOnly,
            [typeof(EventScreenDetector)],
            image =>
            {
                EventScreenMatch match =
                    EventScreenDetector.DetectMatchState(
                        image);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != EventScreenState.None,
                    actionX: match.ActionX,
                    actionY: match.ActionY);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "bounty.board",
            "Bounties",
            "Bounty Board",
            "Classifies Event entry, Board, reroll confirmation, and reward overlays with live card evidence.",
            DetectorInspectionDetailLevel.Partial,
            [
                typeof(BountyBoardDetector),
                typeof(BountyBoardHeaderRecognizer),
                typeof(BountyBoardOwnerDetector),
            ],
            image =>
            {
                BountyBoardMatch match =
                    BountyBoardDetector.Detect(image);
                (int X, int Y)? action =
                    LiveBountyAction(image, match);
                return DetectorProbeResult.Create(
                    match.State.ToString(),
                    match.Confidence,
                    passed:
                        match.State != BountyBoardState.None,
                    actionX: action?.X,
                    actionY: action?.Y,
                    metrics:
                    [
                        new DetectorProbeMetric(
                            "card_action_count",
                            match.Actions.Count.ToString(
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture),
                            match.Actions.Count),
                        new DetectorProbeMetric(
                            "recognized_number_count",
                            match.Numbers.Count.ToString(
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture),
                            match.Numbers.Count),
                        new DetectorProbeMetric(
                            "no_gold",
                            match.NoGold
                                ? "True"
                                : "False",
                            BooleanValue: match.NoGold),
                        new DetectorProbeMetric(
                            "board_button_rail_score",
                            match.BoardButtonRailScore.ToString(
                                "0.000",
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture),
                            match.BoardButtonRailScore),
                        new DetectorProbeMetric(
                            "board_header_score",
                            match.BoardHeaderScore.ToString(
                                "0.000",
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture),
                            match.BoardHeaderScore),
                        new DetectorProbeMetric(
                            "board_header_text_fallback",
                            match.BoardHeaderUsedTextFallback
                                ? "True"
                                : "False",
                            BooleanValue:
                                match.BoardHeaderUsedTextFallback),
                    ]);
            },
            "Accepted live actions and numbers are exposed. Rejected card/action candidates remain internal."),
        DetectorInspectionDefinitionFactory.Create(
            "bounty.numbers",
            "Bounties",
            "Bounty number suffixes",
            "Recognizes #1 through #10 only when anchored to live Bounty card actions.",
            DetectorInspectionDetailLevel.ResultOnly,
            [typeof(BountyNumberRecognizer)],
            image =>
            {
                IReadOnlyList<BountyNumberMatch> matches =
                    BountyNumberRecognizer.Detect(image);
                return DetectorProbeResult.Create(
                    matches.Count == 0
                        ? "None"
                        : string.Join(
                            ", ",
                            matches.Select(match =>
                                $"#{match.Number}")),
                    matches.Count == 0
                        ? 0
                        : matches.Max(match =>
                            match.Confidence),
                    passed: matches.Count > 0,
                    metrics: matches
                        .Select((match, index) =>
                            new DetectorProbeMetric(
                                $"number_{index + 1}",
                                $"#{match.Number} at ({match.CenterX}, {match.CenterY}), confidence {match.Confidence:0.000}",
                                match.Confidence))
                        .ToArray());
            },
            "Only accepted template winners are returned; rejected suffix candidates and pixel distances are not exposed."),
        DetectorInspectionDefinitionFactory.Create(
            "bounty.no-gold",
            "Bounties",
            "Insufficient Gold banner",
            "Matches the bounded alert text template and its independent backdrop.",
            DetectorInspectionDetailLevel.Partial,
            [typeof(BountyNoGoldRecognizer)],
            image =>
            {
                double score =
                    BountyNoGoldRecognizer.Score(image);
                return DetectorProbeResult.Create(
                    score > 0
                        ? "No Gold"
                        : "None",
                    score,
                    passed: score > 0,
                    metrics:
                    [
                        Number("score", score),
                    ]);
            }),
        DetectorInspectionDefinitionFactory.Create(
            "bounty.wave-counter",
            "Bounties",
            "Wave counter",
            "Recognizes either bounded wave-counter layout and requires independent gameplay-HUD ownership.",
            DetectorInspectionDetailLevel.Detailed,
            [
                typeof(WaveCounterOwnerDetector),
                typeof(WaveCounterRecognizer),
                typeof(StageGameplayHudDetector),
            ],
            image =>
            {
                WaveCounterMatch? match =
                    WaveCounterRecognizer.Detect(image);
                StageGameplayHudMatch gameplayHud =
                    StageGameplayHudDetector.Detect(
                        image);
                bool passed =
                    match is not null &&
                    gameplayHud.Visible;
                return DetectorProbeResult.Create(
                    match is null
                        ? "None"
                        : gameplayHud.Visible
                            ? $"Wave {match.Value.Wave}"
                            : $"Wave {match.Value.Wave} (HUD unowned)",
                    match is null
                        ? gameplayHud.Confidence
                        : Math.Min(
                            match.Value.Confidence,
                            gameplayHud.Confidence),
                    passed: passed,
                    metrics: match is null
                        ?
                        [
                            new DetectorProbeMetric(
                                "gameplay_hud",
                                gameplayHud.Visible
                                    ? "true"
                                    : "false",
                                BooleanValue:
                                    gameplayHud.Visible),
                        ]
                        :
                        [
                            new DetectorProbeMetric(
                                "wave",
                                match.Value.Wave.ToString(
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                match.Value.Wave),
                            new DetectorProbeMetric(
                                "distance",
                                match.Value.Distance.ToString(
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture),
                                match.Value.Distance),
                            new DetectorProbeMetric(
                                "margin",
                                match.Value.Margin.ToString(
                                    System.Globalization
                                        .CultureInfo
                                    .InvariantCulture),
                                match.Value.Margin),
                            new DetectorProbeMetric(
                                "gameplay_hud",
                                gameplayHud.Visible
                                    ? "true"
                                    : "false",
                                BooleanValue:
                                    gameplayHud.Visible),
                        ]);
            },
            "Both production counter regions, the accepted wave, winning distance, margin, and gameplay-HUD gate are exposed. The complete 0 through 100 candidate table is intentionally omitted."),
    ];

    private static (int X, int Y)? LiveBountyAction(
        ExpeditionsMacro.Core.Imaging.ImageFrame image,
        BountyBoardMatch match)
    {
        if (match.EventAction is { } eventAction)
        {
            return eventAction;
        }
        if (match.Actions.FirstOrDefault() is
            BountyCardAction card &&
            card != default)
        {
            return (card.X, card.Y);
        }
        return match.State ==
               BountyBoardState.RerollConfirmation
            ? BountyBoardDetector.RerollConfirmAction(
                image)
            : null;
    }

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
