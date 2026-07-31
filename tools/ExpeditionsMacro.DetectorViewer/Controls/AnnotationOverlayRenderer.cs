using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExpeditionsMacro.DetectorViewer.Models;

namespace ExpeditionsMacro.DetectorViewer.Controls;

internal static class AnnotationOverlayRenderer
{
    public static void Render(
        Canvas canvas,
        IReadOnlyList<DetectorAnnotationRegion> regions,
        Guid? selectedId,
        Brush accent,
        Brush selected,
        Brush labelBackground,
        Brush labelForeground)
    {
        canvas.Children.Clear();
        foreach (DetectorAnnotationRegion region in regions)
        {
            bool isSelected =
                region.Id == selectedId;
            Rectangle rectangle = new()
            {
                Width = region.Width,
                Height = region.Height,
                Stroke = isSelected
                    ? selected
                    : accent,
                StrokeThickness = isSelected
                    ? 3
                    : 2,
                Fill = Transparent(accent),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(rectangle, region.X);
            Canvas.SetTop(rectangle, region.Y);
            canvas.Children.Add(rectangle);

            Border label = new()
            {
                Padding = new Thickness(4, 1, 4, 2),
                Background = labelBackground,
                BorderBrush = rectangle.Stroke,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = region.Label,
                    Foreground = labelForeground,
                    FontSize = 11,
                },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(label, region.X);
            Canvas.SetTop(
                label,
                Math.Max(0, region.Y - 20));
            canvas.Children.Add(label);
        }
    }

    private static Brush Transparent(Brush source)
    {
        Brush brush = source.Clone();
        brush.Opacity = 0.12;
        brush.Freeze();
        return brush;
    }
}
