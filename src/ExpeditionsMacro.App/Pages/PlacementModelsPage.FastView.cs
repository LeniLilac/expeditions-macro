using System.Windows.Controls;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private TextBlock FastRouteNameText =>
        FastEditorPanel.FastRouteNameText;

    private ComboBox FastTeamCombo =>
        FastEditorPanel.FastTeamCombo;

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

    private Button FastSaveButton =>
        FastEditorPanel.FastSaveButton;

    private Image PlacementScreenshot =>
        FastEditorPanel.PlacementScreenshot;

    private Canvas PlacementCanvas =>
        FastEditorPanel.PlacementCanvas;

    private ItemsControl PlacementMarkers =>
        FastEditorPanel.PlacementMarkers;

    private RadioButton FastBeforeStartButton =>
        FastEditorPanel.FastBeforeStartButton;

    private RadioButton FastAfterStartButton =>
        FastEditorPanel.FastAfterStartButton;

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

    private TextBox FastDefaultDelayText =>
        FastEditorPanel.FastDefaultDelayText;

    private StackPanel FastAfterStartDelayPanel =>
        FastEditorPanel.FastAfterStartDelayPanel;

    private TextBox FastAfterStartDelayText =>
        FastEditorPanel.FastAfterStartDelayText;

    private TextBlock FastPlacementCountText =>
        FastEditorPanel.FastPlacementCountText;

    private ListBox FastStepsList =>
        FastEditorPanel.FastStepsList;

    private TextBlock FastStatusText =>
        FastEditorPanel.FastStatusText;

    private TextBlock FastDetailText =>
        FastEditorPanel.FastDetailText;

    private Button FastPrepareButton =>
        FastEditorPanel.FastPrepareButton;

    private Button FastTestButton =>
        FastEditorPanel.FastTestButton;

    private Button FastStopButton =>
        FastEditorPanel.FastStopButton;

    private ProgressBar FastOperationProgress =>
        FastEditorPanel.FastOperationProgress;

    private void WireFastEditorEvents()
    {
        FastEditorPanel.SaveRequested += Save_Click;
        FastEditorPanel.PrepareRequested += FastPrepare_Click;
        FastEditorPanel.TestRequested += Test_Click;
        FastEditorPanel.StopRequested += Stop_Click;
        FastEditorPanel.RemoveStepRequested += RemoveRow_Click;
        FastEditorPanel.MoveStepUpRequested += MoveUp_Click;
        FastEditorPanel.MoveStepDownRequested += MoveDown_Click;
        FastEditorPanel.UnitChanged +=
            FastUnitButton_Checked;
        FastEditorPanel.PhaseChanged +=
            FastPhaseButton_Checked;
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
