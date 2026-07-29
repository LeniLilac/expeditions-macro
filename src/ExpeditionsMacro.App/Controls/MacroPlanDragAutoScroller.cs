using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ExpeditionsMacro.App.Controls;

internal sealed class MacroPlanDragAutoScroller
{
    private static readonly TimeSpan ScrollInterval =
        TimeSpan.FromMilliseconds(80);
    private DateTimeOffset _lastScroll;

    public void ScrollNearEdge(
        FrameworkElement editor,
        DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(e);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastScroll < ScrollInterval)
        {
            return;
        }

        ScrollViewer? viewer =
            FindVisualAncestor<ScrollViewer>(editor);
        if (viewer is null ||
            viewer.ViewportHeight <= 0)
        {
            return;
        }

        const double edge = 30;
        double y = e.GetPosition(viewer).Y;
        if (y < edge)
        {
            viewer.LineUp();
            _lastScroll = now;
        }
        else if (y > viewer.ViewportHeight - edge)
        {
            viewer.LineDown();
            _lastScroll = now;
        }
    }

    private static T? FindVisualAncestor<T>(
        DependencyObject source)
        where T : DependencyObject
    {
        for (DependencyObject? current =
                 VisualTreeHelper.GetParent(source);
             current is not null;
             current =
                 VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
