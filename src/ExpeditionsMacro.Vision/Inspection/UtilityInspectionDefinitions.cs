namespace ExpeditionsMacro.Vision.Inspection;

internal static class UtilityInspectionDefinitions
{
    public static IReadOnlyList<DetectorCatalogEntry> Create() =>
    [
        .. NavigationSettingsInspectionDefinitions.Create(),
        .. TeamPlacementRefuelInspectionDefinitions.Create(),
    ];
}
