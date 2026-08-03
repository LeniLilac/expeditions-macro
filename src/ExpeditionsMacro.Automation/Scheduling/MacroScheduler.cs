using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Scheduling;

public sealed record ScheduledTaskResult(
    int Victories,
    int Defeats,
    TimeSpan Runtime,
    DateTimeOffset? NextEligibleAtUtc = null,
    bool Skipped = false,
    bool SkipUntilSchedulerRestart = false,
    ChallengeRotationProgress? ChallengeRotation = null);

public sealed record ScheduledTaskCheckpoint(
    ResourceRefuelTarget? RefuelCompletedTargets = null);

public enum ScheduledTaskContinuation
{
    Handoff,
    RepeatStage,
    ReturnToLobby,
}

public sealed class MacroScheduler
{
    private readonly MacroPlanRepository _plans;

    public MacroScheduler(MacroPlanRepository plans) => _plans = plans;

    public Task RunAsync(
        MacroPlan initialPlan,
        Func<
            MacroTaskDefinition,
            Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>>,
            CancellationToken,
            Task<ScheduledTaskResult>> execute,
        IProgress<MacroProgress>? progress = null,
        Action<MacroPlan>? planChanged = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            initialPlan,
            (task, record, _, token) =>
                execute(task, record, token),
            progress,
            planChanged,
            log,
            cancellationToken);

