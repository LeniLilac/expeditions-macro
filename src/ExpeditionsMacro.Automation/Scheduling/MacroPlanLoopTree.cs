using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Scheduling;

internal sealed class MacroPlanLoopNode
{
    public MacroPlanLoopNode(
        MacroPlanLoopDefinition definition,
        int start,
        int stop)
    {
        Definition = definition;
        Start = start;
        Stop = stop;
    }

    public MacroPlanLoopDefinition Definition { get; }

    public int Start { get; }

    public int Stop { get; }

    public List<MacroPlanLoopNode> Children { get; } = [];

    public IEnumerable<MacroPlanLoopNode> Descendants()
    {
        foreach (MacroPlanLoopNode child in Children)
        {
            yield return child;
            foreach (MacroPlanLoopNode descendant in
                     child.Descendants())
            {
                yield return descendant;
            }
        }
    }
}

internal static class MacroPlanLoopTree
{
    public static IReadOnlyList<MacroPlanLoopNode> Build(
        MacroPlan plan)
    {
        MacroPlanLoopNode[] nodes = plan
            .EffectiveLoops()
            .Select(loop =>
            {
                (int start, int stop) =
                    loop.ResolveRange(plan.Tasks);
                return new MacroPlanLoopNode(
                    loop,
                    start,
                    stop);
            })
            .OrderBy(node => node.Start)
            .ThenByDescending(node => node.Stop)
            .ThenByDescending(
                node => node.Definition.Forever)
            .ToArray();
        List<MacroPlanLoopNode> roots = [];
        List<MacroPlanLoopNode> ancestors = [];
        foreach (MacroPlanLoopNode node in nodes)
        {
            while (ancestors.Count != 0 &&
                   !Contains(ancestors[^1], node))
            {
                ancestors.RemoveAt(
                    ancestors.Count - 1);
            }
            if (ancestors.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                ancestors[^1].Children.Add(node);
            }
            ancestors.Add(node);
        }
        return roots;
    }

    private static bool Contains(
        MacroPlanLoopNode parent,
        MacroPlanLoopNode child) =>
        parent.Start <= child.Start &&
        parent.Stop >= child.Stop;
}
