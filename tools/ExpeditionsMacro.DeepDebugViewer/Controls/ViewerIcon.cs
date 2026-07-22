using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExpeditionsMacro.DeepDebugViewer.Controls;

public enum ViewerIconKind
{
    None,
    ChevronLeft,
    ChevronRight,
    CircleAlert,
    FileArchive,
    FolderOpen,
    ListTree,
    Pause,
    Play,
    X,
}

public static class ViewerIcon
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon",
        typeof(ViewerIconKind),
        typeof(ViewerIcon),
        new FrameworkPropertyMetadata(ViewerIconKind.None));

    public static ViewerIconKind GetIcon(DependencyObject element) =>
        (ViewerIconKind)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, ViewerIconKind value) =>
        element.SetValue(IconProperty, value);
}

// Geometry is ported from the matching Lucide SVG assets. Keeping it native
// preserves crisp scaling and theme inheritance without shipping an icon font.
public sealed class ViewerIconControl : Control
{
    private const double ViewBoxSize = 24;

    private static readonly IReadOnlyDictionary<ViewerIconKind, Geometry> Icons = CreateIcons();

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(ViewerIconKind),
        typeof(ViewerIconControl),
        new FrameworkPropertyMetadata(
            ViewerIconKind.None,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public ViewerIconControl()
    {
        Focusable = false;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    public ViewerIconKind Icon
    {
        get => (ViewerIconKind)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        double width = double.IsNaN(Width) ? 16 : Width;
        double height = double.IsNaN(Height) ? 16 : Height;
        return new Size(Math.Min(width, constraint.Width), Math.Min(height, constraint.Height));
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Icon == ViewerIconKind.None || !Icons.TryGetValue(Icon, out Geometry? geometry)) return;
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        double scale = size / ViewBoxSize;
        Pen pen = new(Foreground ?? Brushes.Black, 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        drawingContext.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, pen, geometry);
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static IReadOnlyDictionary<ViewerIconKind, Geometry> CreateIcons() =>
        new Dictionary<ViewerIconKind, Geometry>
        {
            [ViewerIconKind.ChevronLeft] = Path("m15 18-6-6 6-6"),
            [ViewerIconKind.ChevronRight] = Path("m9 18 6-6-6-6"),
            [ViewerIconKind.CircleAlert] = Group(Circle(12, 12, 10), Path("M12 8v4"), Path("M12 16h.01")),
            [ViewerIconKind.FileArchive] = Group(
                Path("M10 12v-1"),
                Path("M10 18v-2"),
                Path("M10 7V6"),
                Path("M14 2v4a2 2 0 0 0 2 2h4"),
                Path("M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7z")),
            [ViewerIconKind.FolderOpen] = Path("m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"),
            [ViewerIconKind.ListTree] = Group(Path("M21 12h-8"), Path("M21 6H8"), Path("M21 18h-8"), Path("M3 6v4c0 1.1.9 2 2 2h3"), Path("M3 10v6c0 1.1.9 2 2 2h3")),
            [ViewerIconKind.Pause] = Group(Path("M8 5v14"), Path("M16 5v14")),
            [ViewerIconKind.Play] = Path("M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z"),
            [ViewerIconKind.X] = Group(Path("M18 6 6 18"), Path("m6 6 12 12")),
        };

    private static Geometry Path(string data) => Geometry.Parse(data);

    private static Geometry Circle(double x, double y, double radius) =>
        new EllipseGeometry(new Point(x, y), radius, radius);

    private static Geometry Group(params Geometry[] geometries)
    {
        GeometryGroup group = new();
        foreach (Geometry geometry in geometries) group.Children.Add(geometry);
        group.Freeze();
        return group;
    }
}
