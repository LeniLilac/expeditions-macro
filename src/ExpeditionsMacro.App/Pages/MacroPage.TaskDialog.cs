using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private TextBlock TaskEditorTitleText =>
        TaskEditorDialog.TaskEditorTitleText;
    private ComboBox TaskDestinationCombo =>
        TaskEditorDialog.TaskDestinationCombo;
    private ComboBox TaskKindCombo =>
        TaskEditorDialog.TaskKindCombo;
    private Grid TaskKindButtonGrid =>
        TaskEditorDialog.TaskKindButtonGrid;
    private RadioButton ChallengeTaskKindButton =>
        TaskEditorDialog.ChallengeTaskKindButton;
    private RadioButton ExpeditionTaskKindButton =>
        TaskEditorDialog.ExpeditionTaskKindButton;
    private RadioButton StoryTaskKindButton =>
        TaskEditorDialog.StoryTaskKindButton;
    private RadioButton RaidTaskKindButton =>
        TaskEditorDialog.RaidTaskKindButton;
    private RadioButton EventTaskKindButton =>
        TaskEditorDialog.EventTaskKindButton;
    private RadioButton UtilityTaskKindButton =>
        TaskEditorDialog.UtilityTaskKindButton;
    private RadioButton BountyTaskKindButton =>
        TaskEditorDialog.BountyTaskKindButton;
    private ColumnDefinition TaskSelectionColumn =>
        TaskEditorDialog.TaskSelectionColumn;
    private ColumnDefinition TaskSelectionGapColumn =>
        TaskEditorDialog.TaskSelectionGapColumn;
    private ColumnDefinition TaskTargetColumn =>
        TaskEditorDialog.TaskTargetColumn;
    private StackPanel TaskSelectionPanel =>
        TaskEditorDialog.TaskSelectionPanel;
    private TextBlock TaskSelectionLabel =>
        TaskEditorDialog.TaskSelectionLabel;
    private ComboBox TaskRouteCombo =>
        TaskEditorDialog.TaskRouteCombo;
    private TextBlock TaskTargetLabel =>
        TaskEditorDialog.TaskTargetLabel;
    private TextBox TaskTargetText =>
        TaskEditorDialog.TaskTargetText;
    private Border FastTaskOptionsPanel =>
        TaskEditorDialog.FastTaskOptionsPanel;
    private StackPanel TaskDefeatRetriesPanel =>
        TaskEditorDialog.TaskDefeatRetriesPanel;
    private TextBox TaskDefeatRetriesText =>
        TaskEditorDialog.TaskDefeatRetriesText;
    private StackPanel TaskChallengeTypesPanel =>
        TaskEditorDialog.TaskChallengeTypesPanel;
    private CheckBox TaskTraitCheck =>
        TaskEditorDialog.TaskTraitCheck;
    private CheckBox TaskStatCheck =>
        TaskEditorDialog.TaskStatCheck;
    private CheckBox TaskSpriteCheck =>
        TaskEditorDialog.TaskSpriteCheck;
    private StackPanel TaskExpeditionOptionsPanel =>
        TaskEditorDialog.TaskExpeditionOptionsPanel;
    private ComboBox TaskDifficultyCombo =>
        TaskEditorDialog.TaskDifficultyCombo;
    private CheckBox TaskExtractCheck =>
        TaskEditorDialog.TaskExtractCheck;
    private TextBox TaskBossNodesText =>
        TaskEditorDialog.TaskBossNodesText;
    private StackPanel TaskStoryOptionsPanel =>
        TaskEditorDialog.TaskStoryOptionsPanel;
    private CheckBox TaskHardModeCheck =>
        TaskEditorDialog.TaskHardModeCheck;
    private TextBlock TaskEditorStatusText =>
        TaskEditorDialog.TaskEditorStatusText;
    private Button CancelTaskEditButton =>
        TaskEditorDialog.CancelTaskEditButton;
    private Button AddTaskButton =>
        TaskEditorDialog.AddTaskButton;

    private void InitializeTaskDialog()
    {
        TaskKindCombo.SelectionChanged +=
            TaskKindCombo_SelectionChanged;
        TaskRouteCombo.SelectionChanged +=
            TaskRouteCombo_SelectionChanged;
        foreach (RadioButton button in
                 new[]
                 {
                     ChallengeTaskKindButton,
                     ExpeditionTaskKindButton,
                     StoryTaskKindButton,
                     RaidTaskKindButton,
                     EventTaskKindButton,
                     UtilityTaskKindButton,
                     BountyTaskKindButton,
                 })
        {
            button.Checked +=
                TaskKindButton_Checked;
        }
        TaskEditorDialog.CloseTaskEditorButton.Click +=
            CancelTaskEdit_Click;
        CancelTaskEditButton.Click +=
            CancelTaskEdit_Click;
        AddTaskButton.Click +=
            AddOrUpdateTask_Click;
    }

    private void TaskKindButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (_syncingTaskKindButtons ||
            sender is not FrameworkElement
            {
                Tag: string value,
            } ||
            !Enum.TryParse(
                value,
                ignoreCase: true,
                out MacroTaskKind kind))
        {
            return;
        }
        TaskKindCombo.SelectedItem =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .FirstOrDefault(choice =>
                    choice.Value == kind);
        if (kind == MacroTaskKind.Utility &&
            _editingTaskId is null &&
            (TaskTargetText.Text == "1" ||
             !int.TryParse(
                 TaskTargetText.Text,
                 out _)))
        {
            TaskTargetText.Text = "60";
        }
    }

    private void OpenTaskEditor(
        MacroPlanLoopBlockNode? destination)
    {
        if (_services.Coordinator.IsBusy)
        {
            return;
        }
        ResetTaskEditor();
        PopulateTaskDestinations(destination);
        TaskEditorTitleText.Text = "Add task";
        AddTaskButton.Content = "Add task";
        ShowTaskEditor();
    }

    private void ShowTaskEditor()
    {
        TaskEditorOverlay.Visibility =
            Visibility.Visible;
        Dispatcher.BeginInvoke(
            () => TaskDestinationCombo.Focus());
    }

    private void PopulateTaskDestinations(
        MacroPlanLoopBlockNode? preferred)
    {
        TaskDestinationChoice[] choices =
        [
            new("Plan level", null),
            .. LoopEditor.LoopBlocks.Select(loop =>
                new TaskDestinationChoice(
                    loop.Depth == 0
                        ? loop.BlockLabel
                        : $"{loop.BlockLabel} · nesting level {loop.Depth + 1}",
                    loop)),
        ];
        TaskDestinationCombo.ItemsSource =
            choices;
        TaskDestinationCombo.SelectedItem =
            choices.FirstOrDefault(choice =>
                ReferenceEquals(
                    choice.Loop,
                    preferred)) ??
            choices[0];
    }

    private MacroPlanLoopBlockNode?
        SelectedTaskDestination() =>
        (TaskDestinationCombo.SelectedItem as
            TaskDestinationChoice)?.Loop;

    private void SyncTaskKindButtons()
    {
        MacroTaskKind selected =
            SelectedTaskKind();
        bool supportsEvent =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .Any(choice =>
                    choice.Value ==
                    MacroTaskKind.Event);
        bool supportsUtility =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .Any(choice =>
                    choice.Value ==
                    MacroTaskKind.Utility);
        bool supportsBounty =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .Any(choice =>
                    choice.Value ==
                    MacroTaskKind.Bounty);
        _syncingTaskKindButtons = true;
        try
        {
            ChallengeTaskKindButton.IsChecked =
                selected ==
                MacroTaskKind.Challenge;
            ExpeditionTaskKindButton.IsChecked =
                selected ==
                MacroTaskKind.Expedition;
            StoryTaskKindButton.IsChecked =
                selected == MacroTaskKind.Story;
            RaidTaskKindButton.IsChecked =
                selected == MacroTaskKind.Raid;
            EventTaskKindButton.Visibility =
                supportsEvent
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            UtilityTaskKindButton.Visibility =
                supportsUtility
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            BountyTaskKindButton.Visibility =
                supportsBounty
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            EventTaskKindButton.IsChecked =
                selected == MacroTaskKind.Event;
            UtilityTaskKindButton.IsChecked =
                selected == MacroTaskKind.Utility;
            BountyTaskKindButton.IsChecked =
                selected == MacroTaskKind.Bounty;
        }
        finally
        {
            _syncingTaskKindButtons = false;
        }
    }

    private void TaskEditorOverlay_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }
        ResetTaskEditor();
        e.Handled = true;
    }

    private void ShowTaskEditorError(
        string message)
    {
        TaskEditorStatusText.Text = message;
        TaskEditorStatusText.Visibility =
            Visibility.Visible;
    }

    private void HideTaskEditorError()
    {
        TaskEditorStatusText.Text =
            string.Empty;
        TaskEditorStatusText.Visibility =
            Visibility.Collapsed;
    }

    private sealed record TaskDestinationChoice(
        string Name,
        MacroPlanLoopBlockNode? Loop);
}
