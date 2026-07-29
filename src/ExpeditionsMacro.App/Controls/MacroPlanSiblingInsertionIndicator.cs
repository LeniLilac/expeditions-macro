using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Controls;

internal sealed class MacroPlanSiblingInsertionIndicator
{
    private FrameworkElement? _activeZone;
    private SiblingBoundary? _activeBoundary;
    private AdornerLayer? _layer;
    private SiblingInsertionAdorner? _adorner;

    public bool IsActive(FrameworkElement zone) =>
        ReferenceEquals(_activeZone, zone);

    public void Show(
        ObservableCollection<MacroPlanBlockNode> rootBlocks,
        TreeView structureTree,
        FrameworkElement zone,
        MacroPlanBlockNode target,
        bool after,
        Brush brush)
    {
        if (!MacroPlanStructureMove.TryFindOwner(
                rootBlocks,
                target,
                out ObservableCollection<
                    MacroPlanBlockNode>? owner,
                out _))
        {
            Clear();
            return;
        }

        int targetIndex = owner.IndexOf(target);
        if (targetIndex < 0)
        {
            Clear();
            return;
        }

        SiblingBoundary boundary = new(
            owner,
            targetIndex + (after ? 1 : 0));
        if (_activeBoundary is not null &&
            ReferenceEquals(
                _activeBoundary.Owner,
                boundary.Owner) &&
            _activeBoundary.Index == boundary.Index)
        {
            _activeZone = zone;
            return;
        }

        Clear();
        AdornerLayer? layer =
            AdornerLayer.GetAdornerLayer(
                structureTree);
        if (layer is null)
        {
            return;
        }

        TreeViewItem? container =
            FindVisualAncestor<TreeViewItem>(zone);
        FrameworkElement boundaryElement =
            container ?? zone;
        double localY = after
            ? boundaryElement.ActualHeight
            : 0;
        Point origin =
            boundaryElement.TranslatePoint(
                new Point(0, localY),
                structureTree);
        _layer = layer;
        _adorner = new SiblingInsertionAdorner(
            structureTree,
            origin.X,
            origin.Y,
            boundaryElement.ActualWidth,
            brush);
        layer.Add(_adorner);
        _activeZone = zone;
        _activeBoundary = boundary;
    }

    public void Clear()
    {
        if (_layer is not null &&
            _adorner is not null)
        {
            _layer.Remove(_adorner);
        }

        _activeZone = null;
        _activeBoundary = null;
        _layer = null;
        _adorner = null;
    }

    private static T? FindVisualAncestor<T>(
        DependencyObject source)
        where T : DependencyObject
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current =
                VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private sealed record SiblingBoundary(
        ObservableCollection<MacroPlanBlockNode> Owner,
        int Index);

    private sealed class SiblingInsertionAdorner :
        Adorner
    {
        private readonly double _lineX;
        private readonly double _lineY;
        private readonly double _lineWidth;
        private readonly Pen _pen;

        public SiblingInsertionAdorner(
            UIElement adornedElement,
            double lineX,
            double lineY,
            double lineWidth,
            Brush brush)
            : base(adornedElement)
        {
            _lineX = lineX;
            _lineY = lineY;
            _lineWidth = lineWidth;
            IsHitTestVisible = false;
            _pen = new Pen(brush, 2);
            _pen.Freeze();
        }

        protected override void OnRender(
            DrawingContext drawingContext) =>
            drawingContext.DrawLine(
                _pen,
                new Point(_lineX, _lineY),
                new Point(
                    _lineX + _lineWidth,
                    _lineY));
    }
}
