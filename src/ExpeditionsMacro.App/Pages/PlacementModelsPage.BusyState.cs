namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void UpdateBusyState()
    {
        bool busy =
            _services.Coordinator.IsBusy;
        FastPrepareButton.IsEnabled = !busy;
        FastPositionButton.IsEnabled = !busy;
        FastTestButton.IsEnabled = !busy;
        FastStopButton.IsEnabled = busy;
        TargetModeCombo.IsEnabled = !busy;
        TargetMapCombo.IsEnabled = !busy;
        TargetVariantCombo.IsEnabled = !busy;
        FastTeamCombo.IsEnabled = !busy;
        FastManualRecordingCombo.IsEnabled = !busy;
        FastUnit1Button.IsEnabled = !busy;
        FastUnit2Button.IsEnabled = !busy;
        FastUnit3Button.IsEnabled = !busy;
        FastUnit4Button.IsEnabled = !busy;
        FastUnit5Button.IsEnabled = !busy;
        FastUnit6Button.IsEnabled = !busy;
        FastBeforeStartButton.IsEnabled = !busy;
        FastAfterStartButton.IsEnabled = !busy;
        PlacementCanvas.IsEnabled = !busy;
        FastEditorPanel.SetStepsInteractionEnabled(
            !busy);
        FastTimingButton.IsEnabled = !busy;
        if (busy)
        {
            FastEditorPanel.CloseTimingSettings();
        }
    }
}
