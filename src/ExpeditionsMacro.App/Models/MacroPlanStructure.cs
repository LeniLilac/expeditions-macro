using System.Collections.ObjectModel;
using System.IO;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

internal static class MacroPlanStructure
{
    public const int MaximumLoopDepth =
        MacroPlanLoopDefinition.MaximumNestingDepth;

    public static IReadOnlyList<MacroPlanTaskBlockNode>
        FlattenTasks(
        IEnumerable<MacroPlanBlockNode> nodes) =>
        nodes.SelectMany(node =>
                node is MacroPlanTaskBlockNode task
                    ? [task]
                    : FlattenTasks(
                        ((MacroPlanLoopBlockNode)node)
                            .Children))
            .ToArray();

    public static IReadOnlyList<MacroPlanLoopBlockNode>
        FlattenLoops(
        IEnumerable<MacroPlanBlockNode> nodes) =>
        nodes.SelectMany(node =>
                node is MacroPlanLoopBlockNode loop
                    ? new[] { loop }
                        .Concat(
                            FlattenLoops(loop.Children))
                    : [])
            .ToArray();

    public static IReadOnlyList<MacroPlanLoopDefinition>
        ReadDefinitions(
        IEnumerable<MacroPlanBlockNode> roots,
        IReadOnlyList<MacroTaskDefinition> tasks)
    {
        MacroPlanLoopDefinition[] loops =
            FlattenLoops(roots)
                .Select(ReadDefinition)
                .ToArray();
        MacroPlanLoopDefinition.ValidateAll(
            loops,
            tasks);
        return loops;
    }

    public static void RefreshPresentation(
        IEnumerable<MacroPlanBlockNode> roots)
    {
        IReadOnlyList<MacroPlanTaskBlockNode> tasks =
            FlattenTasks(roots);
        for (int index = 0;
             index < tasks.Count;
             index++)
        {
            tasks[index].Order = index + 1;
        }
        int loopOrder = 0;
        RefreshLevel(
            roots,
            depth: 0,
            ref loopOrder);
    }

    public static MacroPlanLoopBlockNode AddLoopBlock(
        ObservableCollection<MacroPlanBlockNode> roots)
    {
        MacroPlanLoopBlockNode loop = new();
        roots.Insert(
            IndexBeforeForever(roots),
            loop);
        RefreshPresentation(roots);
        return loop;
    }

    public static void SynchronizeTasks(
        ObservableCollection<MacroPlanBlockNode> roots,
        IReadOnlyList<MacroTaskRow> rows)
    {
        Dictionary<string, MacroPlanTaskBlockNode>
            existing = FlattenTasks(roots)
                .ToDictionary(
                    node => node.TaskRow.Definition.Id,
                    StringComparer.OrdinalIgnoreCase);
        HashSet<string> currentIds =
            rows.Select(row => row.Definition.Id)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
        RemoveMissing(roots, currentIds);
        foreach (MacroTaskRow row in rows)
        {
            if (existing.TryGetValue(
                    row.Definition.Id,
                    out MacroPlanTaskBlockNode? node))
            {
                node.TaskRow = row;
                continue;
            }
            int insert = IndexBeforeForever(roots);
            roots.Insert(
                insert,
                new MacroPlanTaskBlockNode(row));
        }
        RefreshPresentation(roots);
    }

    public static ObservableCollection<MacroPlanBlockNode>
        Build(
        IReadOnlyList<MacroTaskRow> rows,
        IReadOnlyList<MacroPlanLoopDefinition> loops,
        IReadOnlyList<MacroPlanLoopProgress> progress)
    {
        MacroTaskDefinition[] tasks =
            rows.Select(row => row.Definition)
                .ToArray();
        MacroPlanLoopDefinition.ValidateAll(
            loops,
            tasks);
        IReadOnlyList<LoopDescriptor> roots =
            BuildDescriptors(loops, tasks);
        ObservableCollection<MacroPlanBlockNode>
            result = BuildRange(
                rows,
                start: 0,
                stop: rows.Count - 1,
                roots,
                progress);
        RefreshPresentation(result);
        return result;
    }

    public static int SubtreeLoopHeight(
        MacroPlanBlockNode node) =>
        node is not MacroPlanLoopBlockNode loop
            ? 0
            : 1 +
              (loop.Children.Count == 0
                  ? 0
                  : loop.Children.Max(
                      SubtreeLoopHeight));

    public static bool Contains(
        MacroPlanLoopBlockNode container,
        MacroPlanBlockNode candidate) =>
        container.Children.Any(child =>
            ReferenceEquals(child, candidate) ||
            child is MacroPlanLoopBlockNode nested &&
            Contains(nested, candidate));

    public static int IndexBeforeForever(
        IReadOnlyList<MacroPlanBlockNode> nodes)
    {
        for (int index = 0;
             index < nodes.Count;
             index++)
        {
            if (nodes[index] is
                MacroPlanLoopBlockNode
                {
                    Forever: true,
                })
            {
                return index;
            }
        }
        return nodes.Count;
    }

    private static MacroPlanLoopDefinition
        ReadDefinition(
        MacroPlanLoopBlockNode loop)
    {
        IReadOnlyList<MacroPlanTaskBlockNode> tasks =
            FlattenTasks(loop.Children);
        if (tasks.Count == 0)
        {
            throw new InvalidDataException(
                "Every loop block must contain at least one task.");
        }
        return new MacroPlanLoopDefinition
        {
            StartTaskId =
                tasks[0].TaskRow.Definition.Id,
            StopTaskId =
                tasks[^1].TaskRow.Definition.Id,
            TotalRuns = loop.ReadTotalRuns(),
            Forever = loop.Forever,
        };
    }

