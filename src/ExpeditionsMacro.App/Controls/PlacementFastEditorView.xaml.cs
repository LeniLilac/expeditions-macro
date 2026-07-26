using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExpeditionsMacro.App.Controls;

public partial class PlacementFastEditorView : UserControl
{
    public PlacementFastEditorView()
    {
        InitializeComponent();
        FastTimingEditor.ApplyRequested +=
            FastTimingEditor_ApplyRequested;
    }

    public event RoutedEventHandler? SaveRequested;
    public event RoutedEventHandler? PrepareRequested;
    public event RoutedEventHandler? TestRequested;
    public event RoutedEventHandler? StopRequested;
    public event RoutedEventHandler? RemoveStepRequested;
    public event RoutedEventHandler? MoveStepUpRequested;
    public event RoutedEventHandler? MoveStepDownRequested;
    public event RoutedEventHandler? UnitChanged;
    public event RoutedEventHandler? PhaseChanged;
    public event SelectionChangedEventHandler? ModeChanged;
    public event SelectionChangedEventHandler? RouteChanged;
    public event MouseButtonEventHandler? CanvasClicked;
    public event MouseButtonEventHandler? MarkerSelected;
    public event MouseButtonEventHandler? MarkerRemoved;
    public event EventHandler? TimingSettingsOpening;
    public event EventHandler<PlacementTimingApplyEventArgs>?
        TimingSettingsApplied;

    public void SetTimingSettings(
        int placementIntervalMilliseconds,
        int defaultAfterStartDelayMilliseconds) =>
        FastTimingEditor.SetValues(
            placementIntervalMilliseconds,
            defaultAfterStartDelayMilliseconds);

    public void ShowTimingError(string message) =>
        FastTimingEditor.ShowError(message);

    public void CloseTimingSettings() =>
        FastTimingPopup.IsOpen = false;

    private void Save_Click(
        object sender,
        RoutedEventArgs e) =>
        SaveRequested?.Invoke(sender, e);

    private void FastPrepare_Click(
        object sender,
        RoutedEventArgs e) =>
        PrepareRequested?.Invoke(sender, e);

    private void Test_Click(
        object sender,
        RoutedEventArgs e) =>
        TestRequested?.Invoke(sender, e);

    private void Stop_Click(
        object sender,
        RoutedEventArgs e) =>
        StopRequested?.Invoke(sender, e);

    private void RemoveRow_Click(
        object sender,
        RoutedEventArgs e) =>
        RemoveStepRequested?.Invoke(sender, e);

    private void MoveUp_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveStepUpRequested?.Invoke(sender, e);

    private void MoveDown_Click(
        object sender,
        RoutedEventArgs e) =>
        MoveStepDownRequested?.Invoke(sender, e);

    private void FastTimingButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        FastTimingPopup.IsOpen = true;
        TimingSettingsOpening?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void FastTimingEditor_ApplyRequested(
        object? sender,
        PlacementTimingApplyEventArgs e) =>
        TimingSettingsApplied?.Invoke(this, e);

    private void FastUnitButton_Checked(
        object sender,
        RoutedEventArgs e) =>
        UnitChanged?.Invoke(sender, e);

    private void FastPhaseButton_Checked(
        object sender,
        RoutedEventArgs e) =>
        PhaseChanged?.Invoke(sender, e);

    private void TargetModeCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        ModeChanged?.Invoke(sender, e);

    private void TargetRoute_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        RouteChanged?.Invoke(sender, e);

    private void PlacementCanvas_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) =>
        CanvasClicked?.Invoke(sender, e);

    private void PlacementMarker_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) =>
        MarkerSelected?.Invoke(sender, e);

    private void PlacementMarker_MouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e) =>
        MarkerRemoved?.Invoke(sender, e);
}
