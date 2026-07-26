using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExpeditionsMacro.App.Controls;

public partial class PlacementFastEditorView : UserControl
{
    private const double MinimumZoom = 0.5;
    private const double MaximumZoom = 2.0;
    private const double ZoomStep = 0.25;
    private const double WheelZoomStep = 0.1;
    private double _placementZoom = 1;
    private bool _isPlacementPanning;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

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

    public void SetStepsInteractionEnabled(bool enabled)
    {
        FastStepsList.IsHitTestVisible = enabled;
        FastStepsList.Focusable = enabled;
        KeyboardNavigation.SetTabNavigation(
            FastStepsList,
            enabled
                ? KeyboardNavigationMode.Continue
                : KeyboardNavigationMode.None);

        if (!enabled)
        {
            FastStopButton.Focus();
        }
    }

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

    private void ZoomOut_Click(
        object sender,
        RoutedEventArgs e) =>
        SetZoom(_placementZoom - ZoomStep);

    private void ZoomIn_Click(
        object sender,
        RoutedEventArgs e) =>
        SetZoom(_placementZoom + ZoomStep);

    private void ResetZoom_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetZoom(1);
        PlacementScrollViewer.ScrollToHorizontalOffset(0);
        PlacementScrollViewer.ScrollToVerticalOffset(0);
    }

    private void PlacementScrollViewer_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        Point viewportPoint =
            e.GetPosition(PlacementScrollViewer);
        Point imagePoint =
            e.GetPosition(PlacementSurface);
        ZoomAt(
            _placementZoom +
            (e.Delta > 0
                ? WheelZoomStep
                : -WheelZoomStep),
            viewportPoint,
            imagePoint);
        e.Handled = true;
    }

    private void PlacementScrollViewer_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPlacementPanning = true;
        _panStartPoint =
            e.GetPosition(PlacementScrollViewer);
        _panStartHorizontalOffset =
            PlacementScrollViewer.HorizontalOffset;
        _panStartVerticalOffset =
            PlacementScrollViewer.VerticalOffset;
        PlacementScrollViewer.Cursor =
            Cursors.ScrollAll;
        Mouse.Capture(
            PlacementScrollViewer,
            CaptureMode.Element);
        e.Handled = true;
    }

    private void PlacementScrollViewer_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (!_isPlacementPanning ||
            e.MiddleButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current =
            e.GetPosition(PlacementScrollViewer);
        PlacementScrollViewer.ScrollToHorizontalOffset(
            _panStartHorizontalOffset -
            (current.X - _panStartPoint.X));
        PlacementScrollViewer.ScrollToVerticalOffset(
            _panStartVerticalOffset -
            (current.Y - _panStartPoint.Y));
        e.Handled = true;
    }

    private void PlacementScrollViewer_PreviewMouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EndPlacementPan();
        e.Handled = true;
    }

    private void PlacementScrollViewer_LostMouseCapture(
        object sender,
        MouseEventArgs e) =>
        EndPlacementPan();

    private void EndPlacementPan()
    {
        _isPlacementPanning = false;
        PlacementScrollViewer.ClearValue(
            CursorProperty);
        if (ReferenceEquals(
                Mouse.Captured,
                PlacementScrollViewer))
        {
            Mouse.Capture(null);
        }
    }

    private void ZoomAt(
        double value,
        Point viewportPoint,
        Point imagePoint)
    {
        SetZoom(value);
        PlacementScrollViewer.UpdateLayout();
        PlacementScrollViewer.ScrollToHorizontalOffset(
            (imagePoint.X * _placementZoom) -
            viewportPoint.X);
        PlacementScrollViewer.ScrollToVerticalOffset(
            (imagePoint.Y * _placementZoom) -
            viewportPoint.Y);
    }

    private void SetZoom(double value)
    {
        _placementZoom = Math.Clamp(
            value,
            MinimumZoom,
            MaximumZoom);
        PlacementZoomTransform.ScaleX =
            _placementZoom;
        PlacementZoomTransform.ScaleY =
            _placementZoom;
        PlacementZoomButton.Content =
            $"{_placementZoom:P0}";
    }
}
