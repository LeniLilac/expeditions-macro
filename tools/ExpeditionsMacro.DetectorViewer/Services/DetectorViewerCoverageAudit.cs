using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed record DetectorCoverageRow(
    string ProductionType,
    IReadOnlyList<string> CatalogItems,
    bool HasGeometry,
    bool HasChecks,
    bool HasThresholds,
    bool HasExplicitLimitation,
    string Detail);

public sealed record UnavailableDetectorDetail(
    string CatalogId,
    IReadOnlyList<string> ProductionOwners,
    string Reason);

public sealed record DetectorCoverageReport(
    int DiscoveredProductionTypes,
    int PublicProductionEntryPoints,
    int CatalogItems,
    int GeometryCapableItems,
    int CheckCapableItems,
    int ThresholdBearingItems,
    int UnavailableDetailItems,
    IReadOnlyList<UnavailableDetectorDetail>
        UnavailableDetails,
    IReadOnlyList<DetectorCoverageRow> Rows);

public static class DetectorViewerCoverageAudit
{
    private static readonly string[]
        RequiredProductionOwners =
        [
            "ExpeditionsMacro.Vision.Bounties.BountyBoardActionDetector",
            "ExpeditionsMacro.Vision.Bounties.BountyBoardDetector",
            "ExpeditionsMacro.Vision.Bounties.BountyBoardEventEntryDetector",
            "ExpeditionsMacro.Vision.Bounties.BountyBoardHeaderRecognizer",
            "ExpeditionsMacro.Vision.Bounties.BountyBoardOwnerDetector",
            "ExpeditionsMacro.Vision.Bounties.BountyNoGoldRecognizer",
            "ExpeditionsMacro.Vision.Bounties.BountyNumberRecognizer",
            "ExpeditionsMacro.Vision.Bounties.WaveCounterRecognizer",
            "ExpeditionsMacro.Vision.Bounties.WaveCounterOwnerDetector",
            "ExpeditionsMacro.Vision.Challenges.ChallengeMapDetector",
            "ExpeditionsMacro.Vision.Challenges.ChallengeMatchStateDetector",
            "ExpeditionsMacro.Vision.Challenges.ChallengeScreenDetector",
            "ExpeditionsMacro.Vision.Challenges.ChallengeVictoryDetector",
            "ExpeditionsMacro.Vision.Events.EventActAnchorDetector",
            "ExpeditionsMacro.Vision.Events.EventEntryDetector",
            "ExpeditionsMacro.Vision.Events.EventScreenDetector",
            "ExpeditionsMacro.Vision.Navigation.LobbyExitConfirmationDetector",
            "ExpeditionsMacro.Vision.Navigation.MatchLobbyDoorButtonDetector",
            "ExpeditionsMacro.Vision.Navigation.RobloxChatButtonDetector",
            "ExpeditionsMacro.Vision.Packs.ActionButtonDetector",
            "ExpeditionsMacro.Vision.Packs.AdaptiveUiMatcher",
            "ExpeditionsMacro.Vision.Packs.AfkChamberDetector",
            "ExpeditionsMacro.Vision.Packs.CompiledDetectorPack",
            "ExpeditionsMacro.Vision.Packs.ExpeditionSelectorDetector",
            "ExpeditionsMacro.Vision.Packs.MapSelectionDetector",
            "ExpeditionsMacro.Vision.Packs.PauseButtonDetector",
            "ExpeditionsMacro.Vision.Packs.PlayScreenDetector",
            "ExpeditionsMacro.Vision.Packs.RewardScreenDetector",
            "ExpeditionsMacro.Vision.Packs.StartDialogDetector",
            "ExpeditionsMacro.Vision.Packs.TerminalScreenDetector",
            "ExpeditionsMacro.Vision.Placement.SelectedUnitPanelDetector",
            "ExpeditionsMacro.Vision.Placement.UpgradeUnitReadinessDetector",
            "ExpeditionsMacro.Vision.Refuel.AddFuelDialogDetector",
            "ExpeditionsMacro.Vision.Refuel.AreasScreenDetector",
            "ExpeditionsMacro.Vision.Refuel.RefuelVisionMetrics",
            "ExpeditionsMacro.Vision.Refuel.ResourceStationScreenDetector",
            "ExpeditionsMacro.Vision.Settings.GameSettingsNavigationDetector",
            "ExpeditionsMacro.Vision.Settings.GameSettingsScreenDetector",
            "ExpeditionsMacro.Vision.Settings.GameSettingsTabRailDetector",
            "ExpeditionsMacro.Vision.Settings.GameSettingsVisionMetrics",
            "ExpeditionsMacro.Vision.Settings.RobloxSettingsButtonDetector",
            "ExpeditionsMacro.Vision.Stages.StageGameplayHudDetector",
            "ExpeditionsMacro.Vision.Stages.StageOptionSelectionDetector",
            "ExpeditionsMacro.Vision.Stages.StageScreenDetector",
            "ExpeditionsMacro.Vision.Teams.TeamScreenDetector",
            "ExpeditionsMacro.Vision.TranslationAwareImageScorer",
            "ExpeditionsMacro.Vision.VisionScorer",
        ];

