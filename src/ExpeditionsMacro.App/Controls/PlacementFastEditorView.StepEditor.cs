using System.Windows;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public sealed class PlacementStepEditorOpeningEventArgs(
    PlacementStepRow? step,
    PlacementStepRow? suggestedPlacement) : EventArgs
{
    public PlacementStepRow? Step { get; } = step;

    public PlacementStepRow? SuggestedPlacement
    {
        get;
    } = suggestedPlacement;
}

public partial class PlacementFastEditorView
{
    private void AddStep_Click(
        object sender,
        RoutedEventArgs e)
    {
        PlacementStepRow? source =
            FastStepsList.SelectedItem as
                PlacementStepRow;
        if (source?.HasCoordinate != true)
        {
            source = null;
        }
        source ??=
            FastStepsList.Items
                .OfType<PlacementStepRow>()
                .FirstOrDefault(step =>
                    step.Kind ==
                    MatchStepKind.Placement);
        StepSettingsOpening?.Invoke(
            this,
            new PlacementStepEditorOpeningEventArgs(
                null,
                source));
    }

    private void StepSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement
            {
                DataContext: PlacementStepRow step,
            } ||
            !step.CanEdit)
        {
            return;
        }
        FastStepsList.SelectedItem = step;
        StepSettingsOpening?.Invoke(
            this,
            new PlacementStepEditorOpeningEventArgs(
                step,
                null));
        e.Handled = true;
    }

    private void StepMoveUp_Click(
        object sender,
        RoutedEventArgs e)
    {
        SelectStepFromAction(sender);
        MoveStepUpRequested?.Invoke(sender, e);
        e.Handled = true;
    }

    private void StepMoveDown_Click(
        object sender,
        RoutedEventArgs e)
    {
        SelectStepFromAction(sender);
        MoveStepDownRequested?.Invoke(sender, e);
        e.Handled = true;
    }

    private void StepRemove_Click(
        object sender,
        RoutedEventArgs e)
    {
        SelectStepFromAction(sender);
        RemoveStepRequested?.Invoke(sender, e);
        e.Handled = true;
    }

    private void SelectStepFromAction(object sender)
    {
        if (sender is FrameworkElement
            {
                DataContext: PlacementStepRow step,
            })
        {
            FastStepsList.SelectedItem = step;
        }
    }
}