    public async Task RunAsync(
        MacroPlan initialPlan,
        Func<
            MacroTaskDefinition,
            Func<ScheduledTaskResult, CancellationToken, Task<ScheduledTaskContinuation>>,
            Func<ScheduledTaskCheckpoint, Task>,
            CancellationToken,
            Task<ScheduledTaskResult>> execute,
        IProgress<MacroProgress>? progress = null,
        Action<MacroPlan>? planChanged = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default)
    {
        initialPlan.Validate();
        HashSet<string> sessionExcluded = new(
            StringComparer.OrdinalIgnoreCase);
        MacroPlan plan =
            MacroPlanLoopPolicy.Normalize(
                NormalizeProgress(initialPlan));
        await SaveAsync(plan, planChanged, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            plan = await PrepareLoopAsync(
                    plan,
                    now,
                    sessionExcluded,
                    progress,
                    planChanged,
                    log,
                    cancellationToken)
                .ConfigureAwait(false);
            MacroTaskDefinition? next =
                SelectNextPrepared(
                    plan,
                    now,
                    sessionExcluded);
            if (next is null)
            {
                DateTimeOffset? wake = NextWake(
                    plan,
                    now,
                    sessionExcluded);
                string message = wake is null
                    ? "Every finite task is complete. Waiting for the task list to change."
                    : $"No task is currently eligible. Waiting until {wake.Value.LocalDateTime:t}.";
                progress?.Report(new MacroProgress("Waiting", 0, message, "scheduler_waiting"));
                log?.Invoke(new MacroEvent(DateTimeOffset.Now, MacroEventLevel.Information, message, "scheduler_waiting"));
                TimeSpan delay = wake is null
                    ? TimeSpan.FromSeconds(10)
                    : TimeSpan.FromMilliseconds(Math.Clamp((wake.Value - now).TotalMilliseconds, 500, 10000));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            progress?.Report(new MacroProgress("Task", 0, $"Starting priority {next.Priority}: {DisplayName(next)}.", "scheduler_task"));
            log?.Invoke(new MacroEvent(DateTimeOffset.Now, MacroEventLevel.Information, $"Scheduler selected priority {next.Priority}: {DisplayName(next)}.", "scheduler_task"));
            MacroTaskDefinition activeTask = next;
            bool resultRecorded = false;

            async Task<ScheduledTaskContinuation> RecordResultAsync(
                ScheduledTaskResult result,
                CancellationToken recordCancellationToken)
            {
                resultRecorded = true;
                if (result.SkipUntilSchedulerRestart)
                {
                    sessionExcluded.Add(
                        activeTask.Id);
                }
                DateTimeOffset completedAt = DateTimeOffset.UtcNow;
                MacroTaskProgress before = plan.ProgressFor(activeTask.Id);
                MacroTaskProgress after = Advance(activeTask, before, result, completedAt);
                plan = plan with
                {
                    Progress = plan.Tasks.Select(task => string.Equals(task.Id, activeTask.Id, StringComparison.OrdinalIgnoreCase)
                        ? after
                        : plan.ProgressFor(task.Id)).ToArray(),
                    UpdatedAt = completedAt,
                    ChallengeRotation =
                        result.ChallengeRotation ??
                        plan.ChallengeRotation,
                };
                await SaveAsync(plan, planChanged, recordCancellationToken).ConfigureAwait(false);
                LogResult(activeTask, result, log);

                plan = await PrepareLoopAsync(
                        plan,
                        DateTimeOffset.UtcNow,
                        sessionExcluded,
                        progress,
                        planChanged,
                        log,
                        recordCancellationToken)
                    .ConfigureAwait(false);
                MacroTaskDefinition? following =
                    SelectNextPrepared(
                        plan,
                        DateTimeOffset.UtcNow,
                        sessionExcluded);
                if (!CanRepeatStage(activeTask, following, result))
                {
                    return following?.Kind is
                            MacroTaskKind.Event or
                            MacroTaskKind.Utility or
                            MacroTaskKind.Bounty
                        ? ScheduledTaskContinuation.ReturnToLobby
                        : ScheduledTaskContinuation.Handoff;
                }
                log?.Invoke(new MacroEvent(
                    DateTimeOffset.Now,
                    MacroEventLevel.Information,
                    $"Scheduler kept {DisplayName(following!)} on the same route; using Repeat Stage.",
                    "scheduler_repeat_stage"));
                activeTask = following!;
                return ScheduledTaskContinuation.RepeatStage;
            }

            async Task RecordCheckpointAsync(
                ScheduledTaskCheckpoint checkpoint)
            {
                DateTimeOffset recordedAt =
                    DateTimeOffset.UtcNow;
                MacroTaskProgress before =
                    plan.ProgressFor(activeTask.Id);
                MacroTaskProgress after = ApplyCheckpoint(
                    activeTask,
                    before,
                    checkpoint);
                if (after == before)
                {
                    return;
                }
                plan = plan with
                {
                    Progress = plan.Tasks.Select(task =>
                        string.Equals(
                            task.Id,
                            activeTask.Id,
                            StringComparison.OrdinalIgnoreCase)
                            ? after
                            : plan.ProgressFor(task.Id)).ToArray(),
                    UpdatedAt = recordedAt,
                };
                // A completed station is durable progress. Finish this small
                // atomic write even when Stop was requested immediately after
                // the station accepted its fuel.
                await SaveAsync(
                        plan,
                        planChanged,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            ScheduledTaskResult returned = await execute(
                    next,
                    RecordResultAsync,
                    RecordCheckpointAsync,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!resultRecorded)
            {
                await RecordResultAsync(returned, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<MacroPlan> ResetProgressAsync(MacroPlan plan, CancellationToken cancellationToken = default)
    {
        MacroPlan reset = plan.ResetProgress();
        await _plans.SaveAsync(reset, cancellationToken).ConfigureAwait(false);
        return reset;
    }

    public static MacroTaskDefinition? SelectNext(
        MacroPlan plan,
        DateTimeOffset now) =>
        SelectNextPrepared(
            MacroPlanLoopPolicy
                .Prepare(plan, now)
                .Plan,
            now);

    private static MacroTaskDefinition?
        SelectNextPrepared(
        MacroPlan plan,
        DateTimeOffset now,
        ISet<string>? excluded = null) =>
        MacroPlanLoopPolicy
            .ActiveTasks(
                plan,
                now,
                excluded)
            .Select((task, index) => new { Task = task, Index = index, Progress = plan.ProgressFor(task.Id) })
            .Where(value => value.Task.IsRecurring || !value.Progress.Completed)
            .Where(value =>
                excluded is null ||
                !excluded.Contains(
                    value.Task.Id))
            .Where(value => value.Progress.NextEligibleAtUtc is null || value.Progress.NextEligibleAtUtc <= now)
            .OrderBy(value => value.Task.Priority)
            .ThenBy(value => value.Index)
            .Select(value => value.Task)
            .FirstOrDefault();

    internal static MacroTaskProgress Advance(
        MacroTaskDefinition task,
        MacroTaskProgress before,
        ScheduledTaskResult result,
        DateTimeOffset completedAt)
    {
        int victories = before.Victories + Math.Max(0, result.Victories);
        int defeats = before.Defeats + Math.Max(0, result.Defeats);
        long runtime = before.RuntimeSeconds + Math.Max(0, (long)Math.Ceiling(result.Runtime.TotalSeconds));
        int targetVictories =
            victories -
            before.TargetVictoryBaseline;
        long targetRuntime =
            runtime -
            before.TargetRuntimeBaselineSeconds;
        bool completed = task.Kind switch
        {
            MacroTaskKind.Challenge or
                MacroTaskKind.Utility or
                MacroTaskKind.Bounty => false,
            MacroTaskKind.Story when task.CompleteOnRuntimeDefeat =>
                result.Defeats > 0 &&
                targetRuntime >=
                    task.TargetRuntimeMinutes * 60L,
            _ => targetVictories >= task.TargetVictories,
        };
        return before with
        {
            Victories = victories,
            Defeats = defeats,
            RuntimeSeconds = runtime,
            Completed = completed,
            LastAttemptAt = completedAt,
            LastCompletedAt = completed ? completedAt : before.LastCompletedAt,
            // Any task can become temporarily ineligible. Challenges use this for
            // their global reset, while finite modes use it after a safe skip so a
            // higher-priority camera/model problem cannot spin forever and starve
            // the rest of the plan.
            NextEligibleAtUtc = completed ? null : result.NextEligibleAtUtc,
            RefuelCompletedTargets =
                task.Kind == MacroTaskKind.Utility &&
                !result.Skipped
                    ? ResourceRefuelTarget.None
                    : before.RefuelCompletedTargets,
        };
    }

    internal static MacroTaskProgress ApplyCheckpoint(
        MacroTaskDefinition task,
        MacroTaskProgress before,
        ScheduledTaskCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.RefuelCompletedTargets is not
                ResourceRefuelTarget completed)
        {
            return before;
        }
        if (task.Kind != MacroTaskKind.Utility ||
            completed == ResourceRefuelTarget.None ||
            (completed & ~task.RefuelTarget) != 0 ||
            completed == task.RefuelTarget)
        {
            throw new InvalidDataException(
                "A refuel checkpoint must describe an incomplete Utility cycle.");
        }
        return before with
        {
            RefuelCompletedTargets = completed,
        };
    }

    internal static bool CanRepeatStage(
        MacroTaskDefinition current,
        MacroTaskDefinition? following,
        ScheduledTaskResult result) =>
        !result.Skipped &&
        following is not null &&
        current.Kind is MacroTaskKind.Expedition or
            MacroTaskKind.Story or
            MacroTaskKind.Raid or
            MacroTaskKind.Event &&
        following.Kind == current.Kind &&
        SameRoute(current, following);

    private static bool SameRoute(
        MacroTaskDefinition current,
        MacroTaskDefinition following)
    {
        if (!current.UsesPlacementSetup ||
            !following.UsesPlacementSetup)
        {
            return string.Equals(
                following.PresetId,
                current.PresetId,
                StringComparison.OrdinalIgnoreCase);
        }

        return current.PlacementTarget is not null &&
            following.PlacementTarget is not null &&
            current.PlacementTarget.Matches(
                following.PlacementTarget) &&
            current.Difficulty == following.Difficulty &&
            current.HardMode == following.HardMode;
    }

    private static MacroPlan NormalizeProgress(MacroPlan plan) => plan with
    {
        Progress = plan.Tasks.Select(task => plan.ProgressFor(task.Id)).ToArray(),
    };

    private static DateTimeOffset? NextWake(
        MacroPlan plan,
        DateTimeOffset now,
        ISet<string> excluded) =>
        MacroPlanLoopPolicy
            .ActiveTasks(
                plan,
                now,
                excluded)
            .Where(task =>
                task.IsRecurring ||
                !plan.ProgressFor(task.Id).Completed)
            .Where(task =>
                !excluded.Contains(task.Id))
            .Select(task => plan.ProgressFor(task.Id).NextEligibleAtUtc)
            .Where(value => value > now)
            .OrderBy(value => value)
            .FirstOrDefault();

    private async Task<MacroPlan> PrepareLoopAsync(
        MacroPlan plan,
        DateTimeOffset now,
        ISet<string> excluded,
        IProgress<MacroProgress>? progress,
        Action<MacroPlan>? planChanged,
        Action<MacroEvent>? log,
        CancellationToken cancellationToken)
    {
        MacroPlanLoopEvaluation evaluation =
            MacroPlanLoopPolicy.Prepare(
                plan,
                now,
                excluded);
        if (!evaluation.Changed)
        {
            return plan;
        }
        await SaveAsync(
                evaluation.Plan,
                planChanged,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (string message in evaluation.Messages)
        {
            progress?.Report(new MacroProgress(
                "Loop",
                0,
                message,
                "scheduler_loop"));
            log?.Invoke(new MacroEvent(
                DateTimeOffset.Now,
                MacroEventLevel.Information,
                message,
                "scheduler_loop"));
        }
        return evaluation.Plan;
    }

    private async Task SaveAsync(MacroPlan plan, Action<MacroPlan>? changed, CancellationToken cancellationToken)
    {
        await _plans.SaveAsync(plan, cancellationToken).ConfigureAwait(false);
        changed?.Invoke(plan);
    }

    private static void LogResult(
        MacroTaskDefinition task,
        ScheduledTaskResult result,
        Action<MacroEvent>? log)
    {
        string outcome = result.Skipped
            ? "skipped"
            : result.Victories > 0
                ? $"recorded {result.Victories} victory"
                : result.Defeats > 0
                    ? $"recorded {result.Defeats} defeat"
                    : "updated eligibility";
        log?.Invoke(new MacroEvent(DateTimeOffset.Now, MacroEventLevel.Information, $"{DisplayName(task)} {outcome}.", "scheduler_progress"));
    }

    private static string DisplayName(MacroTaskDefinition task) =>
        string.IsNullOrWhiteSpace(task.Name) ? $"{task.Kind} preset {task.PresetId}" : task.Name;
}
