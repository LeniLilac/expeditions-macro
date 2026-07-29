using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class PlacementStepModePlayback
{
    private readonly PlacementBatchPlayback _batch;
    private readonly PlacementUnitActionPlayback _unitActions;
    private readonly PlacementStepModeKeyResolver _keyResolver;

    public PlacementStepModePlayback(
        IRobloxAutomation automation,
        Func<char> targetingKey,
        Func<char> autoUpgradeKey,
        Func<int> quickPlacementKey,
        Func<char> upgradeKey)
    {
        _batch = new PlacementBatchPlayback(automation);
        _unitActions =
            new PlacementUnitActionPlayback(automation);
        _keyResolver =
            new PlacementStepModeKeyResolver(
                targetingKey,
                autoUpgradeKey,
                quickPlacementKey,
                upgradeKey);
    }

    public void BeginMatch() =>
        _unitActions.BeginMatch();

    public async Task PlayAsync(
        RobloxWindow window,
        PlacementModel model,
        IReadOnlyList<PlacementStep> steps,
        bool useDefaultInterval,
        int defaultIntervalMilliseconds,
        int keyHoldMilliseconds,
        int afterKeyMilliseconds,
        char cancelPlacementKey,
        Action<int, int, PlacementStep>? stepSent,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        ValidateTiming(
            defaultIntervalMilliseconds,
            afterKeyMilliseconds);
        if (steps.Count == 0)
        {
            return;
        }

        MatchStepPlaybackItem[] playable =
            CollectPlayableSteps(
                model,
                steps,
                status);
        if (playable.Length == 0)
        {
            return;
        }

        PlacementStepModeKeys keys =
            _keyResolver.Resolve(
                playable.Select(item => item.Step)
                    .ToArray(),
                cancelPlacementKey);
        int next = 0;
        while (next < playable.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (playable[next].Step.Kind ==
                MatchStepKind.Placement)
            {
                MatchStepPlaybackItem[] group =
                    playable.Skip(next)
                        .TakeWhile(item =>
                            item.Step.Kind ==
                            MatchStepKind.Placement)
                        .ToArray();
                await PlayPlacementGroupAsync(
                        window,
                        model,
                        steps.Count,
                        group,
                        keys,
                        useDefaultInterval,
                        defaultIntervalMilliseconds,
                        keyHoldMilliseconds,
                        afterKeyMilliseconds,
                        stepSent,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
                next += group.Length;
                continue;
            }

            await PlayActionStepAsync(
                    window,
                    model,
                    steps.Count,
                    playable[next],
                    keys,
                    useDefaultInterval,
                    defaultIntervalMilliseconds,
                    stepSent,
                    status,
                    cancellationToken)
                .ConfigureAwait(false);
            next++;
        }
    }

    private async Task PlayPlacementGroupAsync(
        RobloxWindow window,
        PlacementModel model,
        int stepCount,
        IReadOnlyList<MatchStepPlaybackItem> group,
        PlacementStepModeKeys keys,
        bool useDefaultInterval,
        int defaultIntervalMilliseconds,
        int keyHoldMilliseconds,
        int afterKeyMilliseconds,
        Action<int, int, PlacementStep>? stepSent,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        await _batch.PlaceAsync(
                window,
                model,
                group,
                keys,
                keyHoldMilliseconds,
                afterKeyMilliseconds,
                attempt: 1,
                model.PlacementAttempts,
                status,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (MatchStepPlaybackItem playable in group)
        {
            bool selected =
                await _unitActions.TrySelectAsync(
                        window,
                        model,
                        playable,
                        stepCount,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
            for (int attempt = 2;
                 !selected &&
                 attempt <= model.PlacementAttempts;
                 attempt++)
            {
                await _batch.PlaceAsync(
                        window,
                        model,
                        [playable],
                        keys,
                        keyHoldMilliseconds,
                        afterKeyMilliseconds,
                        attempt,
                        model.PlacementAttempts,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
                selected =
                    await _unitActions.TrySelectAsync(
                            window,
                            model,
                            playable,
                            stepCount,
                            status,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            if (!selected)
            {
                status?.Invoke(
                    $"Step {playable.SourceIndex + 1}/{stepCount}: skipped Unit {playable.UnitKey} at ({playable.X}, {playable.Y}) after {model.PlacementAttempts} placement attempt(s) because selected-unit proof never appeared.");
                continue;
            }

            await _unitActions.ApplyPlacementAsync(
                    window,
                    model,
                    playable,
                    stepCount,
                    keys,
                    status,
                    cancellationToken)
                .ConfigureAwait(false);
            stepSent?.Invoke(
                playable.SourceIndex + 1,
                stepCount,
                playable.Step);
            await WaitDefaultIntervalAsync(
                    playable,
                    useDefaultInterval,
                    defaultIntervalMilliseconds,
                    status,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task PlayActionStepAsync(
        RobloxWindow window,
        PlacementModel model,
        int stepCount,
        MatchStepPlaybackItem playable,
        PlacementStepModeKeys keys,
        bool useDefaultInterval,
        int defaultIntervalMilliseconds,
        Action<int, int, PlacementStep>? stepSent,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        PlacementStep step = playable.Step;
        if (step.Kind == MatchStepKind.Delay)
        {
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: waiting {step.DelayDurationMilliseconds} ms.");
            await Task.Delay(
                    step.DelayDurationMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
            stepSent?.Invoke(
                playable.SourceIndex + 1,
                stepCount,
                step);
            return;
        }

        bool selected =
            await _unitActions.TrySelectAsync(
                    window,
                    model,
                    playable,
                    stepCount,
                    status,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!selected)
        {
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: skipped {step.Kind} because selected-unit proof never appeared.");
            return;
        }

        switch (step.Kind)
        {
            case MatchStepKind.ReconfigureUnit:
                await _unitActions.ApplyReconfigureAsync(
                        window,
                        model,
                        playable,
                        stepCount,
                        keys,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case MatchStepKind.UpgradeUnit:
                await _unitActions.ApplyUpgradeAsync(
                        window,
                        model,
                        playable,
                        stepCount,
                        keys,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                throw new InvalidDataException(
                    "The match step action is invalid.");
        }

        stepSent?.Invoke(
            playable.SourceIndex + 1,
            stepCount,
            step);
        await WaitDefaultIntervalAsync(
                playable,
                useDefaultInterval,
                defaultIntervalMilliseconds,
                status,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WaitDefaultIntervalAsync(
        MatchStepPlaybackItem playable,
        bool useDefaultInterval,
        int defaultIntervalMilliseconds,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        int delay =
            useDefaultInterval
                ? defaultIntervalMilliseconds
                : playable.Step
                    .DelayAfterMilliseconds;
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}: waiting {delay} ms before the next match step.");
        await Task.Delay(delay, cancellationToken)
            .ConfigureAwait(false);
    }

    private static MatchStepPlaybackItem[]
        CollectPlayableSteps(
            PlacementModel model,
            IReadOnlyList<PlacementStep> steps,
            Action<string>? status)
    {
        List<MatchStepPlaybackItem> playable = [];
        IReadOnlyList<PlacementStep> timeline =
            PlacementTimelinePolicy.NormalizeSteps(
                model.Steps);
        for (int index = 0; index < steps.Count; index++)
        {
            PlacementStep step = steps[index];
            string? skipReason =
                PlacementSafetyRules
                    .GetPlaybackSkipReason(
                        model,
                        step);
            if (skipReason is null)
            {
                playable.Add(
                    new MatchStepPlaybackItem(
                        index,
                        step,
                        step.HasPlacementReference
                            ? PlacementReferencePolicy
                                .ResolveTarget(
                                    timeline,
                                    step)
                            : null));
            }
            else
            {
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: skipped because {skipReason}");
            }
        }
        return [.. playable];
    }

    private static void ValidateTiming(
        int defaultIntervalMilliseconds,
        int afterKeyMilliseconds)
    {
        if (defaultIntervalMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultIntervalMilliseconds));
        }
        if (afterKeyMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(afterKeyMilliseconds));
        }
    }
}
