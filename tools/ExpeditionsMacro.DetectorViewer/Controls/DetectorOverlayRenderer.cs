using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Controls;

internal static class DetectorOverlayRenderer
{
    public static void Render(
        Canvas canvas,
        DetectorInspectionReport? report,
        IReadOnlySet<string> selectedRegionIds,
        bool showGeometry,
        bool showLabels)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Children.Clear();
        if (report is null || !showGeometry)
        {
            return;
        }

        Dictionary<string, DetectorInspectionCheckStatus> statuses =
            report.Checks
                .SelectMany(check =>
                    check.RegionIds.Select(region =>
                        (region, check.Status)))
                .GroupBy(item => item.region)
                .ToDictionary(
                    group => group.Key,
                    group => MostSevere(
                        group.Select(item => item.Status)),
                    StringComparer.Ordinal);
        foreach (DetectorInspectionRegion region in report.Regions)
        {
            bool selected =
                selectedRegionIds.Contains(region.Id);
            DetectorInspectionCheckStatus status =
                statuses.GetValueOrDefault(
                    region.Id,
                    DetectorInspectionCheckStatus.NotExposed);
            Brush brush = ResolveStatusBrush(
                canvas,
                status);
            Rectangle outline = new()
            {
                Width = region.Region.Width,
                Height = region.Region.Height,
                Stroke = brush,
                StrokeThickness = selected ? 3 : 1.5,
                Fill = new SolidColorBrush(
                    Color.FromArgb(
                        selected ? (byte)34 : (byte)18,
                        ((SolidColorBrush)brush).Color.R,
                        ((SolidColorBrush)brush).Color.G,
                        ((SolidColorBrush)brush).Color.B)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(
                outline,
                region.Region.X);
            Canvas.SetTop(
                outline,
                region.Region.Y);
            canvas.Children.Add(outline);

            if (showLabels)
            {
                AddLabel(
                    canvas,
                    region,
                    brush);
            }
        }

        if (report.Action is not null)
        {
            AddAction(
                canvas,
                report.Action);
        }
    }

    private static void AddLabel(
        Canvas canvas,
        DetectorInspectionRegion region,
        Brush brush)
    {
        TextBlock text = new()
        {
            Text = $"{region.Label}  {region.Region.X},{region.Region.Y}  {region.Region.Width}×{region.Region.Height}",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground =
                (Brush)canvas.FindResource(
                    "OnAccentBrush"),
            TextTrimming =
                TextTrimming.CharacterEllipsis,
        };
        Border label = new()
        {
            MaxWidth = Math.Max(
                92,
                region.Region.Width),
            Padding = new Thickness(
                5,
                2,
                5,
                2),
            Background = brush,
            Child = text,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(
            label,
            region.Region.X);
        Canvas.SetTop(
            label,
            Math.Max(
                0,
                region.Region.Y - 19));
        canvas.Children.Add(label);
    }

    private static void AddAction(
        Canvas canvas,
        DetectorInspectionPoint action)
    {
        Brush brush =
            (Brush)canvas.FindResource(
                action.IsLive
                    ? "AccentBrush"
                    : "WarningBrush");
        const double radius = 8;
        Ellipse circle = new()
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = brush,
            StrokeThickness = 2,
            IsHitTestVisible = false,
            ToolTip = action.Provenance,
        };
        Canvas.SetLeft(
            circle,
            action.X - radius);
        Canvas.SetTop(
            circle,
            action.Y - radius);
        canvas.Children.Add(circle);
        AddLine(
            canvas,
            action.X - 12,
            action.Y,
            action.X + 12,
            action.Y,
            brush);
        AddLine(
            canvas,
            action.X,
            action.Y - 12,
            action.X,
            action.Y + 12,
            brush);
    }

    private static void AddLine(
        Canvas canvas,
        double x1,
        double y1,
        double x2,
        double y2,
        Brush brush)
    {
        canvas.Children.Add(
            new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = brush,
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
            });
    }

    private static Brush ResolveStatusBrush(
        FrameworkElement element,
        DetectorInspectionCheckStatus status) =>
        (Brush)element.FindResource(
            status switch
            {
                DetectorInspectionCheckStatus.Passed =>
                    "SuccessBrush",
                DetectorInspectionCheckStatus.Failed =>
                    "ErrorBrush",
                DetectorInspectionCheckStatus.Observed =>
                    "WarningBrush",
                _ => "AccentBrush",
            });

    private static DetectorInspectionCheckStatus MostSevere(
        IEnumerable<DetectorInspectionCheckStatus> statuses)
    {
        DetectorInspectionCheckStatus[] values =
            statuses.ToArray();
        if (values.Contains(
                DetectorInspectionCheckStatus.Failed))
        {
            return DetectorInspectionCheckStatus.Failed;
        }
        if (values.Contains(
                DetectorInspectionCheckStatus.Passed))
        {
            return DetectorInspectionCheckStatus.Passed;
        }
        if (values.Contains(
                DetectorInspectionCheckStatus.Observed))
        {
            return DetectorInspectionCheckStatus.Observed;
        }
        return DetectorInspectionCheckStatus.NotExposed;
    }
}
