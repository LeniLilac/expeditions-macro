using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExpeditionsMacro.App.Controls;

public enum LucideIconKind
{
    None,
    ArrowDown,
    ArrowUp,
    BookOpen,
    Bug,
    Camera,
    Check,
    ChevronDown,
    ChevronLeft,
    ChevronRight,
    CircleCheck,
    CircleDot,
    CircleHelp,
    Compass,
    Copy,
    Crosshair,
    ExternalLink,
    Eye,
    EyeOff,
    FolderOpen,
    FastForward,
    Keyboard,
    MapPin,
    MessagesSquare,
    Minus,
    MousePointerClick,
    Pause,
    Pencil,
    Play,
    Plus,
    RefreshCw,
    RotateCcw,
    Route,
    Save,
    ScanLine,
    ScanSearch,
    Send,
    Settings,
    Shield,
    SlidersHorizontal,
    Square,
    StepForward,
    Swords,
    Trash2,
    Users,
    Workflow,
    X,
}

public static class Lucide
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon",
        typeof(LucideIconKind),
        typeof(Lucide),
        new FrameworkPropertyMetadata(LucideIconKind.None));

    public static LucideIconKind GetIcon(DependencyObject element) => (LucideIconKind)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, LucideIconKind value) => element.SetValue(IconProperty, value);
}

// Geometry is ported from the matching Lucide SVG assets. Lucide uses a
// 24-by-24 view box, round caps/joins, and a two-unit stroke; rendering it as a
// native WPF control keeps theme inheritance and scaling crisp without an icon
// font or embedded browser.
public sealed class LucideIcon : Control
{
    private const double ViewBoxSize = 24;

