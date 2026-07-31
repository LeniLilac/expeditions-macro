using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.DetectorViewer.Services;

namespace ExpeditionsMacro.DetectorViewer.Controls;

internal sealed class AnnotationDrawingController
{
    private readonly Canvas _canvas;
    private readonly FrameworkElement _image;
    private readonly Func<Brush> _stroke;
    private Point? _origin;
    private Rectangle? _draft;

    public AnnotationDrawingController(
        Canvas canvas,
        FrameworkElement image,
        Func<Brush> stroke)
    {
        _canvas = canvas;
        _image = image;
        _stroke = stroke;
    }

    public event Action<DetectorAnnotationRegion>?
        RegionCreated;

    public bool Enabled { get; private set; }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        _image.Cursor = enabled
            ? Cursors.Cross
            : Cursors.Arrow;
        if (!enabled)
        {
            Cancel();
        }
    }

    public void Begin(Point point)
    {
        if (!Enabled)
        {
            return;
        }
        _origin = Clamp(point);
        _draft = new Rectangle
        {
            Stroke = _stroke(),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection(
                [4, 3]),
            Fill = Brushes.Transparent,
        };
        _canvas.Children.Add(_draft);
        _image.CaptureMouse();
    }

    public void Move(Point point)
    {
        if (_origin is not Point origin ||
            _draft is null)
        {
            return;
        }
        Point end = Clamp(point);
        Canvas.SetLeft(
            _draft,
            Math.Min(origin.X, end.X));
        Canvas.SetTop(
            _draft,
            Math.Min(origin.Y, end.Y));
        _draft.Width =
            Math.Abs(origin.X - end.X);
        _draft.Height =
            Math.Abs(origin.Y - end.Y);
    }

    public void Complete(Point point)
    {
        if (_origin is not Point origin)
        {
            return;
        }
        Point end = Clamp(point);
        DetectorAnnotationRegion? region =
            DetectorAnnotationGeometry.CreateRegion(
                origin.X,
                origin.Y,
                end.X,
                end.Y,
                (int)_image.Width,
                (int)_image.Height);
        Cancel();
        if (region is not null)
        {
            RegionCreated?.Invoke(region);
        }
    }

    private void Cancel()
    {
        _origin = null;
        if (_draft is not null)
        {
            _canvas.Children.Remove(_draft);
            _draft = null;
        }
        _image.ReleaseMouseCapture();
    }

    private Point Clamp(Point point) =>
        new(
            Math.Clamp(
                point.X,
                0,
                _image.Width),
            Math.Clamp(
                point.Y,
                0,
                _image.Height));
}