    public static DetectorCoverageReport Run(
        DetectorInspectionCatalogResult catalog,
        ImageFrame canonicalFrame)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(
            canonicalFrame);
        Dictionary<string, DetectorInspectionReport>
            reports = Evaluate(catalog, canonicalFrame);
        Assembly visionAssembly =
            typeof(DetectorInspectionCatalog).Assembly;
        Dictionary<string, Type> visionTypes =
            visionAssembly
                .GetTypes()
                .Where(type =>
                    type.FullName is not null)
                .ToDictionary(
                    type => type.FullName!,
                    StringComparer.Ordinal);
        Type[] productionTypes =
            RequiredProductionOwners
                .Select(name =>
                    visionTypes.TryGetValue(
                        name,
                        out Type? type)
                        ? type
                        : throw new InvalidDataException(
                            $"The explicit detector-inspection owner '{name}' no longer exists."))
                .OrderBy(type =>
                    type.FullName)
                .ToArray();
        string[] catalogOwners =
            catalog.Definitions
                .SelectMany(definition =>
                    definition.ProductionOwners)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        string[] unexpectedOwners =
            catalogOwners
                .Except(
                    RequiredProductionOwners,
                    StringComparer.Ordinal)
                .ToArray();
        string[] missingOwners =
            RequiredProductionOwners
                .Except(
                    catalogOwners,
                    StringComparer.Ordinal)
                .ToArray();
        if (catalog.ProductionDetectorCount !=
                productionTypes.Length ||
            unexpectedOwners.Length > 0 ||
            missingOwners.Length > 0)
        {
            throw new InvalidDataException(
                $"Catalog discovery reported {catalog.ProductionDetectorCount} production decision owners, but the explicit coverage manifest contains {productionTypes.Length}. Unexpected: {List(unexpectedOwners)}. Missing: {List(missingOwners)}.");
        }
        List<DetectorCoverageRow> rows = [];
        foreach (Type type in productionTypes)
        {
            string fullName =
                type.FullName ??
                type.Name;
            DetectorInspectionDefinition[] items =
                catalog.Definitions
                    .Where(definition =>
                        definition.ProductionOwners
                            .Contains(
                                fullName,
                                StringComparer.Ordinal))
                    .ToArray();
            if (items.Length == 0)
            {
                throw new InvalidDataException(
                    $"Public production entry point '{fullName}' is missing from the detector catalog.");
            }
            bool geometry =
                items.Any(item =>
                    item.Regions.Count > 0);
            bool checks =
                items.Any(item =>
                    reports.TryGetValue(
                        item.Id,
                        out DetectorInspectionReport? report) &&
                    report.Checks.Count > 0);
            bool thresholds =
                items.Any(item =>
                    reports.TryGetValue(
                        item.Id,
                        out DetectorInspectionReport? report) &&
                    HasProductionThreshold(report));
            bool limitation =
                items.All(item =>
                    !string.IsNullOrWhiteSpace(
                        item.Limitation) ||
                    reports.TryGetValue(
                        item.Id,
                        out DetectorInspectionReport? report) &&
                    report.Checks.Any(check =>
                        check.Status ==
                        DetectorInspectionCheckStatus
                            .NotExposed));
            if (!geometry &&
                !checks &&
                !limitation)
            {
                throw new InvalidDataException(
                    $"Catalog coverage for '{fullName}' exposes neither production evidence nor an explicit not-exposed reason.");
            }
            rows.Add(new DetectorCoverageRow(
                fullName,
                items.Select(item =>
                        item.Id)
                    .ToArray(),
                geometry,
                checks,
                thresholds,
                limitation,
                BuildDetail(
                    items,
                    geometry,
                    checks,
                    thresholds)));
        }

        UnavailableDetectorDetail[] unavailable =
            catalog.Definitions
                .Where(item =>
                    item.DetailLevel ==
                    DetectorInspectionDetailLevel
                        .Unavailable)
                .Select(item =>
                    new UnavailableDetectorDetail(
                        item.Id,
                        item.ProductionOwners,
                        item.Limitation ??
                        "Not exposed reason missing."))
                .ToArray();
        if (unavailable.Any(item =>
                string.IsNullOrWhiteSpace(
                    item.Reason) ||
                item.Reason.Equals(
                    "Not exposed reason missing.",
                    StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Every unavailable detector catalog item must include an explicit not-exposed reason.");
        }

        return new DetectorCoverageReport(
            productionTypes.Length,
            productionTypes.Count(type =>
                type.IsPublic),
            catalog.Definitions.Count,
            catalog.Definitions.Count(item =>
                item.Regions.Count > 0),
            catalog.Definitions.Count(item =>
                reports.TryGetValue(
                    item.Id,
                    out DetectorInspectionReport? report) &&
                report.Checks.Count > 0),
            catalog.Definitions.Count(item =>
                reports.TryGetValue(
                    item.Id,
                    out DetectorInspectionReport? report) &&
                HasProductionThreshold(report)),
            unavailable.Length,
            unavailable,
            rows);
    }

    public static async Task WriteAsync(
        DetectorCoverageReport report,
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            directory);
        Directory.CreateDirectory(directory);
        JsonSerializerOptions options =
            new()
            {
                WriteIndented = true,
            };
        await File.WriteAllTextAsync(
            Path.Combine(
                directory,
                "detector-catalog-coverage.json"),
            JsonSerializer.Serialize(
                report,
                options),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(
                directory,
                "detector-catalog-coverage.md"),
            ToMarkdown(report),
            cancellationToken);
    }

