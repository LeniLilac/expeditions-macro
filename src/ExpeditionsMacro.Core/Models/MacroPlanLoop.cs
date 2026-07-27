namespace ExpeditionsMacro.Core.Models;

public sealed record MacroPlanLoopDefinition
{
    public const int MaximumNestingDepth = 3;

    public required string StartTaskId { get; init; }
    public required string StopTaskId { get; init; }
    public int TotalRuns { get; init; } = 2;
    public bool Forever { get; init; }

    public string ConfigurationSignature =>
        $"{StartTaskId}|{StopTaskId}|{TotalRuns}|{Forever}";

    public void Validate(
        IReadOnlyList<MacroTaskDefinition> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        int start = IndexOf(tasks, StartTaskId);
        int stop = IndexOf(tasks, StopTaskId);
        if (start < 0 || stop < 0)
        {
            throw new InvalidDataException(
                "Loop start and stop must refer to tasks in this plan.");
        }
        if (start > stop)
        {
            throw new InvalidDataException(
                "Loop start must be above loop stop in the priority queue.");
        }
        if (TotalRuns is < 1 or > 100000)
        {
            throw new InvalidDataException(
                "Loop amount must be 1 through 100000.");
        }
        if (!tasks
                .Skip(start)
                .Take(stop - start + 1)
                .Any(task => !task.IsRecurring))
        {
            throw new InvalidDataException(
                "A loop must contain at least one finite task.");
        }
    }

    public static void ValidateAll(
        IReadOnlyList<MacroPlanLoopDefinition> loops,
        IReadOnlyList<MacroTaskDefinition> tasks)
    {
        ArgumentNullException.ThrowIfNull(loops);
        ArgumentNullException.ThrowIfNull(tasks);
        foreach (MacroPlanLoopDefinition loop in loops)
        {
            loop.Validate(tasks);
        }

        MacroPlanLoopDefinition[] forever =
            loops.Where(loop => loop.Forever).ToArray();
        if (forever.Length > 1)
        {
            throw new InvalidDataException(
                "A plan may contain only one Forever loop.");
        }
        if (forever.Length == 1 &&
            forever[0].ResolveRange(tasks).Stop !=
                tasks.Count - 1)
        {
            throw new InvalidDataException(
                "A Forever loop must end at the final task.");
        }

        LoopRange[] ranges = loops
            .Select(loop =>
            {
                (int start, int stop) =
                    loop.ResolveRange(tasks);
                return new LoopRange(
                    loop,
                    start,
                    stop);
            })
            .ToArray();
        for (int leftIndex = 0;
             leftIndex < ranges.Length;
             leftIndex++)
        {
            for (int rightIndex = leftIndex + 1;
                 rightIndex < ranges.Length;
                 rightIndex++)
            {
                ValidatePair(
                    ranges[leftIndex],
                    ranges[rightIndex]);
            }
        }
        ValidateDepth(ranges);
    }

    public (int Start, int Stop) ResolveRange(
        IReadOnlyList<MacroTaskDefinition> tasks)
    {
        int start = IndexOf(tasks, StartTaskId);
        int stop = IndexOf(tasks, StopTaskId);
        if (start < 0 || stop < start)
        {
            throw new InvalidDataException(
                "The saved loop range is invalid.");
        }
        return (start, stop);
    }

    private static int IndexOf(
        IReadOnlyList<MacroTaskDefinition> tasks,
        string id)
    {
        for (int index = 0; index < tasks.Count; index++)
        {
            if (string.Equals(
                    tasks[index].Id,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static void ValidatePair(
        LoopRange left,
        LoopRange right)
    {
        bool equal =
            left.Start == right.Start &&
            left.Stop == right.Stop;
        if (equal)
        {
            if (!left.Definition.Forever &&
                !right.Definition.Forever)
            {
                throw new InvalidDataException(
                    "Two finite loops cannot use the same task range.");
            }
            return;
        }

        bool disjoint =
            left.Stop < right.Start ||
            right.Stop < left.Start;
        bool leftContainsRight =
            left.Start <= right.Start &&
            left.Stop >= right.Stop;
        bool rightContainsLeft =
            right.Start <= left.Start &&
            right.Stop >= left.Stop;
        if (!disjoint &&
            !leftContainsRight &&
            !rightContainsLeft)
        {
            throw new InvalidDataException(
                "Loop ranges may be separate or nested, but cannot cross.");
        }

        LoopRange? forever =
            left.Definition.Forever
                ? left
                : right.Definition.Forever
                    ? right
                    : null;
        if (forever is null)
        {
            return;
        }
        LoopRange finite =
            ReferenceEquals(
                forever.Definition,
                left.Definition)
                ? right
                : left;
        if (finite.Start <= forever.Start &&
            finite.Stop >= forever.Stop)
        {
            throw new InvalidDataException(
                "A finite loop cannot contain the Forever loop.");
        }
    }

    private static void ValidateDepth(
        IReadOnlyList<LoopRange> ranges)
    {
        LoopRange[] ordered = ranges
            .OrderBy(range => range.Start)
            .ThenByDescending(range => range.Stop)
            .ThenByDescending(
                range => range.Definition.Forever)
            .ToArray();
        List<LoopRange> ancestors = [];
        foreach (LoopRange range in ordered)
        {
            while (ancestors.Count != 0 &&
                   !ContainsForDepth(
                       ancestors[^1],
                       range))
            {
                ancestors.RemoveAt(
                    ancestors.Count - 1);
            }
            if (ancestors.Count >=
                MaximumNestingDepth)
            {
                throw new InvalidDataException(
                    "Loop nesting is limited to three levels.");
            }
            ancestors.Add(range);
        }
    }

    private static bool ContainsForDepth(
        LoopRange parent,
        LoopRange child)
    {
        bool equal =
            parent.Start == child.Start &&
            parent.Stop == child.Stop;
        return equal
            ? parent.Definition.Forever &&
              !child.Definition.Forever
            : parent.Start <= child.Start &&
              parent.Stop >= child.Stop;
    }

    private sealed record LoopRange(
        MacroPlanLoopDefinition Definition,
        int Start,
        int Stop);
}

public enum MacroPlanLoopPhase
{
    BeforeLoop,
    Loop,
    AfterLoop,
}

public sealed record MacroPlanLoopProgress
{
    public string ConfigurationSignature { get; init; } =
        string.Empty;

    public MacroPlanLoopPhase Phase { get; init; } =
        MacroPlanLoopPhase.BeforeLoop;

    public long CompletedRuns { get; init; }

    public bool IsEmpty =>
        ConfigurationSignature.Length == 0 &&
        Phase == MacroPlanLoopPhase.BeforeLoop &&
        CompletedRuns == 0;

    public void Validate()
    {
        if (!Enum.IsDefined(Phase))
        {
            throw new InvalidDataException(
                "Macro loop phase is invalid.");
        }
        if (CompletedRuns < 0)
        {
            throw new InvalidDataException(
                "Completed loop runs cannot be negative.");
        }
    }
}
