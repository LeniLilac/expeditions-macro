using System.Collections.ObjectModel;

namespace ExpeditionsMacro.App.Models;

internal static class MacroPlanStructureMove
{
    public static bool TryMoveInside(
        ObservableCollection<MacroPlanBlockNode> roots,
        MacroPlanBlockNode dragged,
        MacroPlanLoopBlockNode target,
        out string error)
    {
        if (ReferenceEquals(dragged, target) ||
            dragged is MacroPlanLoopBlockNode loop &&
            MacroPlanStructure.Contains(loop, target))
        {
            error =
                "A loop cannot contain itself.";
            return false;
        }
        if (dragged is MacroPlanLoopBlockNode
            {
                Forever: true,
            })
        {
            error =
                "The Forever loop must remain at the plan level.";
            return false;
        }
        int depth =
            target.Depth + 1 +
            MacroPlanStructure.SubtreeLoopHeight(
                dragged);
        if (depth >
            MacroPlanStructure.MaximumLoopDepth)
        {
            error =
                "Loop nesting is limited to three levels.";
            return false;
        }
        if (!TryDetach(
                roots,
                dragged,
                out _,
                out _))
        {
            error = "The dragged block is no longer in the plan.";
            return false;
        }
        target.Children.Add(dragged);
        error = string.Empty;
        return true;
    }

    public static bool TryMoveBeside(
        ObservableCollection<MacroPlanBlockNode> roots,
        MacroPlanBlockNode dragged,
        MacroPlanBlockNode target,
        bool after,
        out string error)
    {
        if (ReferenceEquals(dragged, target))
        {
            error = string.Empty;
            return true;
        }
        if (dragged is MacroPlanLoopBlockNode loop &&
            MacroPlanStructure.Contains(loop, target))
        {
            error =
                "A loop cannot be moved beside one of its own children.";
            return false;
        }
        if (!TryFindOwner(
                roots,
                target,
                out ObservableCollection<
                    MacroPlanBlockNode>? destination,
                out MacroPlanLoopBlockNode? parent))
        {
            error = "The destination block is no longer in the plan.";
            return false;
        }
        if (dragged is MacroPlanLoopBlockNode
                { Forever: true } &&
            parent is not null)
        {
            error =
                "The Forever loop must remain at the plan level.";
            return false;
        }
        int baseDepth =
            parent is null
                ? 0
                : parent.Depth + 1;
        if (baseDepth +
            MacroPlanStructure.SubtreeLoopHeight(
                dragged) >
            MacroPlanStructure.MaximumLoopDepth)
        {
            error =
                "Loop nesting is limited to three levels.";
            return false;
        }
        if (!TryDetach(
                roots,
                dragged,
                out _,
                out _))
        {
            error = "The dragged block is no longer in the plan.";
            return false;
        }
        int index = destination.IndexOf(target);
        if (index < 0)
        {
            error = "The destination block moved before the drop completed.";
            return false;
        }
        index += after ? 1 : 0;
        InsertAtValidIndex(
            destination,
            dragged,
            index,
            parent is null);
        error = string.Empty;
        return true;
    }

    public static bool TryMoveToRoot(
        ObservableCollection<MacroPlanBlockNode> roots,
        MacroPlanBlockNode dragged,
        out string error)
    {
        if (!TryDetach(
                roots,
                dragged,
                out _,
                out _))
        {
            error = "The dragged block is no longer in the plan.";
            return false;
        }
        InsertAtValidIndex(
            roots,
            dragged,
            roots.Count,
            root: true);
        error = string.Empty;
        return true;
    }

    public static void RemoveLoopAndPromoteChildren(
        ObservableCollection<MacroPlanBlockNode> roots,
        MacroPlanLoopBlockNode loop)
    {
        if (!TryDetach(
                roots,
                loop,
                out ObservableCollection<
                    MacroPlanBlockNode>? owner,
                out int index))
        {
            return;
        }
        MacroPlanBlockNode[] children =
            loop.Children.ToArray();
        loop.Children.Clear();
        foreach (MacroPlanBlockNode child in children)
        {
            owner.Insert(index++, child);
        }
    }

    public static void MakeForever(
        ObservableCollection<MacroPlanBlockNode> roots,
        MacroPlanLoopBlockNode selected)
    {
        selected.Forever = true;
        foreach (MacroPlanLoopBlockNode loop in
                 MacroPlanStructure.FlattenLoops(roots))
        {
            if (!ReferenceEquals(loop, selected))
            {
                loop.Forever = false;
            }
        }
        TryDetach(
            roots,
            selected,
            out _,
            out _);
        roots.Add(selected);
    }

    private static void InsertAtValidIndex(
        ObservableCollection<MacroPlanBlockNode> destination,
        MacroPlanBlockNode dragged,
        int requested,
        bool root)
    {
        if (root &&
            dragged is MacroPlanLoopBlockNode
                { Forever: true })
        {
            destination.Add(dragged);
            return;
        }
        int index = Math.Clamp(
            requested,
            0,
            destination.Count);
        if (root)
        {
            index = Math.Min(
                index,
                MacroPlanStructure
                    .IndexBeforeForever(destination));
        }
        destination.Insert(index, dragged);
    }

    private static bool TryDetach(
        ObservableCollection<MacroPlanBlockNode> roots,
        MacroPlanBlockNode node,
        out ObservableCollection<MacroPlanBlockNode> owner,
        out int index)
    {
        if (!TryFindOwner(
                roots,
                node,
                out owner,
                out _))
        {
            index = -1;
            return false;
        }
        index = owner.IndexOf(node);
        if (index < 0)
        {
            return false;
        }
        owner.RemoveAt(index);
        return true;
    }

    private static bool TryFindOwner(
        ObservableCollection<MacroPlanBlockNode> nodes,
        MacroPlanBlockNode target,
        out ObservableCollection<MacroPlanBlockNode> owner,
        out MacroPlanLoopBlockNode? parent) =>
        TryFindOwner(
            nodes,
            target,
            parentForNodes: null,
            out owner,
            out parent);

    private static bool TryFindOwner(
        ObservableCollection<MacroPlanBlockNode> nodes,
        MacroPlanBlockNode target,
        MacroPlanLoopBlockNode? parentForNodes,
        out ObservableCollection<MacroPlanBlockNode> owner,
        out MacroPlanLoopBlockNode? parent)
    {
        if (nodes.Contains(target))
        {
            owner = nodes;
            parent = parentForNodes;
            return true;
        }
        foreach (MacroPlanLoopBlockNode loop in
                 nodes.OfType<
                     MacroPlanLoopBlockNode>())
        {
            if (TryFindOwner(
                    loop.Children,
                    target,
                    loop,
                    out owner,
                    out parent))
            {
                return true;
            }
        }
        owner = null!;
        parent = null;
        return false;
    }
}
