using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Inspection;

public enum DetectorInspectionDetailLevel
{
    Detailed,
    Partial,
    ResultOnly,
    Unavailable,
}

public enum DetectorInspectionCheckStatus
{
    Passed,
    Failed,
    Observed,
    NotExposed,
}

public sealed record DetectorInspectionPoint(
    int X,
    int Y,
    bool IsLive,
    string Provenance);

public sealed record DetectorInspectionRegion(
    string Id,
    string Label,
    ScreenRegion Region,
    string Expected);

public sealed record DetectorInspectionCheck(
    string Id,
    string Label,
    string Expected,
    string Measured,
    string Threshold,
    DetectorInspectionCheckStatus Status,
    IReadOnlyList<string> RegionIds);

public sealed record DetectorInspectionReport(
    string FinalState,
    double? Confidence,
    string DecisionThreshold,
    bool? Passed,
    DetectorInspectionPoint? Action,
    IReadOnlyList<DetectorInspectionRegion> Regions,
    IReadOnlyList<DetectorInspectionCheck> Checks,
    IReadOnlyList<string> Notes)
{
    public static DetectorInspectionReport Unavailable(
        string limitation,
        IReadOnlyList<DetectorInspectionRegion> regions) =>
        new(
            "Detailed inspection unavailable",
            null,
            "Not exposed",
            null,
            null,
            regions,
            regions
                .Select(region => new DetectorInspectionCheck(
                    $"region:{region.Id}",
                    region.Label,
                    region.Expected,
                    "Geometry is exposed; the production measurement is not.",
                    "Detector-owned",
                    DetectorInspectionCheckStatus.NotExposed,
                    [region.Id]))
                .ToArray(),
            [limitation]);
}

public sealed class DetectorInspectionDefinition
{
    private readonly Func<ImageFrame, DetectorInspectionReport>? _evaluate;

    internal DetectorInspectionDefinition(
        string id,
        string group,
        string name,
        string description,
        DetectorInspectionDetailLevel detailLevel,
        string? limitation,
        IReadOnlyList<string> productionOwners,
        Func<ImageFrame, DetectorInspectionReport>? evaluate,
        IReadOnlyList<DetectorInspectionRegion> regions)
    {
        Id = id;
        Group = group;
        Name = name;
        Description = description;
        DetailLevel = detailLevel;
        Limitation = limitation;
        ProductionOwners = productionOwners;
        _evaluate = evaluate;
        Regions = regions;
    }

    public string Id { get; }

    public string Group { get; }

    public string Name { get; }

    public string Description { get; }

    public DetectorInspectionDetailLevel DetailLevel { get; }

    public string? Limitation { get; }

    public IReadOnlyList<string> ProductionOwners { get; }

    public IReadOnlyList<DetectorInspectionRegion> Regions { get; }

    public bool CanEvaluate => _evaluate is not null;

    public DetectorInspectionReport Evaluate(ImageFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return _evaluate?.Invoke(image) ??
            DetectorInspectionReport.Unavailable(
                Limitation ??
                "This production detector does not expose a standalone inspection path.",
                Regions);
    }
}

public sealed record DetectorInspectionCatalogResult(
    IReadOnlyList<DetectorInspectionDefinition> Definitions,
    int ProductionDetectorCount,
    int EvaluableDetectorCount,
    int UnavailableDetectorCount);
