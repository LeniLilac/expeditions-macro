using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Vision.Inspection;

internal static class GameModeInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create(
        IDetectorPack? detectorPack) =>
        [
            .. ChallengeStageInspectionDefinitions.Create(),
            .. EventBountyInspectionDefinitions.Create(),
        ];
}
