using System.Windows;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private void CancelTaskEdit_Click(
        object sender,
        RoutedEventArgs e) =>
        ResetTaskEditor();

    private void RemoveTask_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not
            MacroTaskRow row)
        {
            return;
        }
        TaskRows.Remove(row);
        ReindexRows();
        if (_editingTaskId == row.Definition.Id)
        {
            ResetTaskEditor();
        }
        TaskEditorStatusText.Text =
            "Task removed. Save the plan to persist the change.";
    }

    private void MoveTaskUp_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveTask(
            (sender as FrameworkElement)?.Tag as
                MacroTaskRow,
            -1);

    private void MoveTaskDown_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveTask(
            (sender as FrameworkElement)?.Tag as
                MacroTaskRow,
            1);

    private void MoveTask(
        MacroTaskRow? row,
        int direction)
    {
        if (row is null) return;
        int index = TaskRows.IndexOf(row);
        int target = index + direction;
        if (index < 0 ||
            target < 0 ||
            target >= TaskRows.Count)
        {
            return;
        }
        TaskRows.Move(index, target);
        ReindexRows();
        TaskEditorStatusText.Text =
            "Priority changed. Save the plan to persist the order.";
    }

    private void ResetTaskEditor()
    {
        _editingTaskId = null;
        AddTaskButton.Content = "Add task";
        CancelTaskEditButton.Visibility =
            Visibility.Collapsed;
        TaskEnabledCheck.IsChecked = true;
        TaskTargetText.Text = "1";
        TaskDefeatRetriesText.Text = "0";
        TaskTraitCheck.IsChecked = true;
        TaskStatCheck.IsChecked = true;
        TaskSpriteCheck.IsChecked = true;
        TaskExtractCheck.IsChecked = true;
        TaskBossNodesText.Text = "1";
        TaskHardModeCheck.IsChecked = false;
        TaskDifficultyCombo.SelectedIndex = 0;
        UpdateTaskTargetEditor();
    }

    private void ReindexRows()
    {
        MacroTaskRow[] rows = TaskRows
            .Select((row, index) =>
                new MacroTaskRow
                {
                    Definition =
                        row.Definition with
                        {
                            Priority = index + 1,
                        },
                    Progress = row.Progress,
                })
            .ToArray();
        TaskRows.Clear();
        foreach (MacroTaskRow row in rows)
        {
            TaskRows.Add(row);
        }
        EmptyTasksText.Visibility =
            TaskRows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
