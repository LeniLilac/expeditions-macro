using System.IO;
using System.Windows;
using System.Windows.Input;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void PlacementCanvas_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (PlacementScreenshot.Source is null)
        {
            return;
        }
        Point point = e.GetPosition(PlacementCanvas);
        int x = Math.Clamp((int)Math.Round(point.X), 0, 807);
        int y = Math.Clamp((int)Math.Round(point.Y), 0, 610);
        string? placementError =
            PlacementSafetyRules.IsInsideFixedCentralHotbar(x, y)
                ? "That point is inside the fixed center unit hotbar. Choose a map point outside the bottom hotbar."
                : CurrentFastTarget().Mode == PlacementTargetMode.Expedition &&
                    _steps.Any(step =>
                        step.Kind ==
                            MatchStepKind.Placement &&
                        step.UnitKey == _selectedFastUnit)
                    ? $"Expedition setups allow one placement for Unit {_selectedFastUnit}. Remove its existing point before adding another."
                    : null;
        if (placementError is not null)
        {
            FastStatusText.Text = placementError;
            e.Handled = true;
            return;
        }
        PlacementStepRow? nearby =
            _steps.FirstOrDefault(
                step =>
                    step.Kind ==
                        MatchStepKind.Placement &&
                    !PlacementAuthoringRules.AreSeparated(
                        x,
                        y,
                        step.X,
                        step.Y));
        if (nearby is not null)
        {
            FastStatusText.Text =
                $"Choose a point at least {PlacementAuthoringRules.MinimumPlacementSpacingPixels} pixels from the existing placement at ({nearby.X}, {nearby.Y}).";
            e.Handled = true;
            return;
        }
        bool coveredByStart =
            PlacementAuthoringRules
                .IsCoveredByStartDialog(x, y);
        PlacementStepRow row = new()
        {
            Kind = MatchStepKind.Placement,
            PlacementId =
                PlacementReferencePolicy
                    .CreatePlacementId(),
            UnitKey = _selectedFastUnit,
            X = x,
            Y = y,
            Phase = coveredByStart
                ? PlacementPhase.AfterStart
                : PlacementPhase.BeforeStart,
            DelayAfterMilliseconds =
                _fastPlacementIntervalMilliseconds,
            AutoUpgradePriority =
                PlacementAuthoringRules
                    .DefaultAutoUpgradePriority,
        };
        int start = StartGameRowIndex();
        int insertion = coveredByStart
            ? start + 1
            : start;
        List<PlacementStep> prospective =
            _steps.Select(step => step.ToModel())
                .ToList();
        prospective.Insert(insertion, row.ToModel());
        try
        {
            ValidateTimeline(prospective);
        }
        catch (Exception error) when (
            error is InvalidDataException or
            InvalidOperationException)
        {
            FastStatusText.Text = error.Message;
            e.Handled = true;
            return;
        }
        using (SuspendPlacementAutoSave())
        {
            _steps.Insert(insertion, row);
            NormalizeTimelineRows();
        }
        FastStepsList.SelectedItem = row;
        FastStepsList.ScrollIntoView(row);
        FastStatusText.Text =
            $"Added Unit {_selectedFastUnit} at ({x}, {y}) {(coveredByStart ? "below" : "above")} Start Game.";
        SchedulePlacementAutoSave();
        e.Handled = true;
    }

    private void PlacementMarker_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                        PlacementStepRow row,
            })
        {
            FastStepsList.SelectedItem = row;
            FastStepsList.ScrollIntoView(row);
            e.Handled = true;
        }
    }

    private void PlacementMarker_MouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_services.Coordinator.IsBusy ||
            sender is not FrameworkElement
            {
                DataContext:
                    PlacementStepRow row,
            })
        {
            return;
        }
        FastStepsList.SelectedItem = row;
        RemoveSelectedPlacementStep();
        e.Handled = true;
    }
}
