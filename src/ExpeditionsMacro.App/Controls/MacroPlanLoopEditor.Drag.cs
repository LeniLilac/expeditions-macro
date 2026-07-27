using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class MacroPlanLoopEditor
{
    private const string BlockDataFormat =
        "ExpeditionsMacro.MacroPlanBlock";
    private MacroPlanBlockNode? _dragCandidate;
    private DependencyObject? _dragSource;
    private Point _dragStart;
    private Border? _activeSiblingZone;
    private SiblingBoundary? _activeSiblingBoundary;
    private AdornerLayer? _siblingInsertionLayer;
    private SiblingInsertionAdorner? _siblingInsertionAdorner;

    private void DragHandle_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_interactionEnabled ||
            (sender as FrameworkElement)?.Tag is not
                MacroPlanBlockNode node)
        {
            return;
        }
        _dragCandidate = node;
        _dragSource = (DependencyObject)sender;
        _dragStart = e.GetPosition(this);
        if (!Mouse.Capture(
                this,
                CaptureMode.Element))
        {
            ClearDragCandidate();
            return;
        }
        e.Handled = true;
    }

    private void DragHandle_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (!_interactionEnabled ||
            (sender as FrameworkElement)?.Tag is not
                MacroPlanBlockNode node ||
            !MacroPlanStructureMove.TryFindOwner(
                RootBlocks,
                node,
                out var owner,
                out MacroPlanLoopBlockNode? parent))
        {
            return;
        }

        int index = owner.IndexOf(node);
        bool alt =
            (e.KeyboardDevice.Modifiers &
                ModifierKeys.Alt) != 0;
        bool moved;
        string error;
        if (!alt &&
            e.Key == Key.Up &&
            index > 0)
        {
            moved =
                MacroPlanStructureMove.TryMoveBeside(
                    RootBlocks,
                    node,
                    owner[index - 1],
                    after: false,
                    out error);
        }
        else if (!alt &&
                 e.Key == Key.Down &&
                 index >= 0 &&
                 index + 1 < owner.Count)
        {
            moved =
                MacroPlanStructureMove.TryMoveBeside(
                    RootBlocks,
                    node,
                    owner[index + 1],
                    after: true,
                    out error);
        }
        else if (alt &&
                 e.Key == Key.Right &&
                 index > 0 &&
                 owner[index - 1] is
                     MacroPlanLoopBlockNode loop)
        {
            moved =
                MacroPlanStructureMove.TryMoveInside(
                    RootBlocks,
                    node,
                    loop,
                    out error);
        }
        else if (alt &&
                 e.Key == Key.Left &&
                 parent is not null)
        {
            moved =
                MacroPlanStructureMove.TryMoveBeside(
                    RootBlocks,
                    node,
                    parent,
                    after: true,
                    out error);
        }
        else
        {
            return;
        }

        e.Handled = true;
        if (!moved)
        {
            ShowValidation(error);
            return;
        }
        CompleteChange();
        if (sender is UIElement focusTarget)
        {
            focusTarget.Focus();
        }
    }

    private void Editor_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_dragCandidate is null ||
            e.LeftButton !=
                MouseButtonState.Pressed)
        {
            if (e.LeftButton !=
                MouseButtonState.Pressed)
            {
                ClearDragCandidate();
            }
            return;
        }
        Point current = e.GetPosition(this);
        if (Math.Abs(
                current.X - _dragStart.X) <
                SystemParameters
                    .MinimumHorizontalDragDistance &&
            Math.Abs(
                current.Y - _dragStart.Y) <
                SystemParameters
                    .MinimumVerticalDragDistance)
        {
            return;
        }
        MacroPlanBlockNode dragged =
            _dragCandidate;
        DependencyObject source =
            _dragSource ?? this;
        ClearDragCandidate();
        DataObject data = new(
            BlockDataFormat,
            dragged);
        RootDropZone.Visibility =
            Visibility.Visible;
        try
        {
            DragDrop.DoDragDrop(
                source,
                data,
                DragDropEffects.Move);
        }
        finally
        {
            ClearSiblingInsertionIndicator();
            RootDropZone.Visibility =
                Visibility.Collapsed;
        }
        e.Handled = true;
    }

    private void Editor_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e) =>
        ClearDragCandidate();

    private void Editor_LostMouseCapture(
        object sender,
        MouseEventArgs e)
    {
        _dragCandidate = null;
        _dragSource = null;
    }

    private void ClearDragCandidate()
    {
        _dragCandidate = null;
        _dragSource = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void Root_DragOver(
        object sender,
        DragEventArgs e)
    {
        ClearSiblingInsertionIndicator();
        MarkDrag(e);
    }

    private void LoopContents_DragOver(
        object sender,
        DragEventArgs e)
    {
        ClearSiblingInsertionIndicator();
        MarkDrag(e);
    }

    private void SiblingZone_DragOver(
        object sender,
        DragEventArgs e)
    {
        if (sender is not Border zone ||
            zone.DataContext is not
                MacroPlanBlockNode target ||
            zone.Tag is not string position ||
            !e.Data.GetDataPresent(BlockDataFormat))
        {
            e.Effects = DragDropEffects.None;
            ClearSiblingInsertionIndicator();
            e.Handled = true;
            return;
        }
        ShowSiblingInsertionIndicator(
            zone,
            target,
            string.Equals(
                position,
                "After",
                StringComparison.Ordinal));
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void SiblingZone_DragLeave(
        object sender,
        DragEventArgs e)
    {
        if (sender is Border zone &&
            ReferenceEquals(
                _activeSiblingZone,
                zone) &&
            !zone.IsMouseOver)
        {
            ClearSiblingInsertionIndicator();
        }
    }

    private void SiblingZone_Drop(
        object sender,
        DragEventArgs e)
    {
        ClearSiblingInsertionIndicator();
        if (!TryReadDrag(
                e,
                out MacroPlanBlockNode dragged) ||
            sender is not Border
            {
                DataContext:
                    MacroPlanBlockNode target,
                Tag: string position,
            })
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        CompleteMove(
            MacroPlanStructureMove.TryMoveBeside(
                RootBlocks,
                dragged,
                target,
                string.Equals(
                    position,
                    "After",
                    StringComparison.Ordinal),
                out string error),
            error,
            e);
    }

    private void LoopContents_Drop(
        object sender,
        DragEventArgs e)
    {
        ClearSiblingInsertionIndicator();
        if (!TryReadDrag(
                e,
                out MacroPlanBlockNode dragged) ||
            (sender as FrameworkElement)?.Tag is not
                MacroPlanLoopBlockNode target)
        {
            return;
        }
        CompleteMove(
            MacroPlanStructureMove.TryMoveInside(
                RootBlocks,
                dragged,
                target,
                out string error),
            error,
            e);
    }

    private void Root_Drop(
        object sender,
        DragEventArgs e)
    {
        ClearSiblingInsertionIndicator();
        if (!TryReadDrag(
                e,
                out MacroPlanBlockNode dragged))
        {
            return;
        }
        CompleteMove(
            MacroPlanStructureMove.TryMoveToRoot(
                RootBlocks,
                dragged,
                out string error),
            error,
            e);
    }

    private void CompleteMove(
        bool moved,
        string error,
        DragEventArgs e)
    {
        e.Handled = true;
        if (!moved)
        {
            ShowValidation(error);
            return;
        }
        CompleteChange();
    }

    private static void MarkDrag(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(
                BlockDataFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool TryReadDrag(
        DragEventArgs e,
        out MacroPlanBlockNode node)
    {
        node = e.Data.GetData(
            BlockDataFormat) as
            MacroPlanBlockNode ?? null!;
        return node is not null;
    }

    private void ShowSiblingInsertionIndicator(
        Border zone,
        MacroPlanBlockNode target,
        bool after)
    {
        if (!MacroPlanStructureMove.TryFindOwner(
                RootBlocks,
                target,
                out ObservableCollection<
                    MacroPlanBlockNode>? owner,
                out _))
        {
            ClearSiblingInsertionIndicator();
            return;
        }
        int targetIndex = owner.IndexOf(target);
        if (targetIndex < 0)
        {
            ClearSiblingInsertionIndicator();
            return;
        }
        int boundaryIndex =
            targetIndex + (after ? 1 : 0);
        SiblingBoundary boundary = new(
            owner,
            boundaryIndex);
        if (_activeSiblingBoundary is not null &&
            ReferenceEquals(
                _activeSiblingBoundary.Owner,
                boundary.Owner) &&
            _activeSiblingBoundary.Index ==
                boundary.Index)
        {
            _activeSiblingZone = zone;
            return;
        }
        ClearSiblingInsertionIndicator();
        AdornerLayer? layer =
            AdornerLayer.GetAdornerLayer(
                StructureTree);
        if (layer is null)
        {
            return;
        }
        bool hasSiblingAcrossBoundary =
            boundaryIndex > 0 &&
            boundaryIndex < owner.Count;
        double localY = hasSiblingAcrossBoundary
            ? after
                ? zone.ActualHeight
                : 0
            : zone.ActualHeight / 2;
        Point origin = zone.TranslatePoint(
            new Point(0, localY),
            StructureTree);
        Brush brush =
            TryFindResource("AccentBrush")
                as Brush ??
            Brushes.SlateBlue;
        _siblingInsertionLayer = layer;
        _siblingInsertionAdorner =
            new SiblingInsertionAdorner(
                StructureTree,
                origin.X,
                origin.Y,
                zone.ActualWidth,
                brush);
        layer.Add(_siblingInsertionAdorner);
        _activeSiblingZone = zone;
        _activeSiblingBoundary = boundary;
    }

    private void ClearSiblingInsertionIndicator()
    {
        if (_siblingInsertionLayer is not null &&
            _siblingInsertionAdorner is not null)
        {
            _siblingInsertionLayer.Remove(
                _siblingInsertionAdorner);
        }
        _activeSiblingZone = null;
        _activeSiblingBoundary = null;
        _siblingInsertionLayer = null;
        _siblingInsertionAdorner = null;
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
