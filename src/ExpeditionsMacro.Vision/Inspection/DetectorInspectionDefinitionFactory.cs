using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Inspection;

internal sealed record DetectorCatalogEntry(
    DetectorInspectionDefinition Definition,
    IReadOnlyList<Type> Owners);

internal static class DetectorInspectionDefinitionFactory
{
    public static DetectorCatalogEntry Create(
        string id,
        string group,
        string name,
        string description,
        DetectorInspectionDetailLevel detailLevel,
        IReadOnlyList<Type> owners,
        Func<ImageFrame, DetectorProbeResult> evaluate,
        string? limitation = null)
    {
        IReadOnlyList<DetectorInspectionRegion> regions =
            ProductionDetectorMetadata.ReadRegions(owners);
        DetectorInspectionDefinition definition = new(
            id,
            group,
            name,
            description,
            detailLevel,
            limitation,
            owners
                .Select(owner =>
                    owner.FullName ?? owner.Name)
                .ToArray(),
            image => DetectorInspectionProbe.Run(
                image,
                owners,
                detailLevel,
                limitation,
                () => evaluate(image)),
            regions);
        return new DetectorCatalogEntry(
            definition,
            owners);
    }

    public static DetectorCatalogEntry Unavailable(
        Type owner,
        string? limitation = null)
    {
        string group = FriendlyGroup(owner);
        string name =
            ProductionDetectorMetadata.FriendlyName(
                owner.Name);
        string reason = limitation ??
            "This helper is used inside another production detector, but it does not expose a stable standalone result and typed check contract.";
        IReadOnlyList<Type> owners = [owner];
        return new DetectorCatalogEntry(
            new DetectorInspectionDefinition(
                $"unavailable.{owner.FullName}"
                    .ToLowerInvariant(),
                group,
                name,
                "Production detector helper. Select it to review exactly what is and is not inspectable.",
                DetectorInspectionDetailLevel.Unavailable,
                reason,
                [owner.FullName ?? owner.Name],
                null,
                ProductionDetectorMetadata.ReadRegions(
                    owners)),
            owners);
    }

    private static string FriendlyGroup(Type type)
    {
        string segment =
            type.Namespace?
                .Split('.')
                .LastOrDefault() ??
            "Other";
        return segment switch
        {
            "Packs" => "Expeditions",
            "Stages" => "Story and Raid",
            "Teams" => "Teams",
            _ =>
                ProductionDetectorMetadata.FriendlyName(
                    segment),
        };
    }
}
