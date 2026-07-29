using System.IO;
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
        if (ActiveStepsSelector.SelectedItem is not
                PlacementStepRow row)
        {
            return false;
        }
        if (!row.CanRemove)
        {
            FastStatusText.Text =
                "Start Game is required and cannot be removed.";
            return false;
        }

        List<PlacementStep> prospective =
            _steps.Where(candidate =>
                    !ReferenceEquals(candidate, row))
                .Select(candidate =>
                    candidate.ToModel())
                .ToList();
        try
        {
            ValidateTimeline(prospective);
        }
        catch (Exception error) when (
            error is InvalidDataException or
            InvalidOperationException)
        {
            FastStatusText.Text = error.Message;
            return false;
        }

        _steps.Remove(row);
        NormalizeTimelineRows();
        FastStatusText.Text =
            $"{row.StepTypeLabel} removed.";
        SchedulePlacementAutoSave();
        return true;
    }

    private void MoveUp_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveSelected(-1);

    private void MoveDown_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveSelected(1);

    private void FastStepReorderRequested(
        object? sender,
        PlacementStepReorderEventArgs e)
    {
        if (_services.Coordinator.IsBusy)
        {
            return;
        }

        int current = _steps.IndexOf(e.Source);
        int target = _steps.IndexOf(e.Target);
        if (current < 0 || target < 0)
        {
            return;
        }
        if (e.InsertAfter)
        {
            target++;
        }
        if (current < target)
        {
            target--;
        }
        MoveStepTo(e.Source, target);
    }

    private void MoveSelected(int offset)
    {
        if (ActiveStepsSelector.SelectedItem is not
                PlacementStepRow row)
        {
            return;
        }

        int current = _steps.IndexOf(row);
        MoveStepTo(row, current + offset);
    }

    private bool MoveStepTo(
        PlacementStepRow row,
        int target)
    {
        int current = _steps.IndexOf(row);
        if (current < 0 ||
            target < 0 ||
            target >= _steps.Count ||
            current == target)
        {
            return false;
        }

        List<PlacementStepRow> ordered =
            [.. _steps];
        ordered.RemoveAt(current);
        ordered.Insert(target, row);
        try
        {
            ValidateTimeline(
                ordered.Select(step =>
                        step.ToModel())
                    .ToArray());
        }
        catch (Exception error) when (
            error is InvalidDataException or
            InvalidOperationException)
        {
            FastStatusText.Text = error.Message;
            return false;
        }

        using (SuspendPlacementAutoSave())
        {
            _steps.Move(current, target);
            NormalizeTimelineRows();
        }
        ActiveStepsSelector.SelectedItem = row;
        FastStepsList.ScrollIntoView(row);
        FastStatusText.Text =
            row.IsStartGame
                ? "Start Game boundary moved."
                : "Match-step order changed.";
        SchedulePlacementAutoSave();
        return true;
    }
}
