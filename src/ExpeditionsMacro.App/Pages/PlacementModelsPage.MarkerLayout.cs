using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private const int MarkerLabelHeight = 24;
    private const int MinimumMarkerLabelWidth = 48;

    private void UpdatePlacementMarkerLayout()
    {
        IReadOnlyList<PlacementStep> steps =
            PlacementTimelinePolicy.NormalizeSteps(
                _steps.Select(row => row.ToModel())
                    .ToArray());
        IReadOnlyDictionary<string, string> labels =
            PlacementReferencePolicy
                .BuildDisplayLabels(steps);
        for (int index = 0;
             index < _steps.Count;
             index++)
        {
            PlacementStepRow row = _steps[index];
            PlacementStep step = steps[index];
            string placementId =
                row.Kind == MatchStepKind.Placement
                    ? step.PlacementId
                    : row.HasPlacementReference
                        ? step.TargetPlacementId
                        : string.Empty;
            row.SetDisplayUnitId(
                labels.GetValueOrDefault(
                    placementId,
                    string.Empty));
        }

        _placementMarkers.Clear();
        foreach (PlacementStepRow row in
                 _steps.Where(row =>
                     row.Kind ==
                     MatchStepKind.Placement))
        {
            _placementMarkers.Add(row);
        }

        PlacementMarkerLabelRequest[] requests =
            _steps.Select(
                    (row, index) =>
                        new
                        {
                            Row = row,
                            Index = index,
                        })
                .Where(item =>
                    item.Row.HasCoordinate)
                .Select(item =>
                        new PlacementMarkerLabelRequest(
                            item.Index,
                            item.Row.X,
                            item.Row.Y,
                            MarkerLabelWidth(item.Row),
                            MarkerLabelHeight))
                .ToArray();
        IReadOnlyDictionary<
            int,
            PlacementMarkerLabelPlacement> placements =
            PlacementMarkerLabelLayout
                .Arrange(requests)
                .ToDictionary(
                    placement => placement.Key,
                    placement => placement);
        for (int index = 0;
             index < _steps.Count;
             index++)
        {
            PlacementStepRow row = _steps[index];
            if (!row.HasCoordinate)
            {
                row.SetMarkerLayout(
                    PlacementMarkerPresentation.Empty);
                continue;
            }
            row.SetMarkerLayout(
                PlacementMarkerPresentation.Create(
                    row.X,
                    row.Y,
                    placements[index]));
        }
    }

    private static int MarkerLabelWidth(
        PlacementStepRow row)
    {
        int estimatedTextWidth =
            row.MarkerLabel.Length * 8;
        return Math.Max(
            MinimumMarkerLabelWidth,
            estimatedTextWidth + 26);
    }

    internal void SetDenseMarkerSnapshot()
    {
        if (_steps.Count < 3)
        {
            return;
        }

        using IDisposable suspension =
            SuspendPlacementAutoSave();
        (int X, int Y)[] positions =
        [
            (398, 350),
            (410, 362),
            (422, 374),
        ];
        for (int index = 0;
             index < positions.Length;
             index++)
        {
            _steps[index].X = positions[index].X;
            _steps[index].Y = positions[index].Y;
        }
        UpdatePlacementMarkerLayout();
    }
}
