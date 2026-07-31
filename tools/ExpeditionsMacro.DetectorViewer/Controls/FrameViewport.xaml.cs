using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public partial class FrameViewport : UserControl
{
    private readonly ViewerSessionModel _session =
        new();
    private DetectorInspectionReport? _report;
    private IReadOnlySet<string> _selectedRegions =
        new HashSet<string>();
    private Point? _panOrigin;
    private double _panHorizontal;
    private double _panVertical;
    private bool _suppressFrameEvent;
    private IReadOnlyList<FramePickerItem> _frameItems =
        Array.Empty<FramePickerItem>();
    private IReadOnlyList<DetectorAnnotationRegion> _annotations = [];
    private Guid? _selectedAnnotationId;
    private readonly AnnotationDrawingController
        _annotationDrawing;

    public FrameViewport()
    {
        InitializeComponent();
        _annotationDrawing =
            new AnnotationDrawingController(
                AnnotationCanvas,
                ImageContent,
                () =>
                    (Brush)FindResource(
                        "WarningBrush"));
        _annotationDrawing.RegionCreated +=
            region =>
                AnnotationRegionCreated?.Invoke(
                    region);
        UpdateNavigation();
    }

    public event EventHandler<int>? FrameIndexRequested;

    public event EventHandler<Point>? PixelHovered;

    public event EventHandler? PixelExited;

    public event Action<DetectorAnnotationRegion>?
        AnnotationRegionCreated;

    public int FrameIndex => _session.FrameIndex;

    public void SetFrameSet(
        IReadOnlyList<string> displayPaths)
    {
        ArgumentNullException.ThrowIfNull(
            displayPaths);
        _frameItems = displayPaths
            .Select((path, index) =>
                new FramePickerItem(
                    index,
                    path))
            .ToArray();
        int frameCount = _frameItems.Count;
        _session.ResetFrames(frameCount);
        _suppressFrameEvent = true;
        FramePicker.ItemsSource = _frameItems;
        FramePicker.SelectedIndex =
            frameCount > 0
                ? 0
                : -1;
        FrameSlider.Minimum = 0;
        FrameSlider.Maximum =
            Math.Max(
                0,
                frameCount - 1);
        FrameSlider.Value = 0;
        _suppressFrameEvent = false;
        UpdateNavigation();
    }

    public void ShowFrame(
        BitmapSource bitmap,
        int index,
        int frameCount,
        string displayName,
        DateTimeOffset? timestamp)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _session.ResetFrames(frameCount);
        _session.SelectFrame(index);
        ImageContent.Width = bitmap.PixelWidth;
        ImageContent.Height = bitmap.PixelHeight;
        FrameImage.Source = bitmap;
        _annotations = [];
        _selectedAnnotationId = null;
        RenderAnnotations();
        EmptyState.Visibility = Visibility.Collapsed;
        ImageScroller.Visibility = Visibility.Visible;
        FrameNumberText.Text =
            $"Frame {index + 1:N0} / {frameCount:N0}";
        TimestampText.Text =
            timestamp?.ToLocalTime()
                .ToString(
                    "yyyy-MM-dd HH:mm:ss.fff zzz",
                    System.Globalization
                        .CultureInfo.InvariantCulture) ??
            "Timestamp unavailable";
        _suppressFrameEvent = true;
        FrameSlider.Maximum =
            Math.Max(
                0,
                frameCount - 1);
        FrameSlider.Value = index;
        FramePicker.SelectedIndex = index;
        if (FramePicker.SelectedIndex < 0)
        {
            FramePicker.Text = displayName;
        }
        _suppressFrameEvent = false;
        UpdateNavigation();
    }

    public void SetAnnotations(
        IReadOnlyList<DetectorAnnotationRegion> annotations,
        Guid? selectedId = null)
    {
        _annotations = annotations ?? [];
        _selectedAnnotationId = selectedId;
        RenderAnnotations();
    }

    public void SetAnnotationMode(bool enabled)
    {
        _annotationDrawing.SetEnabled(enabled);
    }

    public void SetReport(
        DetectorInspectionReport? report,
        IReadOnlySet<string>? selectedRegions = null)
    {
        _report = report;
        _selectedRegions =
            selectedRegions ??
            new HashSet<string>();
        RenderOverlay();
    }

    public void ShowMessage(
        string title,
        string message)
    {
        EmptyTitleText.Text = title;
        EmptyMessageText.Text = message;
        EmptyState.Visibility = Visibility.Visible;
        ImageScroller.Visibility = Visibility.Collapsed;
        FrameNumberText.Text = "No frame";
        TimestampText.Text = "Timestamp unavailable";
        FrameImage.Source = null;
        _report = null;
        OverlayCanvas.Children.Clear();
        AnnotationCanvas.Children.Clear();
        _session.ResetFrames(0);
        _frameItems =
            Array.Empty<FramePickerItem>();
        _suppressFrameEvent = true;
        FramePicker.ItemsSource = null;
        FramePicker.Text = title;
        FramePicker.SelectedIndex = -1;
        _suppressFrameEvent = false;
        UpdateNavigation();
    }

    public void Fit()
    {
        if (FrameImage.Source is null ||
            ImageContent.Width <= 0 ||
            ImageContent.Height <= 0)
        {
            return;
        }
        double width =
            Math.Max(
                1,
                ImageScroller.ViewportWidth - 18);
        double height =
            Math.Max(
                1,
                ImageScroller.ViewportHeight - 18);
        SetZoom(
            Math.Min(
                width / ImageContent.Width,
                height / ImageContent.Height));
        ImageScroller.ScrollToHorizontalOffset(0);
        ImageScroller.ScrollToVerticalOffset(0);
    }

    private void RequestFrame(int index)
    {
        int selected =
            _session.SelectFrame(index);
        UpdateNavigation();
        FrameIndexRequested?.Invoke(
            this,
            selected);
    }

    private void UpdateNavigation()
    {
        PreviousButton.IsEnabled =
            _session.CanMovePrevious;
        NextButton.IsEnabled =
            _session.CanMoveNext;
        FrameSlider.IsEnabled =
            _session.FrameCount > 1;
        FramePicker.IsEnabled =
            _session.FrameCount > 0;
    }

    private void SetZoom(double zoom)
    {
        double value =
            _session.SetZoom(zoom);
        ImageScale.ScaleX = value;
        ImageScale.ScaleY = value;
        ZoomText.Text =
            $"{value * 100:0}%";
    }

    private void RenderOverlay() =>
        DetectorOverlayRenderer.Render(
            OverlayCanvas,
            _report,
            _selectedRegions,
            GeometryCheckBox.IsChecked == true,
            LabelsCheckBox.IsChecked == true);

    private void RenderAnnotations() =>
        AnnotationOverlayRenderer.Render(
            AnnotationCanvas,
            _annotations,
            _selectedAnnotationId,
            (Brush)FindResource("AccentBrush"),
            (Brush)FindResource("WarningBrush"),
            (Brush)FindResource("SurfaceBrush"),
            (Brush)FindResource("TextBrush"));

    private void PreviousButton_Click(
        object sender,
        RoutedEventArgs e) =>
        RequestFrame(
            _session.FrameIndex - 1);

    private void NextButton_Click(
        object sender,
        RoutedEventArgs e) =>
        RequestFrame(
            _session.FrameIndex + 1);

    private void FrameSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_suppressFrameEvent &&
            _session.FrameCount > 0)
        {
            RequestFrame(
                (int)Math.Round(e.NewValue));
        }
    }

    private void FramePicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_suppressFrameEvent &&
            FramePicker.SelectedItem is
                FramePickerItem item &&
            item.Index != _session.FrameIndex)
        {
            RequestFrame(item.Index);
        }
    }

    private void OverlayOption_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }
        _session.ShowGeometry =
            GeometryCheckBox.IsChecked == true;
        _session.ShowLabels =
            LabelsCheckBox.IsChecked == true;
        RenderOverlay();
    }

    private void ZoomOut_Click(
        object sender,
        RoutedEventArgs e) =>
        SetZoom(
            _session.Zoom / 1.2);

    private void ZoomIn_Click(
        object sender,
        RoutedEventArgs e) =>
        SetZoom(
            _session.Zoom * 1.2);

    private void FitButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Fit();

    private void ImageScroller_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control))
        {
            SetZoom(
                e.Delta > 0
                    ? _session.Zoom * 1.12
                    : _session.Zoom / 1.12);
            e.Handled = true;
        }
    }

    private void ImageScroller_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Middle)
        {
            return;
        }
        _panOrigin =
            e.GetPosition(ImageScroller);
        _panHorizontal =
            ImageScroller.HorizontalOffset;
        _panVertical =
            ImageScroller.VerticalOffset;
        ImageScroller.CaptureMouse();
        Mouse.OverrideCursor =
            Cursors.ScrollAll;
        e.Handled = true;
    }

    private void ImageScroller_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_panOrigin is not Point origin ||
            e.MiddleButton !=
            MouseButtonState.Pressed)
        {
            return;
        }
        Point current =
            e.GetPosition(ImageScroller);
        ImageScroller.ScrollToHorizontalOffset(
            _panHorizontal +
            origin.X -
            current.X);
        ImageScroller.ScrollToVerticalOffset(
            _panVertical +
            origin.Y -
            current.Y);
        e.Handled = true;
    }

    private void ImageScroller_PreviewMouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton !=
            MouseButton.Middle)
        {
            return;
        }
        _panOrigin = null;
        ImageScroller.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
        e.Handled = true;
    }

    private void ImageContent_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        Point point =
            e.GetPosition(ImageContent);
        if (_annotationDrawing.Enabled &&
            e.LeftButton == MouseButtonState.Pressed)
        {
            _annotationDrawing.Move(point);
        }
        if (point.X >= 0 &&
            point.Y >= 0 &&
            point.X < ImageContent.Width &&
            point.Y < ImageContent.Height)
        {
            PixelHovered?.Invoke(
                this,
                point);
        }
    }

    private void ImageContent_MouseLeave(
        object sender,
        MouseEventArgs e) =>
        PixelExited?.Invoke(
            this,
            EventArgs.Empty);

    private void ImageContent_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_annotationDrawing.Enabled ||
            FrameImage.Source is null)
        {
            return;
        }
        _annotationDrawing.Begin(
            e.GetPosition(ImageContent));
        e.Handled = true;
    }

    private void ImageContent_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_annotationDrawing.Enabled)
        {
            return;
        }
        _annotationDrawing.Complete(
            e.GetPosition(ImageContent));
        e.Handled = true;
    }
}
