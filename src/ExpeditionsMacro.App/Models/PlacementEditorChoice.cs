using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed record PlacementEditorChoice<T>(
    T Value,
    string Label);

public sealed record PlacementStoryVariant(
    StoryRunKind RunKind,
    int ActNumber,
    string Label);

public sealed record PlacementSetupRow(
    PlacementSetupRoute Route,
    PlacementModel? Model,
    string? InheritedFrom = null)
{
    public string Name => Route.Name;

    public string Status => Model is not null
        ? $"{Model.Steps.Count} placement{(Model.Steps.Count == 1 ? string.Empty : "s")} · {(Model.TeamSlot == 0 ? "Team unchanged" : $"Team {Model.TeamSlot}")}"
        : InheritedFrom is not null
            ? $"Uses {InheritedFrom}"
            : "Not configured";
}

public sealed class PlacementSetupNode
{
    public PlacementSetupNode(
        string name,
        PlacementSetupRow row,
        bool isGroup = false,
        bool isChild = false)
    {
        Name = name;
        Row = row;
        IsGroup = isGroup;
        IsChild = isChild;
    }

    public string Name { get; }

    public PlacementSetupRow Row { get; }

    public bool IsGroup { get; }

    public bool IsChild { get; }

    public bool IsExpanded { get; set; }

    public List<PlacementSetupNode> Children { get; } = [];

    public string Status => Row.Status;
}
