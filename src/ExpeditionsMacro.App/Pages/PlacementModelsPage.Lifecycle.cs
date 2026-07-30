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
        ClearSnapshotMatchSettings();
        _steps.Clear();
        _steps.Add(new PlacementStepRow
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
        _steps.Add(new PlacementStepRow
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
        _steps.Add(new PlacementStepRow
        {
            UnitKey = 3,
            X = 505,
            Y = 332,
            Phase = PlacementPhase.BeforeStart,
            DelayAfterMilliseconds =
                PlacementAuthoringRules
                    .DefaultStepDelayMilliseconds,
        });
        _steps.Add(new PlacementStepRow
        {
            Kind = MatchStepKind.ReconfigureUnit,
            UnitKey = 1,
            X = 390,
            Y = 352,
            Phase = PlacementPhase.BeforeStart,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.Strongest,
            AutoUpgradeAction =
                MatchAutoUpgradeAction.Priority2,
            DelayAfterMilliseconds =
                PlacementAuthoringRules
                    .DefaultStepDelayMilliseconds,
        });
        _steps.Add(new PlacementStepRow
        {
            Kind = MatchStepKind.Delay,
            UnitKey = 1,
            Phase = PlacementPhase.BeforeStart,
            DelayDurationMilliseconds = 1200,
        });
        _steps.Add(new PlacementStepRow
        {
            Kind = MatchStepKind.UpgradeUnit,
            UnitKey = 2,
            X = 445,
            Y = 394,
            Phase = PlacementPhase.AfterStart,
            DelayAfterStartMilliseconds =
                PlacementAuthoringRules
                    .DefaultAfterStartDelayMilliseconds,
            UpgradeCount = 3,
            DelayAfterMilliseconds =
                PlacementAuthoringRules
                    .DefaultStepDelayMilliseconds,
        });
        _steps.Insert(
            4,
            PlacementStepRow.FromModel(
                PlacementTimelinePolicy
                    .CreateStartGameStep()));
        NormalizeTimelineRows();
        FastStepsList.SelectedIndex = 1;
        FastStatusText.Text = string.Empty;
        UpdateFastPlacementCount();
        const string recordingId =
            "snapshot-event-angle-2";
        _manualRecordingChoices =
        [
            new ManualRecordingChoice(
                recordingId,
                "Event Act 1 Angle 2 run"),
        ];
        if (showRecordingSettings)
        {
            _fastImpossibilityThresholdMinutes =
                18;
            _fastManualRecordingId =
                recordingId;
            SetSnapshotMatchSettings(
                _fastPlacementIntervalMilliseconds,
                _fastPlacementAttempts,
                _fastDefaultTargetingPriority,
                _fastDefaultAutoUpgradePriority,
                _fastDefaultAfterStartDelayMilliseconds,
                _fastImpossibilityThresholdMinutes,
                recordingMode: true,
                _fastAdvancedSettings);
        }
        UpdateFastManualRecordingEditor(
            featureEnabledOverride: true);
    }

}
