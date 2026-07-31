using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Inspection;

internal static class CompiledPackInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create(
        IDetectorPack? pack)
    {
        IReadOnlyList<Type> stateOwners =
        [
            typeof(CompiledDetectorPack),
            typeof(AfkChamberDetector),
            typeof(ExpeditionSelectorDetector),
            typeof(MapSelectionDetector),
            typeof(PauseButtonDetector),
            typeof(PlayScreenDetector),
            typeof(RewardScreenDetector),
            typeof(StartDialogDetector),
            typeof(TerminalScreenDetector),
        ];
        if (pack is null)
        {
            return
            [
                DetectorInspectionDefinitionFactory
                    .Unavailable(
                        typeof(CompiledDetectorPack),
                        "The bundled detector pack could not be loaded. Reinstall or republish the viewer with its adjacent DetectorPacks directory."),
            ];
        }

        List<DetectorCatalogEntry> entries =
        [
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.state-classifier",
                "Expeditions",
                "State classifier",
                "Scores every configured Expedition state, applies production priority, and shows the selected advisory action.",
                DetectorInspectionDetailLevel.Detailed,
                stateOwners,
                image => StateResult(pack, image)),
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.recovery-state",
                "Expeditions",
                "Recovery state",
                "Runs the production recovery-state subset used by Expedition navigation.",
                DetectorInspectionDetailLevel.ResultOnly,
                [typeof(CompiledDetectorPack)],
                image => NamedState(
                    pack,
                    image,
                    pack.RecoveryState(image),
                    "Recovery state")),
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.root-recovery-state",
                "Expeditions",
                "Root recovery state",
                "Restricts production recovery classification to AFK, disconnect, or Lobby.",
                DetectorInspectionDetailLevel.ResultOnly,
                [typeof(CompiledDetectorPack)],
                image => NamedState(
                    pack,
                    image,
                    pack.RootRecoveryState(image),
                    "Root recovery state")),
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.selected-map",
                "Expeditions",
                "Selected map",
                "Runs the production selected-map detector and shows manifest-owned selection geometry.",
                DetectorInspectionDetailLevel.Partial,
                [
                    typeof(CompiledDetectorPack),
                    typeof(MapSelectionDetector),
                ],
                image => SelectedValue(
                    "Map",
                    pack.SelectedMap(image))),
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.selected-difficulty",
                "Expeditions",
                "Selected difficulty",
                "Runs the production selected-difficulty detector, including the current hue path.",
                DetectorInspectionDetailLevel.Partial,
                [typeof(CompiledDetectorPack)],
                image => SelectedValue(
                    "Difficulty",
                    pack.SelectedDifficulty(image))),
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.node-type",
                "Expeditions",
                "Current node type",
                "Classifies the current Expedition node from production hue and structural evidence.",
                DetectorInspectionDetailLevel.ResultOnly,
                [typeof(CompiledDetectorPack)],
                image =>
                {
                    string? node =
                        pack.CurrentNodeType(image);
                    return DetectorProbeResult.Create(
                        node ?? "None",
                        passed: node is not null,
                        metrics:
                        [
                            new DetectorProbeMetric(
                                "node_type",
                                node ?? "No node"),
                        ]);
                }),
            DetectorInspectionDefinitionFactory.Create(
                "expeditions.remaining-units",
                "Expeditions",
                "Remaining unit keys",
                "Checks canonical hotbar slots 1 through 6 through the production pack.",
                DetectorInspectionDetailLevel.ResultOnly,
                [typeof(CompiledDetectorPack)],
                image =>
                {
                    IReadOnlyList<int> remaining =
                        pack.RemainingUnitKeys(
                            image,
                            new HashSet<int>(
                                Enumerable.Range(1, 6)));
                    return DetectorProbeResult.Create(
                        remaining.Count == 0
                            ? "No remaining units"
                            : string.Join(", ", remaining),
                        passed: remaining.Count > 0,
                        metrics:
                        [
                            new DetectorProbeMetric(
                                "remaining_unit_count",
                                remaining.Count.ToString(
                                    System.Globalization
                                        .CultureInfo.InvariantCulture),
                                remaining.Count),
                        ]);
                }),
        ];
        foreach (ChallengeType type in
                 Enum.GetValues<ChallengeType>())
        {
            ChallengeType captured = type;
            entries.Add(
                DetectorInspectionDefinitionFactory.Create(
                    $"challenge.map.{captured.ToString().ToLowerInvariant()}",
                    "Challenges",
                    $"Challenge map, {ProductionDetectorMetadata.FriendlyName(captured.ToString())}",
                    "Runs the production challenge-map reference matcher for one live Challenge row.",
                    DetectorInspectionDetailLevel.ResultOnly,
                    [
                        typeof(CompiledDetectorPack),
                        typeof(ChallengeMapDetector),
                    ],
                    image =>
                    {
                        ChallengeMapId? map =
                            pack.ChallengeMapForType(
                                image,
                                captured);
                        return DetectorProbeResult.Create(
                            map?.ToString() ?? "None",
                            passed: map is not null,
                            metrics:
                            [
                                new DetectorProbeMetric(
                                    "challenge_type",
                                    captured.ToString()),
                                new DetectorProbeMetric(
                                    "map",
                                    map?.ToString() ??
                                    "No accepted map"),
                            ]);
                    },
                    "The detector-pack interface exposes the accepted map but not its complete candidate score table."));
        }
        return entries;
    }

    private static DetectorProbeResult StateResult(
        IDetectorPack pack,
        ImageFrame image)
    {
        IReadOnlyDictionary<string, double> scores =
            pack.ScoreStates(image);
        string? state = pack.Classify(scores);
        List<DetectorProbeMetric> metrics = [];
        foreach ((string name, double score) in scores)
        {
            double? threshold =
                pack.Manifest.States
                    .FirstOrDefault(candidate =>
                        candidate.Name.Equals(
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    ?.Threshold;
            if (name.Equals(
                    "afk",
                    StringComparison.OrdinalIgnoreCase))
            {
                threshold = AfkChamberDetector.Threshold;
            }
            metrics.Add(new DetectorProbeMetric(
                $"state.{name}",
                score.ToString(
                    "0.000",
                    System.Globalization
                        .CultureInfo.InvariantCulture),
                score,
                GateValue: threshold));
        }
        (int X, int Y)? action =
            state is null
                ? null
                : TryAction(pack, state, image);
        double? confidence =
            state is not null &&
            scores.TryGetValue(
                state,
                out double selected)
                ? selected
                : scores.Count == 0
                    ? null
                    : scores.Values.Max();
        double? decisionThreshold =
            state is null
                ? null
                : pack.Manifest.States
                    .FirstOrDefault(candidate =>
                        candidate.Name.Equals(
                            state,
                            StringComparison.OrdinalIgnoreCase))
                    ?.Threshold;
        return DetectorProbeResult.Create(
            state ?? "None",
            confidence,
            decisionThreshold,
            state is not null,
            action?.X,
            action?.Y,
            actionIsLive: false,
            actionProvenance:
                "Production pack action mapping; advisory unless the selected state owns current live geometry",
            metrics: metrics);
    }

    private static DetectorProbeResult NamedState(
        IDetectorPack pack,
        ImageFrame image,
        string? state,
        string metric)
    {
        (int X, int Y)? action =
            state is null
                ? null
                : TryAction(pack, state, image);
        return DetectorProbeResult.Create(
            state ?? "None",
            passed: state is not null,
            actionX: action?.X,
            actionY: action?.Y,
            actionIsLive: false,
            actionProvenance:
                "Production pack action mapping; advisory unless live owner geometry is available",
            metrics:
            [
                new DetectorProbeMetric(
                    metric,
                    state ?? "None"),
            ]);
    }

    private static DetectorProbeResult SelectedValue(
        string label,
        int? value) =>
        DetectorProbeResult.Create(
            value?.ToString(
                System.Globalization
                    .CultureInfo.InvariantCulture) ??
            "None",
            passed: value is not null,
            metrics:
            [
                new DetectorProbeMetric(
                    label,
                    value?.ToString(
                        System.Globalization
                            .CultureInfo.InvariantCulture) ??
                    "No accepted value"),
            ]);

    private static (int X, int Y)? TryAction(
        IDetectorPack pack,
        string state,
        ImageFrame image)
    {
        try
        {
            return pack.ActionFor(state, image);
        }
        catch (Exception error) when (
            error is KeyNotFoundException or
                ArgumentOutOfRangeException or
                InvalidOperationException)
        {
            return null;
        }
    }
}
