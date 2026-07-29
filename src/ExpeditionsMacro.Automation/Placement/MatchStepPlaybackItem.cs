using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed record MatchStepPlaybackItem(
    int SourceIndex,
    PlacementStep Step,
    PlacementStep? TargetPlacement)
{
    public PlacementStep Placement =>
        Step.Kind == MatchStepKind.Placement
            ? Step
            : TargetPlacement ??
              throw new InvalidDataException(
                  "The match action has no placement target.");

    public string PlacementId =>
        Placement.PlacementId;

    public int UnitKey => Placement.UnitKey;

    public int X => Placement.X;

    public int Y => Placement.Y;
}