    private static void RefreshLevel(
        IEnumerable<MacroPlanBlockNode> nodes,
        int depth,
        ref int loopOrder)
    {
        foreach (MacroPlanBlockNode node in nodes)
        {
            node.Depth = depth;
            if (node is MacroPlanTaskBlockNode)
            {
                continue;
            }
            MacroPlanLoopBlockNode loop =
                (MacroPlanLoopBlockNode)node;
            loopOrder++;
            IReadOnlyList<MacroPlanTaskBlockNode>
                members = FlattenTasks(loop.Children);
            int nestedLoopCount =
                FlattenLoops(loop.Children).Count;
            loop.BlockLabel = $"Loop {loopOrder}";
            loop.MembershipText =
                members.Count == 0
                    ? "No blocks yet"
                    : $"{members.Count} " +
                      $"task{Plural(members.Count)}, " +
                      $"{nestedLoopCount} nested " +
                      $"loop{Plural(nestedLoopCount)}";
            long completed =
                loop.Progress.CompletedRuns;
            string completedText =
                $"{completed} run{Plural(completed)} " +
                "completed.";
            loop.StatusText = loop.Forever
                ? $"Forever · {completedText}"
                : $"Repeat {loop.AmountText} times · " +
                  completedText;
            RefreshLevel(
                loop.Children,
                depth + 1,
                ref loopOrder);
        }
    }

    private static string Plural(long count) =>
        count == 1
            ? string.Empty
            : "s";

    private static void RemoveMissing(
        ObservableCollection<MacroPlanBlockNode> nodes,
        IReadOnlySet<string> currentIds)
    {
        for (int index = nodes.Count - 1;
             index >= 0;
             index--)
        {
            MacroPlanBlockNode node = nodes[index];
            if (node is MacroPlanTaskBlockNode task)
            {
                if (!currentIds.Contains(
                        task.TaskRow.Definition.Id))
                {
                    nodes.RemoveAt(index);
                }
                continue;
            }
            RemoveMissing(
                ((MacroPlanLoopBlockNode)node)
                    .Children,
                currentIds);
        }
    }

    private static IReadOnlyList<LoopDescriptor>
        BuildDescriptors(
        IReadOnlyList<MacroPlanLoopDefinition> loops,
        IReadOnlyList<MacroTaskDefinition> tasks)
    {
        LoopDescriptor[] ordered = loops
            .Select(loop =>
            {
                (int start, int stop) =
                    loop.ResolveRange(tasks);
                return new LoopDescriptor(
                    loop,
                    start,
                    stop);
            })
            .OrderBy(value => value.Start)
            .ThenByDescending(value => value.Stop)
            .ThenByDescending(
                value => value.Definition.Forever)
            .ToArray();
        List<LoopDescriptor> roots = [];
        List<LoopDescriptor> stack = [];
        foreach (LoopDescriptor value in ordered)
        {
            while (stack.Count != 0 &&
                   !stack[^1].Contains(value))
            {
                stack.RemoveAt(stack.Count - 1);
            }
            (stack.Count == 0
                ? roots
                : stack[^1].Children)
                .Add(value);
            stack.Add(value);
        }
        return roots;
    }

    private static ObservableCollection<MacroPlanBlockNode>
        BuildRange(
        IReadOnlyList<MacroTaskRow> rows,
        int start,
        int stop,
        IReadOnlyList<LoopDescriptor> loops,
        IReadOnlyList<MacroPlanLoopProgress> progress)
    {
        ObservableCollection<MacroPlanBlockNode>
            result = [];
        int cursor = start;
        foreach (LoopDescriptor descriptor in loops)
        {
            AddTasks(result, rows, cursor,
                descriptor.Start - 1);
            MacroPlanLoopBlockNode loop = new()
            {
                AmountText =
                    descriptor.Definition.TotalRuns
                        .ToString(
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture),
                Forever =
                    descriptor.Definition.Forever,
                Progress =
                    progress.FirstOrDefault(state =>
                        string.Equals(
                            state.ConfigurationSignature,
                            descriptor.Definition
                                .ConfigurationSignature,
                            StringComparison.Ordinal)) ??
                    new(),
            };
            foreach (MacroPlanBlockNode child in
                     BuildRange(
                         rows,
                         descriptor.Start,
                         descriptor.Stop,
                         descriptor.Children,
                         progress))
            {
                loop.Children.Add(child);
            }
            result.Add(loop);
            cursor = descriptor.Stop + 1;
        }
        AddTasks(result, rows, cursor, stop);
        return result;
    }

    private static void AddTasks(
        ICollection<MacroPlanBlockNode> result,
        IReadOnlyList<MacroTaskRow> rows,
        int start,
        int stop)
    {
        for (int index = start;
             index <= stop;
             index++)
        {
            result.Add(
                new MacroPlanTaskBlockNode(
                    rows[index]));
        }
    }

    private sealed class LoopDescriptor(
        MacroPlanLoopDefinition definition,
        int start,
        int stop)
    {
        public MacroPlanLoopDefinition Definition { get; } =
            definition;
        public int Start { get; } = start;
        public int Stop { get; } = stop;
        public List<LoopDescriptor> Children { get; } = [];

        public bool Contains(LoopDescriptor other) =>
            Start <= other.Start &&
            Stop >= other.Stop;
    }
}
