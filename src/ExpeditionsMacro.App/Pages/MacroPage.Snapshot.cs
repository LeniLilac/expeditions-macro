using System.Windows;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

internal enum MacroPlanSnapshotState
{
    Empty,
    EmptyLoop,
    TasksOnly,
    NestedLoops,
    TaskPopup,
    StoryActTaskPopup,
    StoryMasteryTaskPopup,
    StoryInfiniteTaskPopup,
    UtilityTaskPopup,
    LoopSettingsPopup,
}

public partial class MacroPage
{
    private void PopulateSnapshotTasks(
        MacroPlanSnapshotState state)
    {
        TaskRows.Clear();
        LoopEditor.SetTasks(TaskRows);
        LoopEditor.Apply([], []);
        if (state == MacroPlanSnapshotState.Empty)
        {
            VerifyLoopFreeSnapshotStructure(
                expectedTasks: 0);
            EmptyTasksText.Visibility =
                Visibility.Visible;
            ApplyTotals();
            return;
        }
        if (state == MacroPlanSnapshotState.EmptyLoop)
        {
            LoopEditor.AddLoopBlock();
            if (LoopEditor.RootBlocks is not
                [MacroPlanLoopBlockNode
                {
                    Children.Count: 0,
                }])
            {
                throw new InvalidOperationException(
                    "A user-added empty loop did not remain as one editable loop block.");
            }
            EmptyTasksText.Visibility =
                Visibility.Collapsed;
            ApplyTotals();
            return;
        }

        AddSnapshotTask(
            "snapshot-challenge",
            MacroTaskKind.Challenge,
            "Challenge rotation",
            1);
        AddSnapshotTask(
            "snapshot-story",
            MacroTaskKind.Story,
            "School Grounds infinite",
            2,
            victories: 12,
            defeats: 1,
            runtimeSeconds: 8450);
        AddSnapshotTask(
            "snapshot-raid",
            MacroTaskKind.Raid,
            "Spirit City · Act 2",
            3,
            victories: 2);
        AddSnapshotTask(
            "snapshot-expedition",
            MacroTaskKind.Expedition,
            "Flower Forest expedition",
            4);
        AddSnapshotTask(
            "snapshot-event",
            MacroTaskKind.Event,
            "Villain Invasion · Act 4",
            5);

        LoopEditor.SetTasks(TaskRows);
        if (state == MacroPlanSnapshotState.TasksOnly)
        {
            VerifyLoopFreeSnapshotStructure(
                TaskRows.Count);
            EmptyTasksText.Visibility =
                Visibility.Collapsed;
            ApplyTotals();
            return;
        }

        MacroPlanLoopDefinition forever =
            SnapshotLoop(0, 4, 1, forever: true);
        MacroPlanLoopDefinition firstGroup =
            SnapshotLoop(0, 2, 3);
        MacroPlanLoopDefinition story =
            SnapshotLoop(1, 1, 2);
        MacroPlanLoopDefinition secondGroup =
            SnapshotLoop(3, 4, 4);
        MacroPlanLoopDefinition[] loops =
        [
            forever,
            firstGroup,
            story,
            secondGroup,
        ];
        LoopEditor.Apply(
            loops,
            [
                SnapshotLoopProgress(
                    forever,
                    completedRuns: 2),
                SnapshotLoopProgress(
                    firstGroup,
                    completedRuns: 1),
                SnapshotLoopProgress(story),
                SnapshotLoopProgress(secondGroup),
            ]);
        EmptyTasksText.Visibility =
            Visibility.Collapsed;
        ApplyTotals();
        if (state == MacroPlanSnapshotState.TaskPopup)
        {
            OpenTaskEditor(
                LoopEditor.LoopBlocks[1]);
        }
        else if (state is
                 MacroPlanSnapshotState
                     .StoryActTaskPopup or
                 MacroPlanSnapshotState
                     .StoryMasteryTaskPopup or
                 MacroPlanSnapshotState
                     .StoryInfiniteTaskPopup)
        {
            StoryRunKind runKind = state switch
            {
                MacroPlanSnapshotState
                    .StoryActTaskPopup =>
                    StoryRunKind.Act,
                MacroPlanSnapshotState
                    .StoryMasteryTaskPopup =>
                    StoryRunKind.Mastery,
                _ => StoryRunKind.Infinite,
            };
            OpenStoryTaskEditorForSnapshot(
                runKind);
        }
        else if (state ==
                 MacroPlanSnapshotState
                     .UtilityTaskPopup)
        {
            OpenUtilityTaskEditorForSnapshot();
        }
        else if (state ==
                 MacroPlanSnapshotState
                     .LoopSettingsPopup)
        {
            LoopEditor.OpenLoopSettingsForSnapshot(
                LoopEditor.LoopBlocks[1]);
        }
    }

