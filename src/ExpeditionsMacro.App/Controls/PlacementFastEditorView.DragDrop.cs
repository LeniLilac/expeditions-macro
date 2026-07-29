using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public sealed class PlacementStepReorderEventArgs(
    PlacementStepRow source,
    PlacementStepRow target,
    bool insertAfter) : EventArgs
{
    public PlacementStepRow Source { get; } = source;

    public PlacementStepRow Target { get; } = target;

    public bool InsertAfter { get; } = insertAfter;
}

public partial class PlacementFastEditorView
{
    private Point? _stepDragOrigin;
    private PlacementStepRow? _draggedStep;
    private ListBoxItem? _adornedStep;
    private StepInsertionAdorner? _stepInsertionAdorner;
    private DateTimeOffset _lastStepAutoScroll;

    public event EventHandler<PlacementStepReorderEventArgs>?
        StepReorderRequested;

    private void StepDragHandle_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement
            {
                DataContext: PlacementStepRow row,
            })
        {
            return;
        }

        _draggedStep = row;
        _stepDragOrigin =
            e.GetPosition(FastStepsList);
        FastStepsList.SelectedItem = row;
        Mouse.Capture(FastStepsList);
        e.Handled = true;
    }

    private void FastStepsList_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        _draggedStep = null;
        _stepDragOrigin = null;
        if (ReferenceEquals(
                Mouse.Captured,
                FastStepsList))
        {
            Mouse.Capture(null);
        }
    }

    private void FastStepsList_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_draggedStep is null ||
            _stepDragOrigin is null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(FastStepsList);
        if (Math.Abs(
                current.X -
                _stepDragOrigin.Value.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(
                current.Y -
                _stepDragOrigin.Value.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        PlacementStepRow dragged = _draggedStep;
        try
        {
            Mouse.Capture(null);
            DragDrop.DoDragDrop(
                FastStepsList,
                new DataObject(
                    typeof(PlacementStepRow),
                    dragged),
                DragDropEffects.Move);
        }
        finally
        {
            ClearStepInsertionAdorner();
            _draggedStep = null;
            _stepDragOrigin = null;
            if (ReferenceEquals(
                    Mouse.Captured,
                    FastStepsList))
            {
                Mouse.Capture(null);
            }
        }
    }

    private void FastStepsList_DragOver(
        object sender,
        DragEventArgs e)
    {
        if (!TryGetDraggedStep(
                e.Data,
                out PlacementStepRow? dragged) ||
            !TryFindDropTarget(
                e.GetPosition(FastStepsList),
                out PlacementStepRow? target,
                out ListBoxItem? container,
                out bool insertAfter))
        {
            e.Effects = DragDropEffects.None;
            ClearStepInsertionAdorner();
            e.Handled = true;
            return;
        }

        ScrollNearEdge(
            e.GetPosition(FastStepsList));
        e.Effects = DragDropEffects.Move;
        ShowStepInsertionAdorner(
            container,
            insertAfter);
        e.Handled = true;
    }

    private void FastStepsList_DragLeave(
        object sender,
        DragEventArgs e)
    {
        if (!FastStepsList.IsMouseOver)
        {
            ClearStepInsertionAdorner();
        }
    }

    private void FastStepsList_Drop(
        object sender,
        DragEventArgs e)
    {
        try
        {
            if (!TryGetDraggedStep(
                    e.Data,
                    out PlacementStepRow? dragged) ||
                !TryFindDropTarget(
                    e.GetPosition(FastStepsList),
                    out PlacementStepRow? target,
                    out _,
                    out bool insertAfter))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            StepReorderRequested?.Invoke(
                this,
                new PlacementStepReorderEventArgs(
                    dragged,
                    target,
                    insertAfter));
            FastStepsList.SelectedItem = dragged;
            FastStepsList.ScrollIntoView(dragged);
            e.Effects = DragDropEffects.Move;
        }
        finally
        {
            ClearStepInsertionAdorner();
            e.Handled = true;
        }
    }

    private bool TryFindDropTarget(
        Point position,
        [NotNullWhen(true)]
        out PlacementStepRow? target,
        [NotNullWhen(true)]
        out ListBoxItem? container,
        out bool insertAfter)
    {
        DependencyObject? hit =
            FastStepsList.InputHitTest(position)
            as DependencyObject;
        container =
            hit is null
                ? null
                : ItemsControl.ContainerFromElement(
                    FastStepsList,
                    hit) as ListBoxItem;
        target = container?.DataContext
            as PlacementStepRow;
        insertAfter =
            container is not null &&
            position.Y -
            container.TranslatePoint(
                new Point(),
                FastStepsList).Y >
            container.ActualHeight / 2;

        if (target is not null &&
            container is not null)
        {
            return true;
        }

        IReadOnlyList<PlacementStepRow> rows =
            FastStepsList.Items
                .OfType<PlacementStepRow>()
                .ToArray();
        if (rows.Count == 0)
        {
            target = null;
            container = null;
            return false;
        }

        target = rows[^1];
        container =
            FastStepsList.ItemContainerGenerator
                .ContainerFromItem(target)
                as ListBoxItem;
        insertAfter = true;
        return container is not null;
    }

    private void ScrollNearEdge(Point position)
    {
        const double edge = 30;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastStepAutoScroll <
            TimeSpan.FromMilliseconds(80))
        {
            return;
        }

        ScrollViewer viewer =
            FastWorkspaceScrollViewer;
        Point viewportPosition =
            FastStepsList.TranslatePoint(
                position,
                viewer);

        if (viewportPosition.Y < edge)
        {
            viewer.LineUp();
            _lastStepAutoScroll = now;
        }
        else if (
            viewportPosition.Y >
                viewer.ViewportHeight - edge)
        {
            viewer.LineDown();
            _lastStepAutoScroll = now;
        }
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0;
             index <
                VisualTreeHelper.GetChildrenCount(
                    parent);
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T match)
            {
                return match;
            }

            T? descendant =
                FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool TryGetDraggedStep(
        IDataObject data,
        [NotNullWhen(true)]
        out PlacementStepRow? step)
    {
        step = data.GetData(
            typeof(PlacementStepRow))
            as PlacementStepRow;
        return step is not null;
    }

    private void ShowStepInsertionAdorner(
        ListBoxItem item,
        bool insertAfter)
    {
        if (ReferenceEquals(item, _adornedStep) &&
            _stepInsertionAdorner?.InsertAfter ==
                insertAfter)
        {
            return;
        }

        ClearStepInsertionAdorner();
        AdornerLayer? layer =
            AdornerLayer.GetAdornerLayer(item);
        if (layer is null) return;

        Brush brush =
            TryFindResource("AccentBrush")
                as Brush ??
            Brushes.SlateBlue;
        _adornedStep = item;
        _stepInsertionAdorner =
            new StepInsertionAdorner(
                item,
                insertAfter,
                brush);
        layer.Add(_stepInsertionAdorner);
    }

    private void ClearStepInsertionAdorner()
    {
        if (_adornedStep is not null &&
            _stepInsertionAdorner is not null)
        {
            AdornerLayer.GetAdornerLayer(
                    _adornedStep)
                ?.Remove(_stepInsertionAdorner);
        }

        _adornedStep = null;
        _stepInsertionAdorner = null;
    }

    private sealed class StepInsertionAdorner :
        Adorner
    {
        private readonly Pen _pen;

        public StepInsertionAdorner(
            UIElement adornedElement,
            bool insertAfter,
            Brush brush)
            : base(adornedElement)
        {
            InsertAfter = insertAfter;
            IsHitTestVisible = false;
            _pen = new Pen(brush, 2);
            _pen.Freeze();
        }

        public bool InsertAfter { get; }

        protected override void OnRender(
            DrawingContext drawingContext)
        {
            double y = InsertAfter
                ? AdornedElement.RenderSize.Height - 1
                : 1;
            drawingContext.DrawLine(
                _pen,
                new Point(0, y),
                new Point(
                    AdornedElement.RenderSize.Width,
                    y));
        }
    }
}
