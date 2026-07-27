using System.Windows;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    internal void SetSnapshotState(
        bool showRecordingSettings = false)
    {
        if (!FastWorkflow) return;
        using IDisposable suspension =
            SuspendPlacementAutoSave();
        _selectedModel = null;
        _selectedSetupTarget =
            showRecordingSettings
                ? PlacementSetupCatalog.All
                    .Single(route =>
                        route.Target.Mode ==
                            PlacementTargetMode.Event &&
                        route.Target.ActNumber ==
                            (int)EventAct.Act1 &&
                        route.Target.SpawnRoute ==
                            EventSpawnRoute.Angle2)
                    .Target
                : PlacementSetupCatalog.All[0]
                    .Target;
        ApplyFastTarget(_selectedSetupTarget);
        FastTeamCombo.SelectedIndex = 2;
        ResetFastTimingDefaults();
        ResetFastRecordingSettings();
        FastEditorPanel.ClearSnapshotSettings();
        _steps.Clear();
        InsertStepInPhaseOrder(new PlacementStepRow
        {
            UnitKey = 1,
            X = 390,
            Y = 352,
            Phase = PlacementPhase.BeforeStart,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Priority1,
            DelayAfterMilliseconds =
                PlacementAuthoringRules
                    .DefaultStepDelayMilliseconds,
        });
        InsertStepInPhaseOrder(new PlacementStepRow
        {
            UnitKey = 2,
            X = 445,
            Y = 394,
            Phase = PlacementPhase.AfterStart,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Priority4,
            DelayAfterStartMilliseconds =
                PlacementAuthoringRules
                    .DefaultAfterStartDelayMilliseconds,
            DelayAfterMilliseconds =
                PlacementAuthoringRules
                    .DefaultStepDelayMilliseconds,
        });
        InsertStepInPhaseOrder(new PlacementStepRow
        {
            UnitKey = 3,
            X = 505,
            Y = 332,
            Phase = PlacementPhase.BeforeStart,
            DelayAfterMilliseconds =
                PlacementAuthoringRules
                    .DefaultStepDelayMilliseconds,
        });
        FastAfterStartButton.IsChecked = true;
        FastStepsList.SelectedIndex = 1;
        FastStatusText.Text = string.Empty;
        UpdateFastPlacementCount();
        if (showRecordingSettings)
        {
            const string recordingId =
                "snapshot-event-angle-2";
            _fastImpossibilityThresholdMinutes =
                18;
            _fastManualRecordingId =
                recordingId;
            FastEditorPanel.SetManualRecordingMode(
                enabled: true,
                recordingId,
                [
                    new ManualRecordingChoice(
                        recordingId,
                        "Event Act 1 Angle 2 run"),
                ]);
            FastEditorPanel.SetSnapshotSettings(
                _fastPlacementIntervalMilliseconds,
                _fastDefaultAfterStartDelayMilliseconds,
                _fastImpossibilityThresholdMinutes,
                recordingId,
                "Event Act 1 Angle 2 run");
        }
    }

    private void ApplyWorkflowMode()
    {
        bool fast = FastWorkflow;
        ModelsList.ItemsSource =
            fast ? _setupRows : _models;
        ApplyCatalogContentVisibility();
        ModelsHeaderText.Text =
            fast
                ? "PLACEMENT SETUP"
                : "PLACEMENT MODELS";
        LegacyEditorPanel.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        FastEditorPanel.Visibility =
            fast ? Visibility.Visible : Visibility.Collapsed;
        PageHeading.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        if (_selectedModel is not null &&
            _selectedModel.CameraPreparationMode ==
                (fast
                    ? CameraPreparationMode.FastNoAlign
                    : CameraPreparationMode.CameraModel))
        {
            ApplyModel(_selectedModel);
        }
        else
        {
            ApplyNewModelDefaults();
        }
        UpdateBusyState();
    }
}
