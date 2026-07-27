using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Scheduling;

internal sealed record MacroPlanLoopEvaluation(
    MacroPlan Plan,
    bool Changed,
    IReadOnlyList<string> Messages);

internal static class MacroPlanLoopPolicy
{
    public static MacroPlan Normalize(
        MacroPlan source)
    {
        IReadOnlyList<MacroPlanLoopDefinition> loops =
            source.EffectiveLoops();
        IReadOnlyList<MacroPlanLoopProgress> savedStates =
            source.EffectiveLoopStates();
        IReadOnlyList<MacroTaskProgress> normalized =
            source.Tasks
                .Select(task => source.ProgressFor(task.Id))
                .ToArray();
        if (loops.Count == 0)
        {
            MacroTaskProgress[] withoutBaselines =
                normalized.Select(ClearBaseline).ToArray();
            if (source.Progress.SequenceEqual(
                    withoutBaselines) &&
                source.Loops.Count == 0 &&
                source.Loop is null &&
                source.LoopStates.Count == 0 &&
                source.LoopProgress.IsEmpty)
            {
                return source;
            }
            return source with
            {
                Progress = withoutBaselines,
                Loops = [],
                LoopStates = [],
                Loop = null,
                LoopProgress = new(),
            };
        }

        HashSet<string> savedSignatures =
            savedStates
                .Select(state =>
                    state.ConfigurationSignature)
                .ToHashSet(StringComparer.Ordinal);
        MacroPlanLoopProgress[] states =
            loops.Select(loop =>
            {
                MacroPlanLoopProgress? saved =
                    savedStates.FirstOrDefault(state =>
                        string.Equals(
                            state.ConfigurationSignature,
                            loop.ConfigurationSignature,
                            StringComparison.Ordinal));
                MacroPlanLoopProgress state =
                    saved ?? InitialState(loop);
                MacroPlanLoopPhase phase =
                    state.Phase ==
                        MacroPlanLoopPhase.AfterLoop ||
                    !loop.Forever &&
                    state.CompletedRuns >= loop.TotalRuns
                        ? MacroPlanLoopPhase.AfterLoop
                        : MacroPlanLoopPhase.Loop;
                return state with
                {
                    ConfigurationSignature =
                        loop.ConfigurationSignature,
                    Phase = phase,
                };
            }).ToArray();

        HashSet<int> covered = [];
        HashSet<int> newlyConfigured = [];
        foreach (MacroPlanLoopDefinition loop in loops)
        {
            (int start, int stop) =
                loop.ResolveRange(source.Tasks);
            for (int index = start;
                 index <= stop;
                 index++)
            {
                covered.Add(index);
                if (!savedSignatures.Contains(
                        loop.ConfigurationSignature))
                {
                    newlyConfigured.Add(index);
                }
            }
        }
        MacroTaskProgress[] progress =
            source.Tasks.Select((task, index) =>
            {
                MacroTaskProgress value =
                    normalized[index];
                if (!covered.Contains(index))
                {
                    return ClearBaseline(value);
                }
                return newlyConfigured.Contains(index) &&
                    !task.IsRecurring
                        ? BeginNextRun(value)
                        : value;
            }).ToArray();

        bool alreadyCurrent =
            source.Loop is null &&
            source.LoopProgress.IsEmpty &&
            source.Loops.SequenceEqual(loops) &&
            source.LoopStates.SequenceEqual(states) &&
            source.Progress.SequenceEqual(progress);
        return alreadyCurrent
            ? source
            : source with
            {
                Progress = progress,
                Loops = loops.ToArray(),
                LoopStates = states,
                Loop = null,
                LoopProgress = new(),
            };
    }

    public static MacroPlanLoopEvaluation Prepare(
        MacroPlan source,
        DateTimeOffset now)
    {
        MacroPlan plan = Normalize(source);
        bool changed = !ReferenceEquals(plan, source);
        List<string> messages = [];
        while (plan.EffectiveLoops().Count != 0)
        {
            LoopSearchResult result =
                FindNext(plan, now);
            if (result.Tasks.Count != 0 ||
                result.LoopToAdvance is null)
            {
                break;
            }
            plan = AdvanceLoop(
                plan,
                result.LoopToAdvance,
                now,
                messages);
            changed = true;
        }
        return new MacroPlanLoopEvaluation(
            plan,
            changed,
            messages);
    }

    public static IReadOnlyList<MacroTaskDefinition>
        ActiveTasks(
        MacroPlan plan,
        DateTimeOffset now)
    {
        if (plan.EffectiveLoops().Count == 0)
        {
            return plan.Tasks;
        }
        return FindNext(plan, now).Tasks;
    }

    private static LoopSearchResult FindNext(
        MacroPlan plan,
        DateTimeOffset now) =>
        SearchContainer(
            plan,
            0,
            plan.Tasks.Count - 1,
            MacroPlanLoopTree.Build(plan),
            now);

