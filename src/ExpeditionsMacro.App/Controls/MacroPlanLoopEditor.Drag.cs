using System.Windows;
using System.Windows.Input;
using ExpeditionsMacro.App.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class MacroPlanLoopEditor
{
    private const string BlockDataFormat =
        "ExpeditionsMacro.MacroPlanBlock";

    private MacroPlanBlockNode? _dragCandidate;
    private Point _dragStart;

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
        _dragStart = e.GetPosition(this);
    }

    private void DragHandle_MouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (_dragCandidate is null ||
            e.LeftButton !=
                MouseButtonState.Pressed)
        {
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
        DataObject data = new(
            BlockDataFormat,
            dragged);
        RootDropZone.Visibility =
            Visibility.Visible;
        try
        {
            DragDrop.DoDragDrop(
                (DependencyObject)sender,
                data,
                DragDropEffects.Move);
        }
        finally
        {
            _dragCandidate = null;
            RootDropZone.Visibility =
                Visibility.Collapsed;
        }
    }

    private void Block_DragOver(
        object sender,
        DragEventArgs e) =>
        MarkDrag(e);

    private void Root_DragOver(
        object sender,
        DragEventArgs e) =>
        MarkDrag(e);

    private void LoopContents_DragOver(
        object sender,
        DragEventArgs e) =>
        MarkDrag(e);

    private void Block_Drop(
        object sender,
        DragEventArgs e)
    {
        if (!TryReadDrag(
                e,
                out MacroPlanBlockNode dragged) ||
            (sender as FrameworkElement)?.DataContext is not
                MacroPlanBlockNode target)
        {
            return;
        }
        FrameworkElement element =
            (FrameworkElement)sender;
        bool after =
            e.GetPosition(element).Y >
            element.ActualHeight / 2;
        CompleteMove(
            MacroPlanStructureMove.TryMoveBeside(
                RootBlocks,
                dragged,
                target,
                after,
                out string error),
            error,
            e);
    }

    private void LoopContents_Drop(
        object sender,
        DragEventArgs e)
    {
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
}
