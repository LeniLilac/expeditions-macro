using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Scheduling;

internal sealed record MacroPlanLoopEvaluation(
    MacroPlan Plan,
    bool Changed,
    IReadOnlyList<string> Messages);

internal static class MacroPlanLoopPolicy
{
    public static MacroPlan Normalize(
        MacroPlan plan)
    {
        IReadOnlyList<MacroTaskProgress> normalized =
            plan.Tasks
                .Select(task => plan.ProgressFor(task.Id))
                .ToArray();
        if (plan.Loop is null)
        {
            MacroTaskProgress[] withoutBaselines =
                normalized
                    .Select(ClearBaseline)
                    .ToArray();
            if (plan.Progress.SequenceEqual(
                    withoutBaselines) &&
                plan.LoopProgress.IsEmpty)
            {
                return plan;
            }
            return plan with
            {
                Progress = withoutBaselines,
                LoopProgress = new(),
            };
        }

        MacroPlanLoopDefinition loop = plan.Loop;
        (int start, int stop) =
            loop.ResolveRange(plan.Tasks);
        if (string.Equals(
                plan.LoopProgress.ConfigurationSignature,
                loop.ConfigurationSignature,
                StringComparison.Ordinal))
        {
            MacroPlanLoopProgress progress =
                plan.LoopProgress;
            if (!loop.Forever &&
                progress.CompletedRuns >= loop.TotalRuns &&
                progress.Phase !=
                    MacroPlanLoopPhase.AfterLoop)
            {
                progress = progress with
                {
                    Phase =
                        MacroPlanLoopPhase.AfterLoop,
                };
            }
            if (plan.Progress.SequenceEqual(
                    normalized) &&
                progress == plan.LoopProgress)
            {
                return plan;
            }
            return plan with
            {
                Progress = normalized,
                LoopProgress = progress,
            };
        }

        MacroTaskProgress[] reset =
            plan.Tasks
                .Select((task, index) =>
                {
                    MacroTaskProgress progress =
                        normalized[index];
                    if (index >= start &&
                        index <= stop &&
                        !task.IsRecurring)
                    {
                        return BeginNextRun(progress);
                    }
                    return ClearBaseline(progress);
                })
                .ToArray();
        return plan with
        {
            Progress = reset,
            LoopProgress = new MacroPlanLoopProgress
            {
                ConfigurationSignature =
                    loop.ConfigurationSignature,
                Phase = start == 0
                    ? MacroPlanLoopPhase.Loop
                    : MacroPlanLoopPhase.BeforeLoop,
            },
        };
    }

    public static MacroPlanLoopEvaluation Prepare(
        MacroPlan source,
        DateTimeOffset now)
    {
        MacroPlan plan = Normalize(source);
        bool changed = !ReferenceEquals(plan, source);
        List<string> messages = [];
        if (plan.Loop is null)
        {
            return new MacroPlanLoopEvaluation(
                plan,
                changed,
                messages);
        }

        while (true)
        {
            MacroPlanLoopPhase phase =
                plan.LoopProgress.Phase;
            IReadOnlyList<MacroTaskDefinition> scope =
                ActiveTasks(plan);
            if (!ScopeComplete(plan, scope, now))
            {
                return new MacroPlanLoopEvaluation(
                    plan,
                    changed,
                    messages);
            }

            if (phase == MacroPlanLoopPhase.BeforeLoop)
            {
                plan = plan with
                {
                    LoopProgress =
                        plan.LoopProgress with
                        {
                            Phase =
                                MacroPlanLoopPhase.Loop,
                        },
                    UpdatedAt = now,
                };
                changed = true;
                messages.Add(
                    "Tasks before the loop are complete; starting loop run 1.");
                continue;
            }
            if (phase == MacroPlanLoopPhase.AfterLoop)
            {
                return new MacroPlanLoopEvaluation(
                    plan,
                    changed,
                    messages);
            }

            long completedRuns =
                plan.LoopProgress.CompletedRuns + 1;
            if (!plan.Loop.Forever &&
                completedRuns >=
                    plan.Loop.TotalRuns)
            {
                plan = plan with
                {
                    LoopProgress =
                        plan.LoopProgress with
                        {
                            CompletedRuns =
                                completedRuns,
                            Phase =
                                MacroPlanLoopPhase
                                    .AfterLoop,
                        },
                    UpdatedAt = now,
                };
                changed = true;
                messages.Add(
                    $"Loop finished after {completedRuns} run{Plural(completedRuns)}.");
                continue;
            }

            (int start, int stop) =
                plan.Loop.ResolveRange(plan.Tasks);
            MacroTaskProgress[] nextProgress =
                plan.Tasks
                    .Select((task, index) =>
                        index >= start &&
                        index <= stop &&
                        !task.IsRecurring
                            ? BeginNextRun(
                                plan.ProgressFor(
                                    task.Id))
                            : plan.ProgressFor(
                                task.Id))
                    .ToArray();
            plan = plan with
            {
                Progress = nextProgress,
                LoopProgress =
                    plan.LoopProgress with
                    {
                        CompletedRuns =
                            completedRuns,
                    },
                UpdatedAt = now,
            };
            changed = true;
            messages.Add(
                plan.Loop.Forever
                    ? $"Loop run {completedRuns} complete; starting run {completedRuns + 1}."
                    : $"Loop run {completedRuns} of {plan.Loop.TotalRuns} complete; starting run {completedRuns + 1}.");
        }
    }

    public static IReadOnlyList<MacroTaskDefinition>
        ActiveTasks(
        MacroPlan plan)
    {
        if (plan.Loop is null)
        {
            return plan.Tasks;
        }
        (int start, int stop) =
            plan.Loop.ResolveRange(plan.Tasks);
        return plan.LoopProgress.Phase switch
        {
            MacroPlanLoopPhase.BeforeLoop =>
                plan.Tasks.Take(start).ToArray(),
            MacroPlanLoopPhase.Loop =>
                plan.Tasks
                    .Skip(start)
                    .Take(stop - start + 1)
                    .ToArray(),
            MacroPlanLoopPhase.AfterLoop =>
                plan.Tasks.Skip(stop + 1).ToArray(),
            _ => throw new InvalidDataException(
                "Macro loop phase is invalid."),
        };
    }

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
}
