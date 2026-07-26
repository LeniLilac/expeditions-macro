using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
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
                "Placement order changed. Save setup to keep it.";
        }
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
            if (FastWorkflow)
            {
                FastStatusText.Text =
                    "Before Start steps always stay above After Start steps.";
            }
            return;
        }

        _steps.Move(current, target);
        ActiveStepsSelector.SelectedItem = row;
        if (FastWorkflow)
        {
            FastStepsList.ScrollIntoView(row);
            FastStatusText.Text =
                "Placement order changed. Save setup to keep it.";
        }
    }
}
