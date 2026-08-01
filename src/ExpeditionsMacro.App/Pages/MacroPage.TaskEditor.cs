using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private void TaskKindCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        SyncTaskKindButtons();
        RefreshVisibleRoutes();
        UpdateTaskTargetEditor();
    }

    private void TaskRouteCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateTaskTargetEditor();

    private async void AddOrUpdateTask_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            MacroTaskDefinition definition =
                BuildPlacementSetupTask();
            await ValidateBountyTaskAsync(
                definition);
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
            ResetTaskEditor();
            QueuePlanAutoSave();
        }
        catch (Exception error)
        {
            ShowTaskEditorError(error.Message);
        }
    }

    private MacroTaskDefinition BuildPlacementSetupTask()
    {
        MacroTaskKind kind = SelectedTaskKind();
        bool utility = kind == MacroTaskKind.Utility;
        PlacementSetupRoute? route =
            kind is MacroTaskKind.Challenge or
                MacroTaskKind.Utility or
                MacroTaskKind.Bounty
                ? null
                : TaskRouteCombo.SelectedItem as
                    PlacementSetupRoute
                    ?? throw new InvalidOperationException(
                        "Choose a map and act.");
        NamedChoice<ResourceRefuelTarget>?
            utilityRoute = utility
                ? TaskRouteCombo.SelectedItem as
                    NamedChoice<ResourceRefuelTarget>
                    ?? throw new InvalidOperationException(
                        "Choose a refuel route.")
                : null;
        bool runtimeTarget =
            route?.Target.Mode ==
                PlacementTargetMode.Story &&
            route.Target.StoryRunKind ==
                StoryRunKind.Infinite;
        int target = kind is
                MacroTaskKind.Challenge or
                MacroTaskKind.Bounty
            ? 1
            : ParsePositiveInt(
                TaskTargetText,
                utility
                    ? "Interval minutes"
                    : runtimeTarget
                    ? "Runtime minutes"
                    : "Victory target");
        int retries = kind is
                MacroTaskKind.Expedition or
                MacroTaskKind.Utility or
                MacroTaskKind.Bounty
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
        string name = kind switch
        {
            MacroTaskKind.Challenge =>
                "Challenge rotation",
            MacroTaskKind.Utility =>
                utilityRoute!.Name,
            MacroTaskKind.Bounty =>
                "Mythic Bounty Board",
            _ => route!.Name,
        };
        MacroTaskDefinition definition =
            StoryHardModePolicy.Normalize(
                new MacroTaskDefinition
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
                    RefuelTarget =
                        utilityRoute?.Value ??
                        ResourceRefuelTarget.GoldMine,
                    RefuelIntervalMinutes =
                        utility ? target : 60,
                });
        definition.Validate();
        return definition;
    }

    private void BeginTaskEdit(MacroTaskRow row)
    {
        if (row.Definition.Kind is not
                (MacroTaskKind.Utility or
                 MacroTaskKind.Bounty) &&
            !row.Definition.UsesPlacementSetup)
        {
            ShowPlanBlocksStatus(
                "This legacy preset task is read-only. Remove it and add a Placement Setup route to replace it.");
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
        RefreshVisibleRoutes();
        ApplyPlacementSetupTask(row.Definition);
        TaskTargetText.Text =
            row.Definition.Kind ==
                MacroTaskKind.Utility
                ? row.Definition
                    .RefuelIntervalMinutes
                    .ToString(
                        CultureInfo.InvariantCulture)
                : row.Definition.CompleteOnRuntimeDefeat
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
        if (definition.Kind == MacroTaskKind.Utility)
        {
            TaskRouteCombo.SelectedItem =
                UtilityRoutes.First(choice =>
                    choice.Value ==
                    definition.RefuelTarget);
        }
        else if (definition.PlacementTarget is not null)
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
        ResourceRefuelTarget? selectedUtility =
            (TaskRouteCombo.SelectedItem as
                NamedChoice<ResourceRefuelTarget>)?.Value;
        PlacementSetupRoute? selected =
            TaskRouteCombo.SelectedItem as
                PlacementSetupRoute;
        MacroTaskKind kind = SelectedTaskKind();
        if (kind == MacroTaskKind.Bounty)
        {
            TaskRouteCombo.ItemsSource = null;
            HideTaskEditorError();
            return;
        }
        if (kind == MacroTaskKind.Utility)
        {
            TaskRouteCombo.ItemsSource =
                UtilityRoutes;
            TaskRouteCombo.SelectedItem =
                UtilityRoutes.FirstOrDefault(
                    choice =>
                        choice.Value ==
                        selectedUtility) ??
                UtilityRoutes[0];
            HideTaskEditorError();
            return;
        }

        TaskRouteCombo.ItemsSource = _visibleRoutes;
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
        if (TaskTargetLabel is null ||
            TaskTargetText is null)
        {
            return;
        }
        UpdatePlacementSetupTaskEditor();
    }

    private void UpdatePlacementSetupTaskEditor()
    {
        MacroTaskKind kind = SelectedTaskKind();
        bool challenge =
            kind == MacroTaskKind.Challenge;
        bool utility =
            kind == MacroTaskKind.Utility;
        bool bounty =
            kind == MacroTaskKind.Bounty;
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
            challenge || bounty
                ? Visibility.Collapsed
                : Visibility.Visible;
        TaskSelectionGapColumn.Width =
            challenge || bounty
                ? new GridLength(0)
                : new GridLength(16);
        TaskSelectionColumn.Width =
            challenge || bounty
                ? new GridLength(0)
                : new GridLength(
                    1,
                    GridUnitType.Star);
        TaskTargetColumn.Width =
            challenge || bounty
                ? new GridLength(
                    1,
                    GridUnitType.Star)
                : new GridLength(132);
        TaskRouteCombo.Visibility =
            challenge || bounty
                ? Visibility.Collapsed
                : Visibility.Visible;
        FastTaskOptionsPanel.Visibility =
            utility || bounty
                ? Visibility.Collapsed
                : Visibility.Visible;
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
        bool supportsHardMode =
            story &&
            SelectedStoryRouteSupportsHardMode();
        if (!supportsHardMode)
        {
            TaskHardModeCheck.IsChecked = false;
        }
        TaskStoryOptionsPanel.Visibility =
            supportsHardMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        TaskTargetLabel.Text = challenge
            ? "Schedule"
            : bounty
                ? "Schedule"
            : utility
                ? "Interval, min"
                : runtime
                ? "Runtime, min"
                : "Victories";
        TaskTargetLabel.Visibility =
            challenge || bounty
                ? Visibility.Collapsed
                : Visibility.Visible;
        TaskTargetText.IsEnabled =
            !challenge && !bounty &&
            !_services.Coordinator.IsBusy;
        if (challenge || bounty)
        {
            TaskTargetText.Text = bounty
                ? "Daily reset · UTC"
                : "Every reset";
        }
        else if (utility &&
                 !int.TryParse(
                     TaskTargetText.Text,
                     out _))
        {
            TaskTargetText.Text = "60";
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

    private bool SelectedStoryRouteSupportsHardMode() =>
        TaskRouteCombo.SelectedItem is
            PlacementSetupRoute route &&
        StoryHardModePolicy.SupportsHardMode(
            route.Target);

}