    private void OpenUtilityTaskEditorForSnapshot()
    {
        OpenTaskEditor(
            LoopEditor.LoopBlocks[1]);
        TaskKindCombo.SelectedItem =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .First(choice =>
                    choice.Value ==
                    MacroTaskKind.Utility);
        RefreshVisibleRoutes();
        TaskRouteCombo.SelectedItem =
            UtilityRoutes.Single(choice =>
                choice.Value ==
                ResourceRefuelTarget.Both);
        TaskTargetText.Text = "45";
        UpdateTaskTargetEditor();
    }

    private void OpenStoryTaskEditorForSnapshot(
        StoryRunKind runKind)
    {
        OpenTaskEditor(
            LoopEditor.LoopBlocks[1]);
        TaskKindCombo.SelectedItem =
            TaskKindCombo.Items
                .Cast<NamedChoice<MacroTaskKind>>()
                .First(choice =>
                    choice.Value ==
                    MacroTaskKind.Story);
        RefreshVisibleRoutes();
        TaskRouteCombo.SelectedItem =
            _visibleRoutes.First(route =>
                route.Target.MapNumber == 1 &&
                route.Target.StoryRunKind ==
                    runKind &&
                (runKind != StoryRunKind.Act ||
                 route.Target.ActNumber == 1));
        UpdateTaskTargetEditor();

        bool hardModeVisible =
            TaskStoryOptionsPanel.Visibility ==
            Visibility.Visible;
        if (hardModeVisible !=
            (runKind == StoryRunKind.Act))
        {
            throw new InvalidOperationException(
                "Story Hard mode visibility does not match the selected run type.");
        }
    }

    private void VerifyLoopFreeSnapshotStructure(
        int expectedTasks)
    {
        bool valid =
            LoopEditor.RootBlocks.Count ==
                expectedTasks &&
            LoopEditor.RootBlocks.All(node =>
                node is MacroPlanTaskBlockNode) &&
            MacroPlanStructure.FlattenLoops(
                LoopEditor.RootBlocks).Count == 0;
        if (!valid)
        {
            throw new InvalidOperationException(
                "A tasks-only Macro Plan created an implicit loop block.");
        }
    }

    private void AddSnapshotTask(
        string id,
        MacroTaskKind kind,
        string name,
        int priority,
        int victories = 0,
        int defeats = 0,
        long runtimeSeconds = 0)
    {
        TaskRows.Add(new MacroTaskRow
        {
            Definition = new MacroTaskDefinition
            {
                Id = id,
                Kind = kind,
                PresetId = $"{id}-preset",
                Name = name,
                Priority = priority,
            },
            Progress = new MacroTaskProgress
            {
                TaskId = id,
                Victories = victories,
                Defeats = defeats,
                RuntimeSeconds = runtimeSeconds,
            },
        });
    }

    private MacroPlanLoopDefinition SnapshotLoop(
        int start,
        int stop,
        int runs,
        bool forever = false) => new()
        {
            StartTaskId =
                TaskRows[start].Definition.Id,
            StopTaskId =
                TaskRows[stop].Definition.Id,
            TotalRuns = runs,
            Forever = forever,
        };

    private static MacroPlanLoopProgress
        SnapshotLoopProgress(
        MacroPlanLoopDefinition loop,
        int completedRuns = 0) => new()
        {
            ConfigurationSignature =
                loop.ConfigurationSignature,
            Phase = MacroPlanLoopPhase.Loop,
            CompletedRuns = completedRuns,
        };
}