    private static Dictionary<
        string,
        DetectorInspectionReport> Evaluate(
        DetectorInspectionCatalogResult catalog,
        ImageFrame frame)
    {
        Dictionary<string, DetectorInspectionReport>
            reports =
                new(StringComparer.OrdinalIgnoreCase);
        foreach (DetectorInspectionDefinition definition in
                 catalog.Definitions)
        {
            try
            {
                reports[definition.Id] =
                    definition.Evaluate(frame);
            }
            catch (Exception error)
            {
                throw new InvalidDataException(
                    $"Detector catalog entry '{definition.Id}' failed the canonical smoke frame.",
                    error);
            }
        }
        return reports;
    }

    private static bool HasProductionThreshold(
        DetectorInspectionReport report) =>
        IsExplicitGate(report.DecisionThreshold) ||
        report.Checks.Any(check =>
            IsExplicitGate(check.Threshold) ||
            check.Threshold.Equals(
                "Boolean result",
                StringComparison.Ordinal));

    private static bool IsExplicitGate(string value) =>
        value.StartsWith(
            ">=",
            StringComparison.Ordinal) ||
        value.StartsWith(
            "<=",
            StringComparison.Ordinal);

    private static string BuildDetail(
        IReadOnlyList<DetectorInspectionDefinition> items,
        bool geometry,
        bool checks,
        bool thresholds)
    {
        string exposed =
            string.Join(
                ", ",
                new[]
                    {
                        geometry
                            ? "production geometry"
                            : null,
                        checks
                            ? "production result/checks"
                            : null,
                        thresholds
                            ? "graded gates"
                            : null,
                    }
                    .Where(value =>
                        value is not null));
        string[] limitations =
            items
                .Select(item =>
                    item.Limitation)
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .Cast<string>()
                .ToArray();
        if (limitations.Length == 0)
        {
            return exposed;
        }
        return $"{exposed}; not exposed: {string.Join(" | ", limitations)}"
            .TrimStart(
                ';',
                ' ');
    }

    private static string ToMarkdown(
        DetectorCoverageReport report)
    {
        StringBuilder text = new();
        text.AppendLine(
            "# Detector catalog coverage");
        text.AppendLine();
        text.AppendLine(
            $"Discovered production detector types: {report.DiscoveredProductionTypes}");
        text.AppendLine(
            $"Public production entry points: {report.PublicProductionEntryPoints}");
        text.AppendLine(
            $"Catalog items: {report.CatalogItems}");
        text.AppendLine(
            $"Geometry-capable items: {report.GeometryCapableItems}");
        text.AppendLine(
            $"Check-capable items: {report.CheckCapableItems}");
        text.AppendLine(
            $"Threshold-bearing items: {report.ThresholdBearingItems}");
        text.AppendLine(
            $"Unavailable-detail items: {report.UnavailableDetailItems}");
        text.AppendLine();
        text.AppendLine(
            "## Explicit unavailable-detail reasons");
        text.AppendLine();
        foreach (UnavailableDetectorDetail detail in
                 report.UnavailableDetails)
        {
            text.AppendLine(
                $"- `{detail.CatalogId}`: {detail.Reason}");
        }
        text.AppendLine();
        text.AppendLine(
            "## Production detector coverage");
        text.AppendLine();
        text.AppendLine(
            "| Production detector type | Catalog items | Geometry | Checks | Gates | Explicit limitation |");
        text.AppendLine(
            "| --- | ---: | :---: | :---: | :---: | :---: |");
        foreach (DetectorCoverageRow row in report.Rows)
        {
            text.AppendLine(
                $"| `{row.ProductionType}` | {row.CatalogItems.Count} | {Mark(row.HasGeometry)} | {Mark(row.HasChecks)} | {Mark(row.HasThresholds)} | {Mark(row.HasExplicitLimitation)} |");
        }
        return text.ToString();
    }

    private static string Mark(bool value) =>
        value
            ? "yes"
            : "no";

    private static string List(
        IReadOnlyList<string> values) =>
        values.Count == 0
            ? "none"
            : string.Join(
                ", ",
                values);
}
