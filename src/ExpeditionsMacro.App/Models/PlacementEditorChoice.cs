using System.ComponentModel;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed record PlacementEditorChoice<T>(
    T Value,
    string Label);

public sealed record PlacementStoryVariant(
    StoryRunKind RunKind,
    int ActNumber,
    string Label);

public sealed class PlacementSetupRow(
    PlacementSetupRoute route,
    PlacementModel? model,
    string? inheritedFrom = null) :
    INotifyPropertyChanged
{
    private PlacementModel? _model = model;
    private string? _inheritedFrom = inheritedFrom;

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public PlacementSetupRoute Route { get; } =
        route;

    public PlacementModel? Model => _model;

    public string? InheritedFrom =>
        _inheritedFrom;

    public string Name => Route.Name;

    public string Status => Model is not null
        ? $"{PlacementCount} placement{(PlacementCount == 1 ? string.Empty : "s")} · {(Model.TeamSlot == 0 ? "Team unchanged" : $"Team {Model.TeamSlot}")}"
        : InheritedFrom is not null
            ? $"Uses {InheritedFrom}"
            : "Not configured";

    public int PlacementCount =>
        Model?.Steps.Count(step =>
            step.Kind == MatchStepKind.Placement) ?? 0;

    public void UpdateModel(
        PlacementModel? model,
        string? inheritedFrom = null)
    {
        _model = model;
        _inheritedFrom = inheritedFrom;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(Model)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(InheritedFrom)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                nameof(Status)));
    }
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

    public string Status
    {
        get
        {
            if (!IsGroup) return Row.Status;

            int childrenWithPlacements = 0;

            foreach (PlacementSetupNode child in Children)
            {
                if (child.Row.PlacementCount <= 0) continue;
                childrenWithPlacements++;
            }

            if (childrenWithPlacements == 0) return "Not configured";
            if (childrenWithPlacements == Children.Count) return "All setups configured";

            return $"{childrenWithPlacements} of {Children.Count} setups configured";
        }
    }
}
