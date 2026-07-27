using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private bool FastTaskWorkflow =>
        _services.Settings.FastNoAlignEnabled;

    private void TaskKindCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (TaskPresetCombo is null) return;
        SyncTaskKindButtons();
        RefreshVisiblePresets();
        UpdateTaskTargetEditor();
    }

    private void TaskPresetCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateTaskTargetEditor();

    private void TaskRouteCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateTaskTargetEditor();

    private void AddOrUpdateTask_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            MacroTaskDefinition definition =
                FastTaskWorkflow
                    ? BuildPlacementSetupTask()
                    : BuildLegacyPresetTask();
            int existingIndex =
                IndexOfTask(_editingTaskId);
            MacroTaskProgress progress =
                existingIndex >= 0 &&
                SameWork(
                    TaskRows[existingIndex].Definition,
                    definition)
                    ? TaskRows[existingIndex].Progress
                    : new MacroTaskProgress
                    {
                        TaskId = definition.Id,
                    };
            MacroTaskRow row = new()
            {
                Definition = definition,
                Progress = progress,
            };
            if (existingIndex >= 0)
            {
                TaskRows[existingIndex] = row;
            }
            else
            {
                TaskRows.Add(row);
            }
            LoopEditor.SetTasks(TaskRows);
            MacroPlanLoopBlockNode? destination =
                SelectedTaskDestination();
            if (!LoopEditor.TryPlaceTask(
                    row,
                    destination,
                    out string placementError))
            {
                throw new InvalidOperationException(
                    placementError);
            }
            ReindexRows();
            ShowPlanBlocksStatus(
                existingIndex >= 0
                    ? "Task updated. Save the plan to persist it."
                    : destination is null
                        ? "Task added to the plan. Save the plan to persist it."
                        : $"Task added to {destination.BlockLabel}. Save the plan to persist it.");
            ResetTaskEditor();
        }
        catch (Exception error)
        {
            ShowTaskEditorError(error.Message);
        }
    }

    private MacroTaskDefinition BuildPlacementSetupTask()
    {
        MacroTaskKind kind = SelectedTaskKind();
        PlacementSetupRoute? route =
            kind == MacroTaskKind.Challenge
                ? null
                : TaskRouteCombo.SelectedItem as
                    PlacementSetupRoute
                    ?? throw new InvalidOperationException(
                        "Choose a map and act.");
        bool runtimeTarget =
            route?.Target.Mode ==
                PlacementTargetMode.Story &&
            route.Target.StoryRunKind ==
                StoryRunKind.Infinite;
        int target = kind == MacroTaskKind.Challenge
            ? 1
            : ParsePositiveInt(
                TaskTargetText,
                runtimeTarget
                    ? "Runtime minutes"
                    : "Victory target");
        int retries = kind == MacroTaskKind.Expedition
            ? 0
            : ParseWholeNumber(
                TaskDefeatRetriesText,
                "Defeat retries",
                0,
                20);
        int bosses = kind == MacroTaskKind.Expedition
            ? ParseWholeNumber(
                TaskBossNodesText,
                "Boss nodes before extraction",
                0,
                99)
            : 1;
        int difficulty =
            TaskDifficultyCombo.SelectedItem is
                NamedChoice<int> choice
                ? choice.Value
                : 1;
        string name = kind == MacroTaskKind.Challenge
            ? "Challenge rotation"
            : route!.Name;
        MacroTaskDefinition definition = new()
        {
            Id = _editingTaskId ??
                $"task-{Guid.NewGuid():N}",
            Kind = kind,
            Name = name,
            Priority = 1,
            PlacementTarget = route?.Target,
            TargetVictories =
                runtimeTarget ? 1 : target,
            TargetRuntimeMinutes =
                runtimeTarget ? target : 180,
            CompleteOnRuntimeDefeat =
                runtimeTarget,
            Difficulty = difficulty,
            HardMode =
                TaskHardModeCheck.IsChecked == true,
            DefeatRetries = retries,
            RunTraitChallenge =
                TaskTraitCheck.IsChecked == true,
            RunStatChallenge =
                TaskStatCheck.IsChecked == true,
            RunSpriteChallenge =
                TaskSpriteCheck.IsChecked == true,
            ExtractAtCheckpoint =
                TaskExtractCheck.IsChecked == true,
            BossesBeforeExtract = bosses,
        };
        definition.Validate();
        return definition;
    }

    private void BeginTaskEdit(MacroTaskRow row)
    {
        if (FastTaskWorkflow &&
            !row.Definition.UsesPlacementSetup)
        {
            ShowPlanBlocksStatus(
                "This is a legacy preset task. Remove it and add a Fast no align route to replace it.");
            return;
        }
        PopulateTaskDestinations(
            LoopEditor.ParentLoopFor(row));
        _editingTaskId = row.Definition.Id;
        TaskKindCombo.SelectedItem =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .First(value =>
                    value.Value ==
                    row.Definition.Kind);
        RefreshVisiblePresets();
        if (FastTaskWorkflow &&
            row.Definition.UsesPlacementSetup)
        {
            ApplyPlacementSetupTask(row.Definition);
        }
        else
        {
            TaskPresetCombo.SelectedItem =
                _visiblePresets.FirstOrDefault(
                    value =>
                        value.Kind ==
                            row.Definition.Kind &&
                        value.Id ==
                            row.Definition.PresetId);
        }
        TaskTargetText.Text =
            row.Definition.CompleteOnRuntimeDefeat
                ? row.Definition
                    .TargetRuntimeMinutes
                    .ToString(
                        CultureInfo.InvariantCulture)
                : row.Definition
                    .TargetVictories
                    .ToString(
                        CultureInfo.InvariantCulture);
        TaskEditorTitleText.Text = "Edit task";
        AddTaskButton.Content = "Save changes";
        HideTaskEditorError();
        UpdateTaskTargetEditor();
        ShowTaskEditor();
    }

    private void ApplyPlacementSetupTask(
        MacroTaskDefinition definition)
    {
        if (definition.PlacementTarget is not null)
        {
            TaskRouteCombo.SelectedItem =
                _visibleRoutes.FirstOrDefault(
                    route =>
                        route.Target.Matches(
                            definition
                                .PlacementTarget));
        }
        TaskDefeatRetriesText.Text =
            definition.DefeatRetries.ToString(
                CultureInfo.InvariantCulture);
        TaskTraitCheck.IsChecked =
            definition.RunTraitChallenge;
        TaskStatCheck.IsChecked =
            definition.RunStatChallenge;
        TaskSpriteCheck.IsChecked =
            definition.RunSpriteChallenge;
        TaskExtractCheck.IsChecked =
            definition.ExtractAtCheckpoint;
        TaskBossNodesText.Text =
            definition.BossesBeforeExtract.ToString(
                CultureInfo.InvariantCulture);
        TaskHardModeCheck.IsChecked =
            definition.HardMode;
        TaskDifficultyCombo.SelectedItem =
            TaskDifficultyCombo.Items
                .Cast<NamedChoice<int>>()
                .First(choice =>
                    choice.Value ==
                    definition.Difficulty);
    }

    private void RefreshVisibleRoutes()
    {
        PlacementSetupRoute? selected =
            TaskRouteCombo.SelectedItem as
                PlacementSetupRoute;
        MacroTaskKind kind = SelectedTaskKind();
        PlacementTargetMode? mode = kind switch
        {
            MacroTaskKind.Expedition =>
                PlacementTargetMode.Expedition,
            MacroTaskKind.Story =>
                PlacementTargetMode.Story,
            MacroTaskKind.Raid =>
                PlacementTargetMode.Raid,
            MacroTaskKind.Event =>
                PlacementTargetMode.Event,
            _ => null,
        };
        _visibleRoutes.Clear();
        if (mode is not null)
        {
            foreach (PlacementSetupRoute route in
                     PlacementSetupCatalog.All.Where(
                         route =>
                             route.Target.Mode ==
                             mode &&
                             route.Target.MapNumber !=
                             PlacementSetupCatalog
                                 .SharedExpeditionMapNumber &&
                             !PlacementSetupCatalog
                                 .IsSharedStoryTarget(
                                     route.Target)))
            {
                _visibleRoutes.Add(route);
            }
        }
        TaskRouteCombo.SelectedItem =
            _visibleRoutes.FirstOrDefault(
                route =>
                    selected is not null &&
                    route.Target.Matches(
                        selected.Target)) ??
            _visibleRoutes.FirstOrDefault();
        HideTaskEditorError();
    }

    private void UpdateTaskTargetEditor()
    {
        if (SharePlanCard is not null)
        {
            SharePlanCard.Visibility =
                FastTaskWorkflow
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        if (TaskTargetLabel is null ||
            TaskTargetText is null)
        {
            return;
        }
        if (FastTaskWorkflow)
        {
            UpdatePlacementSetupTaskEditor();
            return;
        }

        TaskSelectionLabel.Text = "Preset";
        TaskSelectionPanel.Visibility =
            Visibility.Visible;
        TaskSelectionGapColumn.Width =
            new GridLength(16);
        TaskSelectionColumn.Width =
            new GridLength(
                1,
                GridUnitType.Star);
        TaskTargetColumn.Width =
            new GridLength(132);
        TaskPresetCombo.Visibility =
            Visibility.Visible;
        TaskRouteCombo.Visibility =
            Visibility.Collapsed;
        FastTaskOptionsPanel.Visibility =
            Visibility.Collapsed;
        MacroTaskKind kind = SelectedTaskKind();
        bool challenge =
            kind == MacroTaskKind.Challenge;
        bool runtime =
            TaskPresetCombo.SelectedItem is
                MacroPresetChoice preset &&
            IsInfiniteStory(preset);
        TaskTargetLabel.Text = challenge
            ? "Schedule"
            : runtime
                ? "Runtime, min"
                : "Victories";
        TaskTargetLabel.Visibility =
            Visibility.Visible;
        TaskTargetText.IsEnabled =
            !challenge &&
            !_services.Coordinator.IsBusy;
        if (challenge)
        {
            TaskTargetText.Text = "Every reset";
        }
        else if (runtime &&
                 !int.TryParse(
                     TaskTargetText.Text,
                     out _))
        {
            TaskTargetText.Text = "180";
        }
        else if (!runtime &&
                 !int.TryParse(
                     TaskTargetText.Text,
                     out _))
        {
            TaskTargetText.Text = "1";
        }
    }

    private void UpdatePlacementSetupTaskEditor()
    {
        MacroTaskKind kind = SelectedTaskKind();
        bool challenge =
            kind == MacroTaskKind.Challenge;
        bool expedition =
            kind == MacroTaskKind.Expedition;
        bool story =
            kind == MacroTaskKind.Story;
        bool retries =
            kind is MacroTaskKind.Challenge or
                MacroTaskKind.Story or
                MacroTaskKind.Raid or
                MacroTaskKind.Event;
        bool runtime =
            TaskRouteCombo.SelectedItem is
                PlacementSetupRoute route &&
            route.Target.Mode ==
                PlacementTargetMode.Story &&
            route.Target.StoryRunKind ==
                StoryRunKind.Infinite;

        TaskSelectionLabel.Text = "Route";
        TaskSelectionPanel.Visibility =
            challenge
                ? Visibility.Collapsed
                : Visibility.Visible;
        TaskSelectionGapColumn.Width =
            challenge
                ? new GridLength(0)
                : new GridLength(16);
        TaskSelectionColumn.Width =
            challenge
                ? new GridLength(0)
                : new GridLength(
                    1,
                    GridUnitType.Star);
        TaskTargetColumn.Width =
            challenge
                ? new GridLength(
                    1,
                    GridUnitType.Star)
                : new GridLength(132);
        TaskPresetCombo.Visibility =
            Visibility.Collapsed;
        TaskRouteCombo.Visibility =
            challenge
                ? Visibility.Collapsed
                : Visibility.Visible;
        FastTaskOptionsPanel.Visibility =
            Visibility.Visible;
        TaskDefeatRetriesPanel.Visibility =
            retries
                ? Visibility.Visible
                : Visibility.Collapsed;
        TaskChallengeTypesPanel.Visibility =
            challenge
                ? Visibility.Visible
                : Visibility.Collapsed;
        TaskExpeditionOptionsPanel.Visibility =
            expedition
                ? Visibility.Visible
                : Visibility.Collapsed;
        TaskStoryOptionsPanel.Visibility =
            story
                ? Visibility.Visible
                : Visibility.Collapsed;
        TaskTargetLabel.Text = challenge
            ? "Schedule"
            : runtime
                ? "Runtime, min"
                : "Victories";
        TaskTargetLabel.Visibility =
            challenge
                ? Visibility.Collapsed
                : Visibility.Visible;
        TaskTargetText.IsEnabled =
            !challenge &&
            !_services.Coordinator.IsBusy;
        if (challenge)
        {
            TaskTargetText.Text = "Every reset";
        }
        else if (runtime &&
                 !int.TryParse(
                     TaskTargetText.Text,
                     out _))
        {
            TaskTargetText.Text = "180";
        }
        else if (!runtime &&
                 !int.TryParse(
                     TaskTargetText.Text,
                     out _))
        {
            TaskTargetText.Text = "1";
        }
    }

    private static int ParseWholeNumber(
        TextBox field,
        string label,
        int minimum,
        int maximum) =>
        int.TryParse(
            field.Text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value) &&
        value >= minimum &&
        value <= maximum
            ? value
            : throw new InvalidDataException(
                $"{label} must be {minimum} through {maximum}.");
}