    private static LoopSearchResult SearchContainer(
        MacroPlan plan,
        int start,
        int stop,
        IReadOnlyList<MacroPlanLoopNode> children,
        DateTimeOffset now)
    {
        int cursor = start;
        foreach (MacroPlanLoopNode child in children)
        {
            IReadOnlyList<MacroTaskDefinition> before =
                Slice(plan.Tasks, cursor, child.Start - 1);
            if (!ScopeComplete(plan, before, now))
            {
                return new LoopSearchResult(
                    before,
                    null);
            }

            MacroPlanLoopProgress state =
                plan.LoopStateFor(child.Definition);
            if (state.Phase !=
                MacroPlanLoopPhase.AfterLoop)
            {
                LoopSearchResult nested =
                    SearchContainer(
                        plan,
                        child.Start,
                        child.Stop,
                        child.Children,
                        now);
                if (nested.Tasks.Count != 0 ||
                    nested.LoopToAdvance is not null)
                {
                    return nested;
                }
                return new LoopSearchResult(
                    [],
                    child);
            }
            cursor = child.Stop + 1;
        }

        IReadOnlyList<MacroTaskDefinition> after =
            Slice(plan.Tasks, cursor, stop);
        return ScopeComplete(plan, after, now)
            ? new LoopSearchResult([], null)
            : new LoopSearchResult(after, null);
    }

    private static MacroPlan AdvanceLoop(
        MacroPlan plan,
        MacroPlanLoopNode node,
        DateTimeOffset now,
        ICollection<string> messages)
    {
        MacroPlanLoopDefinition loop =
            node.Definition;
        MacroPlanLoopProgress state =
            plan.LoopStateFor(loop);
        long completedRuns = state.CompletedRuns + 1;
        bool finished =
            !loop.Forever &&
            completedRuns >= loop.TotalRuns;
        MacroPlanLoopProgress nextState =
            state with
            {
                CompletedRuns = completedRuns,
                Phase = finished
                    ? MacroPlanLoopPhase.AfterLoop
                    : MacroPlanLoopPhase.Loop,
            };
        IReadOnlyList<MacroPlanLoopProgress> states =
            ReplaceState(
                plan.LoopStates,
                nextState);
        IReadOnlyList<MacroTaskProgress> progress =
            plan.Progress;
        if (!finished)
        {
            HashSet<string> descendantSignatures =
                node.Descendants()
                    .Select(child =>
                        child.Definition
                            .ConfigurationSignature)
                    .ToHashSet(StringComparer.Ordinal);
            states = states
                .Select(value =>
                    descendantSignatures.Contains(
                        value.ConfigurationSignature)
                        ? InitialState(
                            plan.EffectiveLoops()
                                .Single(loopValue =>
                                    string.Equals(
                                        loopValue
                                            .ConfigurationSignature,
                                        value.ConfigurationSignature,
                                        StringComparison.Ordinal)))
                        : value)
                .ToArray();
            progress = plan.Tasks
                .Select((task, index) =>
                    index >= node.Start &&
                    index <= node.Stop &&
                    !task.IsRecurring
                        ? BeginNextRun(
                            plan.ProgressFor(task.Id))
                        : plan.ProgressFor(task.Id))
                .ToArray();
        }

        string range =
            $"tasks {node.Start + 1}-{node.Stop + 1}";
        messages.Add(finished
            ? $"Loop {range} finished after {completedRuns} run{Plural(completedRuns)}."
            : loop.Forever
                ? $"Forever loop {range} completed run {completedRuns}; starting run {completedRuns + 1}."
                : $"Loop {range} completed run {completedRuns} of {loop.TotalRuns}; starting run {completedRuns + 1}.");
        return plan with
        {
            Progress = progress,
            LoopStates = states,
            UpdatedAt = now,
        };
    }

    private static IReadOnlyList<MacroPlanLoopProgress>
        ReplaceState(
        IReadOnlyList<MacroPlanLoopProgress> states,
        MacroPlanLoopProgress replacement) =>
        states
            .Select(state =>
                string.Equals(
                    state.ConfigurationSignature,
                    replacement.ConfigurationSignature,
                    StringComparison.Ordinal)
                    ? replacement
                    : state)
            .ToArray();

    private static IReadOnlyList<MacroTaskDefinition> Slice(
        IReadOnlyList<MacroTaskDefinition> tasks,
        int start,
        int stop) =>
        stop < start
            ? []
            : tasks
                .Skip(start)
                .Take(stop - start + 1)
                .ToArray();

    private static bool ScopeComplete(
        MacroPlan plan,
        IReadOnlyList<MacroTaskDefinition> tasks,
        DateTimeOffset now) =>
        tasks
            .Where(task => task.Enabled)
            .All(task =>
            {
                MacroTaskProgress progress =
                    plan.ProgressFor(task.Id);
                return task.IsRecurring
                    ? progress.NextEligibleAtUtc > now
                    : progress.Completed;
            });

    private static MacroPlanLoopProgress InitialState(
        MacroPlanLoopDefinition loop) =>
        new()
        {
            ConfigurationSignature =
                loop.ConfigurationSignature,
            Phase = MacroPlanLoopPhase.Loop,
        };

    private static MacroTaskProgress BeginNextRun(
        MacroTaskProgress progress) =>
        progress with
        {
            Completed = false,
            NextEligibleAtUtc = null,
            TargetVictoryBaseline =
                progress.Victories,
            TargetRuntimeBaselineSeconds =
                progress.RuntimeSeconds,
        };

    private static MacroTaskProgress ClearBaseline(
        MacroTaskProgress progress) =>
        progress.TargetVictoryBaseline == 0 &&
        progress.TargetRuntimeBaselineSeconds == 0
            ? progress
            : progress with
            {
                TargetVictoryBaseline = 0,
                TargetRuntimeBaselineSeconds = 0,
            };

    private static string Plural(long value) =>
        value == 1
            ? string.Empty
            : "s";

    private sealed record LoopSearchResult(
        IReadOnlyList<MacroTaskDefinition> Tasks,
        MacroPlanLoopNode? LoopToAdvance);
}
