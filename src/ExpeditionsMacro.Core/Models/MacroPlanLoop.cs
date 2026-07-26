namespace ExpeditionsMacro.Core.Models;

public sealed record MacroPlanLoopDefinition
{
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
                .Any(task =>
                    task.Enabled &&
                    !task.IsRecurring))
        {
            throw new InvalidDataException(
                "A loop must contain at least one enabled finite task.");
        }
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
