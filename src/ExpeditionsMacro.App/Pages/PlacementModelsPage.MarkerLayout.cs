using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private const int MarkerLabelHeight = 24;
    private const int MinimumMarkerLabelWidth = 48;

    private void UpdatePlacementMarkerLayout()
    {
        PlacementMarkerLabelRequest[] requests =
            _steps.Select(
                    (row, index) =>
                        new PlacementMarkerLabelRequest(
                            index,
                            row.X,
                            row.Y,
                            MarkerLabelWidth(row),
                            MarkerLabelHeight))
                .ToArray();
        IReadOnlyDictionary<int, ScreenRegion> labels =
            PlacementMarkerLabelLayout
                .Arrange(requests)
                .ToDictionary(
                    placement => placement.Key,
                    placement =>
                        placement.LabelBounds);
        for (int index = 0;
             index < _steps.Count;
             index++)
        {
            PlacementStepRow row = _steps[index];
            row.SetMarkerLayout(
                PlacementMarkerPresentation.Create(
                    row.X,
                    row.Y,
                    labels[index]));
        }
    }

    private static int MarkerLabelWidth(
        PlacementStepRow row)
    {
        int estimatedTextWidth =
            row.MarkerLabel.Length * 8 +
            row.PhaseShortLabel.Length * 7;
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