    private static readonly IReadOnlyDictionary<LucideIconKind, Geometry> Icons = CreateIcons();

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(LucideIconKind),
        typeof(LucideIcon),
        new FrameworkPropertyMetadata(LucideIconKind.None, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(LucideIcon),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    public LucideIcon()
    {
        Focusable = false;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    public LucideIconKind Icon
    {
        get => (LucideIconKind)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
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
        if (Icon == LucideIconKind.None || !Icons.TryGetValue(Icon, out Geometry? geometry)) return;
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;

        double scale = size / ViewBoxSize;
        double left = (ActualWidth - size) / 2;
        double top = (ActualHeight - size) / 2;
        Pen pen = new(Foreground ?? Brushes.Black, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        drawingContext.PushTransform(new TranslateTransform(left, top));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, pen, geometry);
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static IReadOnlyDictionary<LucideIconKind, Geometry> CreateIcons() => new Dictionary<LucideIconKind, Geometry>
    {
        [LucideIconKind.ArrowDown] = Group(Path("M12 5v14"), Path("m19 12-7 7-7-7")),
        [LucideIconKind.ArrowUp] = Group(Path("m5 12 7-7 7 7"), Path("M12 19V5")),
        [LucideIconKind.BookOpen] = Group(
            Path("M12 7v14"),
            Path("M3 18a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h5a4 4 0 0 1 4 4 4 4 0 0 1 4-4h5a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1h-6a3 3 0 0 0-3 3 3 3 0 0 0-3-3z")),
        [LucideIconKind.Bug] = Group(
            Path("m8 2 1.88 1.88"),
            Path("M14.12 3.88 16 2"),
            Path("M9 7.13v-1a3 3 0 1 1 6 0v1"),
            Path("M12 20c-3.3 0-6-2.7-6-6v-3a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v3c0 3.3-2.7 6-6 6"),
            Path("M12 20v-9"),
            Path("M6.53 9C4.6 8.8 3 7.1 3 5"),
            Path("M6 13H2"),
            Path("M3 21c0-2.1 1.7-3.9 3.8-4"),
            Path("M17.47 9C19.4 8.8 21 7.1 21 5"),
            Path("M18 13h4"),
            Path("M21 21c0-2.1-1.7-3.9-3.8-4")),
        [LucideIconKind.Camera] = Group(
            Path("M13.997 4a2 2 0 0 1 1.76 1.05l.486.9A2 2 0 0 0 18.003 7H20a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2h1.997a2 2 0 0 0 1.759-1.048l.489-.904A2 2 0 0 1 10.004 4z"),
            Circle(12, 13, 3)),
        [LucideIconKind.Check] = Path("m20 6-11 11-5-5"),
        [LucideIconKind.ChevronDown] = Path("m6 9 6 6 6-6"),
        [LucideIconKind.ChevronLeft] = Path("m15 18-6-6 6-6"),
        [LucideIconKind.ChevronRight] = Path("m9 18 6-6-6-6"),
        [LucideIconKind.CircleCheck] = Group(Circle(12, 12, 10), Path("m9 12 2 2 4-4")),
        [LucideIconKind.CircleDot] = Group(Circle(12, 12, 10), Circle(12, 12, 1)),
        [LucideIconKind.CircleHelp] = Group(Circle(12, 12, 10), Path("M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"), Path("M12 17h.01")),
        [LucideIconKind.Compass] = Group(Circle(12, 12, 10), Path("m16.24 7.76-1.804 5.411a2 2 0 0 1-1.265 1.265L7.76 16.24l1.804-5.411a2 2 0 0 1 1.265-1.265z")),
        [LucideIconKind.Copy] = Group(Rectangle(8, 8, 14, 14, 2), Path("M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2")),
        [LucideIconKind.Crosshair] = Group(Circle(12, 12, 10), Line(22, 12, 18, 12), Line(6, 12, 2, 12), Line(12, 6, 12, 2), Line(12, 22, 12, 18)),
        [LucideIconKind.ExternalLink] = Group(Path("M15 3h6v6"), Path("M10 14 21 3"), Path("M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6")),
        [LucideIconKind.Eye] = Group(Path("M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0"), Circle(12, 12, 3)),
        [LucideIconKind.EyeOff] = Group(
            Path("M10.733 5.076A10.744 10.744 0 0 1 12 5c4.76 0 8.637 3.337 9.648 6.652a1 1 0 0 1 0 .696 10.8 10.8 0 0 1-1.052 2.246"),
            Path("M14.084 14.158a3 3 0 0 1-4.242-4.242"),
            Path("M17.479 17.499A10.75 10.75 0 0 1 12 19c-4.76 0-8.637-3.337-9.648-6.652a1 1 0 0 1 0-.696 10.72 10.72 0 0 1 3.9-5.5"),
            Path("m2 2 20 20")),
        [LucideIconKind.FolderOpen] = Path("m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"),
        [LucideIconKind.FastForward] = Group(
            Path("m13 19 9-7-9-7z"),
            Path("m2 19 9-7-9-7z")),
        [LucideIconKind.Keyboard] = Group(Path("M10 8h.01"), Path("M12 12h.01"), Path("M14 8h.01"), Path("M16 12h.01"), Path("M18 8h.01"), Path("M6 8h.01"), Path("M7 16h10"), Path("M8 12h.01"), Rectangle(2, 4, 20, 16, 2)),
        [LucideIconKind.MapPin] = Group(Path("M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0"), Circle(12, 10, 3)),
        [LucideIconKind.MessagesSquare] = Group(Path("M16 10a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 14.286V4a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"), Path("M20 9a2 2 0 0 1 2 2v10.286a.71.71 0 0 1-1.212.502l-2.202-2.202A2 2 0 0 0 17.172 19H10a2 2 0 0 1-2-2v-1")),
        [LucideIconKind.Minus] = Path("M5 12h14"),
        [LucideIconKind.MousePointerClick] = Group(Path("M14 4.1 12 6"), Path("m5.1 8-2.9-.8"), Path("m6 12-1.9 2"), Path("M7.2 2.2 8 5.1"), Path("M9.037 9.69a.498.498 0 0 1 .653-.653l11 4.5a.5.5 0 0 1-.074.949l-4.349 1.041a1 1 0 0 0-.74.739l-1.04 4.35a.5.5 0 0 1-.95.074z")),
        [LucideIconKind.Pause] = Group(Line(8, 5, 8, 19), Line(16, 5, 16, 19)),
        [LucideIconKind.Pencil] = Group(Path("M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z"), Path("m15 5 4 4")),
        [LucideIconKind.Play] = Path("M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z"),
        [LucideIconKind.Plus] = Group(Path("M5 12h14"), Path("M12 5v14")),
        [LucideIconKind.RefreshCw] = Group(Path("M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"), Path("M21 3v5h-5"), Path("M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"), Path("M8 16H3v5")),
        [LucideIconKind.RotateCcw] = Group(Path("M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"), Path("M3 3v5h5")),
        [LucideIconKind.Route] = Group(
            Circle(6, 19, 3),
            Circle(18, 5, 3),
            Path("M9 19h6.5a3.5 3.5 0 0 0 0-7h-7a3.5 3.5 0 0 1 0-7H15")),
        [LucideIconKind.Save] = Group(Path("M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z"), Path("M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7"), Path("M7 3v4a1 1 0 0 0 1 1h7")),
        [LucideIconKind.ScanLine] = Group(Path("M3 7V5a2 2 0 0 1 2-2h2"), Path("M17 3h2a2 2 0 0 1 2 2v2"), Path("M21 17v2a2 2 0 0 1-2 2h-2"), Path("M7 21H5a2 2 0 0 1-2-2v-2"), Path("M7 12h10")),
        [LucideIconKind.ScanSearch] = Group(
            Path("M3 7V5a2 2 0 0 1 2-2h2"),
            Path("M17 3h2a2 2 0 0 1 2 2v2"),
            Path("M21 17v2a2 2 0 0 1-2 2h-2"),
            Path("M7 21H5a2 2 0 0 1-2-2v-2"),
            Circle(11, 11, 3),
            Path("m16 16-2.1-2.1")),
        [LucideIconKind.Send] = Group(Path("M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z"), Path("m21.854 2.147-10.94 10.939")),
        [LucideIconKind.Settings] = Group(Path("M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915"), Circle(12, 12, 3)),
        [LucideIconKind.Shield] = Path("M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"),
        [LucideIconKind.SlidersHorizontal] = Group(Path("M10 5H3"), Path("M12 19H3"), Path("M14 3v4"), Path("M16 17v4"), Path("M21 12h-9"), Path("M21 19h-5"), Path("M21 5h-7"), Path("M8 10v4"), Path("M8 12H3")),
        [LucideIconKind.Square] = Rectangle(3, 3, 18, 18, 2),
        [LucideIconKind.StepForward] = Group(
            Path("M6 5a2 2 0 0 1 3.008-1.728l7.997 4.665a2 2 0 0 1 .003 3.458l-8 4.667A2 2 0 0 1 6 14.334z"),
            Line(20, 4, 20, 20)),
        [LucideIconKind.Swords] = Group(Polyline((14.5, 17.5), (3, 6), (3, 3), (6, 3), (17.5, 14.5)), Line(13, 19, 19, 13), Line(16, 16, 20, 20), Line(19, 21, 21, 19), Polyline((14.5, 6.5), (18, 3), (21, 3), (21, 6), (17.5, 9.5)), Line(5, 14, 9, 18), Line(7, 17, 4, 20), Line(3, 19, 5, 21)),
        [LucideIconKind.Trash2] = Group(Path("M10 11v6"), Path("M14 11v6"), Path("M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"), Path("M3 6h18"), Path("M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2")),
        [LucideIconKind.Users] = Group(
            Path("M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"),
            Circle(9, 7, 4),
            Path("M22 21v-2a4 4 0 0 0-3-3.87"),
            Path("M16 3.13a4 4 0 0 1 0 7.75")),
        [LucideIconKind.Workflow] = Group(Rectangle(3, 3, 8, 8, 2), Path("M7 11v4a2 2 0 0 0 2 2h4"), Rectangle(13, 13, 8, 8, 2)),
        [LucideIconKind.X] = Group(Path("M18 6 6 18"), Path("m6 6 12 12")),
    };

    private static Geometry Path(string data) => Geometry.Parse(data);

    private static Geometry Rectangle(double x, double y, double width, double height, double radius) =>
        new RectangleGeometry(new Rect(x, y, width, height), radius, radius);

    private static Geometry Circle(double x, double y, double radius) =>
        new EllipseGeometry(new Point(x, y), radius, radius);

    private static Geometry Line(double x1, double y1, double x2, double y2) =>
        new LineGeometry(new Point(x1, y1), new Point(x2, y2));

    private static Geometry Polyline(params (double X, double Y)[] points)
    {
        PathFigure figure = new() { StartPoint = new Point(points[0].X, points[0].Y) };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1).Select(point => new Point(point.X, point.Y)), isStroked: true));
        return new PathGeometry([figure]);
    }

    private static Geometry Group(params Geometry[] geometries)
    {
        GeometryGroup group = new();
        foreach (Geometry geometry in geometries) group.Children.Add(geometry);
        group.Freeze();
        return group;
    }
}
