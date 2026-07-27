using System.Windows;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void RemoveRow_Click(
        object sender,
        RoutedEventArgs e) =>
        RemoveSelectedPlacementStep();

    private bool RemoveSelectedPlacementStep()
    {
        if (ActiveStepsSelector.SelectedItem is
            PlacementStepRow row)
        {
            _steps.Remove(row);
            return true;
        }

        return false;
    }

    private void MoveUp_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveSelected(-1);

    private void MoveDown_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveSelected(1);

    private void InsertStepInPhaseOrder(
        PlacementStepRow row)
    {
        int index =
            row.Phase ==
                PlacementPhase.BeforeStart
                ? _steps.TakeWhile(step =>
                    step.Phase ==
                        PlacementPhase.BeforeStart)
                    .Count()
                : _steps.Count;
        _steps.Insert(index, row);
    }

    private void FastStepReorderRequested(
        object? sender,
        PlacementStepReorderEventArgs e)
    {
        if (_services.Coordinator.IsBusy)
        {
            return;
        }

        if (MoveStepWithinPhase(
                e.Source,
                e.Target,
                e.InsertAfter))
        {
            FastStatusText.Text =
                "Placement order changed.";
        }
    }

    private bool NormalizeChangedStepPhase(
        PlacementStepRow row)
    {
        int sourceIndex = _steps.IndexOf(row);
        if (sourceIndex < 0 ||
            !Enum.IsDefined(row.Phase))
        {
            return false;
        }
        if (row.Phase ==
                PlacementPhase.BeforeStart &&
            PlacementAuthoringRules
                .IsCoveredByStartDialog(
                    row.X,
                    row.Y))
        {
            _normalizingPlacementStepPhase = true;
            try
            {
                row.Phase =
                    PlacementPhase.AfterStart;
            }
            finally
            {
                _normalizingPlacementStepPhase = false;
            }
            FastStatusText.Text =
                "That point is covered by the Start Game dialog. Move it outside the dialog or keep it After Start.";
            return false;
        }

        PlacementStep[] original =
            _steps.Select(step => step.ToModel())
                .ToArray();
        original[sourceIndex] =
            original[sourceIndex] with
            {
                Phase =
                    row.Phase ==
                        PlacementPhase.BeforeStart
                        ? PlacementPhase.AfterStart
                        : PlacementPhase.BeforeStart,
            };
        PlacementPhaseChange change =
            PlacementAuthoringRules
                .ChangePhaseForAuthoring(
                    original,
                    sourceIndex,
                    row.Phase);
        if (!change.Changed)
        {
            return false;
        }

        if (change.ChangedIndex != sourceIndex)
        {
            using (SuspendPlacementAutoSave())
            {
                _steps.Move(
                    sourceIndex,
                    change.ChangedIndex);
            }
        }
        UpdateFastPlacementCount();
        ActiveStepsSelector.SelectedItem = row;
        FastStepsList.ScrollIntoView(row);
        FastStatusText.Text =
            $"Unit {row.UnitKey} moved to {row.PhaseLabel}.";
        return true;
    }

    private bool MoveStepWithinPhase(
        PlacementStepRow source,
        PlacementStepRow target,
        bool insertAfter)
    {
        if (source.Phase != target.Phase)
        {
            return false;
        }

        int current = _steps.IndexOf(source);
        int targetIndex =
            _steps.IndexOf(target);
        if (current < 0 || targetIndex < 0)
        {
            return false;
        }

        if (insertAfter)
        {
            targetIndex++;
        }
        if (current < targetIndex)
        {
            targetIndex--;
        }
        if (current == targetIndex)
        {
            return false;
        }

        _steps.Move(current, targetIndex);
        FastStepsList.SelectedItem = source;
        FastStepsList.ScrollIntoView(source);
        return true;
    }

    private void MoveSelected(int offset)
    {
        if (ActiveStepsSelector.SelectedItem is not
            PlacementStepRow row)
        {
            return;
        }

        int current = _steps.IndexOf(row);
        int target = current + offset;
        if (target < 0 || target >= _steps.Count)
        {
            return;
        }
        if (_steps[target].Phase != row.Phase)
        {
            FastStatusText.Text =
                "Before Start steps always stay above After Start steps.";
            return;
        }

        _steps.Move(current, target);
        ActiveStepsSelector.SelectedItem = row;
        FastStepsList.ScrollIntoView(row);
        FastStatusText.Text =
            "Placement order changed.";
    }
}
