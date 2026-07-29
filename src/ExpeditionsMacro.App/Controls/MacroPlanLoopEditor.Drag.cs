using System.Windows;
using System.Windows.Controls;
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
    private readonly MacroPlanSiblingInsertionIndicator
        _siblingInsertionIndicator = new();
    private readonly MacroPlanDragAutoScroller
        _dragAutoScroller = new();

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
        _dragAutoScroller.ScrollNearEdge(this, e);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void SiblingZone_DragLeave(
        object sender,
        DragEventArgs e)
    {
        if (sender is Border zone &&
            _siblingInsertionIndicator.IsActive(zone) &&
            !zone.IsMouseOver)
        {
            ClearSiblingInsertionIndicator();
        }
    }

    private void BlockCard_DragOver(
        object sender,
        DragEventArgs e)
    {
        if (sender is not Border card ||
            card.DataContext is not
                MacroPlanBlockNode target ||
            !e.Data.GetDataPresent(BlockDataFormat))
        {
            e.Effects = DragDropEffects.None;
            ClearSiblingInsertionIndicator();
            e.Handled = true;
            return;
        }

        bool after =
            e.GetPosition(card).Y >=
            card.ActualHeight / 2;
        ShowSiblingInsertionIndicator(
            card,
            target,
            after);
        _dragAutoScroller.ScrollNearEdge(this, e);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void BlockCard_DragLeave(
        object sender,
        DragEventArgs e)
    {
        SiblingZone_DragLeave(sender, e);
    }

    private void BlockCard_Drop(
        object sender,
        DragEventArgs e)
    {
        ClearSiblingInsertionIndicator();
        if (!TryReadDrag(
                e,
                out MacroPlanBlockNode dragged) ||
            sender is not Border card ||
            card.DataContext is not
                MacroPlanBlockNode target)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool after =
            e.GetPosition(card).Y >=
            card.ActualHeight / 2;
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

    private void MarkDrag(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(BlockDataFormat))
        {
            _dragAutoScroller.ScrollNearEdge(this, e);
        }
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
        FrameworkElement zone,
        MacroPlanBlockNode target,
        bool after)
        => _siblingInsertionIndicator.Show(
            RootBlocks,
            StructureTree,
            zone,
            target,
            after,
            TryFindResource("AccentBrush")
                as Brush ??
            Brushes.SlateBlue);

    private void ClearSiblingInsertionIndicator()
        => _siblingInsertionIndicator.Clear();
}
