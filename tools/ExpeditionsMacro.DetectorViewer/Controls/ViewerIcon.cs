using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public enum ViewerIconKind
{
    None,
    AlertCircle,
    Check,
    ChevronLeft,
    ChevronRight,
    Crosshair,
    Folder,
    FolderOpen,
    Image,
    Info,
    Layers,
    Maximize,
    Moon,
    Search,
    Sun,
    Tag,
    X,
    ZoomIn,
    ZoomOut,
}

public static class ViewerIcon
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached(
            "Icon",
            typeof(ViewerIconKind),
            typeof(ViewerIcon),
            new FrameworkPropertyMetadata(
                ViewerIconKind.None));

    public static ViewerIconKind GetIcon(
        DependencyObject element) =>
        (ViewerIconKind)element.GetValue(
            IconProperty);

    public static void SetIcon(
        DependencyObject element,
        ViewerIconKind value) =>
        element.SetValue(
            IconProperty,
            value);
}

// Geometry is ported from matching Lucide SVG assets. Native vectors stay
// crisp at Windows scaling and inherit the current semantic foreground.
public sealed class ViewerIconControl : Control
{
    private const double ViewBoxSize = 24;
    private static readonly IReadOnlyDictionary<
        ViewerIconKind,
        Geometry> Icons = CreateIcons();

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(ViewerIconKind),
            typeof(ViewerIconControl),
            new FrameworkPropertyMetadata(
                ViewerIconKind.None,
                FrameworkPropertyMetadataOptions
                    .AffectsMeasure |
                FrameworkPropertyMetadataOptions
                    .AffectsRender));

    public ViewerIconControl()
    {
        Focusable = false;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    public ViewerIconKind Icon
    {
        get =>
            (ViewerIconKind)GetValue(
                IconProperty);
        set => SetValue(
            IconProperty,
            value);
    }

    protected override Size MeasureOverride(
        Size constraint)
    {
        double width =
            double.IsNaN(Width)
                ? 16
                : Width;
        double height =
            double.IsNaN(Height)
                ? 16
                : Height;
        return new Size(
            Math.Min(width, constraint.Width),
            Math.Min(height, constraint.Height));
    }

    protected override void OnRender(
        DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Icon == ViewerIconKind.None ||
            !Icons.TryGetValue(
                Icon,
                out Geometry? geometry))
        {
            return;
        }
        double size =
            Math.Min(
                ActualWidth,
                ActualHeight);
        if (size <= 0)
        {
            return;
        }
        double scale = size / ViewBoxSize;
        Pen pen = new(
            Foreground ??
            Brushes.Black,
            2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        drawingContext.PushTransform(
            new TranslateTransform(
                (ActualWidth - size) / 2,
                (ActualHeight - size) / 2));
        drawingContext.PushTransform(
            new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(
            null,
            pen,
            geometry);
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static IReadOnlyDictionary<
        ViewerIconKind,
        Geometry> CreateIcons() =>
        new Dictionary<ViewerIconKind, Geometry>
        {
            [ViewerIconKind.AlertCircle] =
                Group(
                    Circle(12, 12, 10),
                    Path("M12 8v4"),
                    Path("M12 16h.01")),
            [ViewerIconKind.Check] =
                Path("M20 6 9 17l-5-5"),
            [ViewerIconKind.ChevronLeft] =
                Path("m15 18-6-6 6-6"),
            [ViewerIconKind.ChevronRight] =
                Path("m9 18 6-6-6-6"),
            [ViewerIconKind.Crosshair] =
                Group(
                    Circle(12, 12, 3),
                    Path("M12 2v4"),
                    Path("M12 18v4"),
                    Path("M2 12h4"),
                    Path("M18 12h4")),
            [ViewerIconKind.Folder] =
                Path("M20 20H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H20a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2Z"),
            [ViewerIconKind.FolderOpen] =
                Path("m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"),
            [ViewerIconKind.Image] =
                Group(
                    Path("M14.5 4h-9A2.5 2.5 0 0 0 3 6.5v11A2.5 2.5 0 0 0 5.5 20h13a2.5 2.5 0 0 0 2.5-2.5v-8"),
                    Circle(10, 10, 2),
                    Path("m21 15-3.1-3.1a2 2 0 0 0-2.8 0L6 21"),
                    Path("m21 3-5 5"),
                    Path("m16 3 5 5")),
            [ViewerIconKind.Info] =
                Group(
                    Circle(12, 12, 10),
                    Path("M12 16v-4"),
                    Path("M12 8h.01")),
            [ViewerIconKind.Layers] =
                Group(
                    Path("m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z"),
                    Path("m22 12.5-9.17 4.17a2 2 0 0 1-1.66 0L2 12.5"),
                    Path("m22 17.5-9.17 4.17a2 2 0 0 1-1.66 0L2 17.5")),
            [ViewerIconKind.Maximize] =
                Group(
                    Path("M8 3H5a2 2 0 0 0-2 2v3"),
                    Path("M16 3h3a2 2 0 0 1 2 2v3"),
                    Path("M8 21H5a2 2 0 0 1-2-2v-3"),
                    Path("M16 21h3a2 2 0 0 0 2-2v-3")),
            [ViewerIconKind.Moon] =
                Path("M20.985 12.486A9 9 0 1 1 11.514 3.015c.36-.033.742.236.628.579a6 6 0 0 0 7.305 7.305c.343-.114.612.268.579.628Z"),
            [ViewerIconKind.Search] =
                Group(
                    Circle(11, 11, 8),
                    Path("m21 21-4.3-4.3")),
            [ViewerIconKind.Sun] =
                Group(
                    Circle(12, 12, 4),
                    Path("M12 2v2"),
                    Path("M12 20v2"),
                    Path("m4.93 4.93 1.41 1.41"),
                    Path("m17.66 17.66 1.41 1.41"),
                    Path("M2 12h2"),
                    Path("M20 12h2"),
                    Path("m6.34 17.66-1.41 1.41"),
                    Path("m19.07 4.93-1.41 1.41")),
            [ViewerIconKind.Tag] =
                Group(
                    Path("M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z"),
                    Circle(7.5, 7.5, 0.5)),
            [ViewerIconKind.X] =
                Group(
                    Path("M18 6 6 18"),
                    Path("m6 6 12 12")),
            [ViewerIconKind.ZoomIn] =
                Group(
                    Circle(11, 11, 8),
                    Path("m21 21-4.3-4.3"),
                    Path("M11 8v6"),
                    Path("M8 11h6")),
            [ViewerIconKind.ZoomOut] =
                Group(
                    Circle(11, 11, 8),
                    Path("m21 21-4.3-4.3"),
                    Path("M8 11h6")),
        };

    private static Geometry Path(string data) =>
        Geometry.Parse(data);

    private static Geometry Circle(
        double x,
        double y,
        double radius) =>
        new EllipseGeometry(
            new Point(x, y),
            radius,
            radius);

    private static Geometry Group(
        params Geometry[] geometries)
    {
        GeometryGroup group = new();
        foreach (Geometry geometry in geometries)
        {
            group.Children.Add(geometry);
        }
        group.Freeze();
        return group;
    }
}
