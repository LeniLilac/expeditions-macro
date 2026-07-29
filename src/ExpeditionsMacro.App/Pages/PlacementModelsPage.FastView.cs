using System.Windows.Controls;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private TextBlock FastRouteNameText =>
        FastEditorPanel.FastRouteNameText;

    private ComboBox FastTeamCombo =>
        FastEditorPanel.FastTeamCombo;

    private ComboBox FastManualRecordingCombo =>
        FastEditorPanel.FastManualRecordingCombo;

    private ComboBox TargetModeCombo =>
        FastEditorPanel.TargetModeCombo;

    private ComboBox TargetMapCombo =>
        FastEditorPanel.TargetMapCombo;

    private StackPanel TargetVariantPanel =>
        FastEditorPanel.TargetVariantPanel;

    private TextBlock TargetVariantLabel =>
        FastEditorPanel.TargetVariantLabel;

    private ComboBox TargetVariantCombo =>
        FastEditorPanel.TargetVariantCombo;

    private Image PlacementScreenshot =>
        FastEditorPanel.PlacementScreenshot;

    private Canvas PlacementCanvas =>
        FastEditorPanel.PlacementCanvas;

    private ItemsControl PlacementMarkers =>
        FastEditorPanel.PlacementMarkers;

    private RadioButton FastUnit1Button =>
        FastEditorPanel.FastUnit1Button;

    private RadioButton FastUnit2Button =>
        FastEditorPanel.FastUnit2Button;

    private RadioButton FastUnit3Button =>
        FastEditorPanel.FastUnit3Button;

    private RadioButton FastUnit4Button =>
        FastEditorPanel.FastUnit4Button;

    private RadioButton FastUnit5Button =>
        FastEditorPanel.FastUnit5Button;

    private RadioButton FastUnit6Button =>
        FastEditorPanel.FastUnit6Button;

    private TextBlock FastPlacementCountText =>
        FastEditorPanel.FastPlacementCountText;

    private ListBox FastStepsList =>
        FastEditorPanel.FastStepsList;

    private Button FastTimingButton =>
        FastEditorPanel.FastTimingButton;

    private TextBlock FastStatusText =>
        FastEditorPanel.FastStatusText;

    private TextBlock FastDetailText =>
        FastEditorPanel.FastDetailText;

    private Button FastPrepareButton =>
        FastEditorPanel.FastPrepareButton;

    private Button FastPositionButton =>
        FastEditorPanel.FastPositionButton;

    private Button FastTestButton =>
        FastEditorPanel.FastTestButton;

    private Button FastStopButton =>
        FastEditorPanel.FastStopButton;

    private ProgressBar FastOperationProgress =>
        FastEditorPanel.FastOperationProgress;

    internal void SetCompactSnapshotViewport(
        bool showMatchSteps) =>
        FastEditorPanel.SetCompactSnapshotViewport(
            showMatchSteps);

    internal void SetSnapshotStepSettings(
        PlacementStepRow step)
    {
        PlacementStepRow? target =
            _steps.FirstOrDefault(row =>
                row.Kind ==
                MatchStepKind.Placement);
        if (step.HasPlacementReference &&
            target is not null)
        {
            step.TargetPlacementId =
                target.PlacementId;
            step.UnitKey = target.UnitKey;
            step.SetDisplayUnitId(
                target.DisplayUnitId);
        }
        OpenMatchStepEditor(
            new PlacementStepEditorOpeningEventArgs(
                step,
                null));
    }

    private void WireFastEditorEvents()
    {
        FastEditorPanel.PrepareRequested += FastPrepare_Click;
        FastEditorPanel.PositionRequested +=
            FastPosition_Click;
        FastEditorPanel.TestRequested += Test_Click;
        FastEditorPanel.StopRequested += Stop_Click;
        FastEditorPanel.RemoveStepRequested += RemoveRow_Click;
        FastEditorPanel.MoveStepUpRequested += MoveUp_Click;
        FastEditorPanel.MoveStepDownRequested += MoveDown_Click;
        FastEditorPanel.StepReorderRequested +=
            FastStepReorderRequested;
        FastEditorPanel.TimingSettingsOpening +=
            FastTimingSettingsOpening;
        MatchSettingsDialog.ApplyRequested +=
            FastTimingSettingsApplied;
        MatchSettingsDialog.CancelRequested +=
            MatchSettingsDialog_CancelRequested;
        FastEditorPanel.StepSettingsOpening +=
            FastStepSettingsOpening;
        MatchStepEditorDialog.ApplyRequested +=
            FastStepSettingsApplied;
        MatchStepEditorDialog.CancelRequested +=
            MatchStepEditorDialog_CancelRequested;
        FastEditorPanel.ManualRecordingChanged +=
            FastManualRecording_SelectionChanged;
        FastEditorPanel.PlaybackModeChanged +=
            FastPlaybackMode_Changed;
        FastEditorPanel.UnitChanged +=
            FastUnitButton_Checked;
        FastEditorPanel.ModeChanged +=
            TargetModeCombo_SelectionChanged;
        FastEditorPanel.RouteChanged +=
            TargetRoute_SelectionChanged;
        FastEditorPanel.CanvasClicked +=
            PlacementCanvas_MouseLeftButtonDown;
        FastEditorPanel.MarkerSelected +=
            PlacementMarker_MouseLeftButtonDown;
        FastEditorPanel.MarkerRemoved +=
            PlacementMarker_MouseRightButtonDown;
    }
}
