using System.Windows;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private void CancelTaskEdit_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResetTaskEditor();
        OpenTaskEditorButton.Focus();
    }

    private void RemoveTask(MacroTaskRow row)
    {
        TaskRows.Remove(row);
        ReindexRows();
        if (_editingTaskId == row.Definition.Id)
        {
            ResetTaskEditor();
        }
        ShowPlanBlocksStatus(
            "Task removed. Save the plan to persist the change.");
    }

    private void ResetTaskEditor()
    {
        _editingTaskId = null;
        TaskEditorOverlay.Visibility =
            Visibility.Collapsed;
        TaskEditorTitleText.Text = "Add task";
        AddTaskButton.Content = "Add task";
        TaskTargetText.Text = "1";
        TaskDefeatRetriesText.Text = "0";
        TaskTraitCheck.IsChecked = true;
        TaskStatCheck.IsChecked = true;
        TaskSpriteCheck.IsChecked = true;
        TaskExtractCheck.IsChecked = true;
        TaskBossNodesText.Text = "1";
        TaskHardModeCheck.IsChecked = false;
        TaskDifficultyCombo.SelectedIndex = 0;
        HideTaskEditorError();
        UpdateTaskTargetEditor();
    }

    private void ShowPlanBlocksStatus(
        string message)
    {
        PlanBlocksStatusText.Text = message;
        PlanBlocksStatusText.Visibility =
            string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void ReindexRows()
    {
        LoopEditor.SetTasks(TaskRows);
        MacroTaskRow[] rows = LoopEditor
            .OrderedTaskRows
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
        LoopEditor.SetTasks(TaskRows);
        EmptyTasksText.Visibility =
            LoopEditor.RootBlocks.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
