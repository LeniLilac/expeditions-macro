using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Models;

public sealed class DetectorCatalogItem
{
    public DetectorCatalogItem(
        DetectorInspectionDefinition definition)
    {
        Definition = definition;
        DetailLabel = definition.DetailLevel switch
        {
            DetectorInspectionDetailLevel.Detailed =>
                "Detailed",
            DetectorInspectionDetailLevel.Partial =>
                "Partial",
            DetectorInspectionDetailLevel.ResultOnly =>
                "Result only",
            _ => "Unavailable",
        };
        SearchText = string.Join(
                " ",
                definition.Group,
                definition.Name,
                definition.Description,
                definition.Limitation,
                string.Join(
                    " ",
                    definition.ProductionOwners))
            .ToLowerInvariant();
    }

    public DetectorInspectionDefinition Definition { get; }

    public string Id => Definition.Id;

    public string Group => Definition.Group;

    public string Name => Definition.Name;

    public string Description => Definition.Description;

    public string DetailLabel { get; }

    public bool IsUnavailable =>
        Definition.DetailLevel ==
        DetectorInspectionDetailLevel.Unavailable;

    internal string SearchText { get; }
}

public static class DetectorCatalogQuery
{
    public static IReadOnlyList<DetectorCatalogItem> Filter(
        IEnumerable<DetectorCatalogItem> items,
        string? query,
        bool includeUnavailable = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        string[] tokens =
            (query ?? string.Empty)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(token =>
                    token.ToLowerInvariant())
                .ToArray();
        return items
            .Where(item =>
                includeUnavailable ||
                !item.IsUnavailable)
            .Where(item =>
                tokens.All(item.SearchText.Contains))
            .OrderBy(item => item.Group)
            .ThenBy(item => item.IsUnavailable)
            .ThenBy(item => item.Name)
            .ToArray();
    }
}
