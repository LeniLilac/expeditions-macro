using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class PlacementStepModePlayback
{
    private const int PlacementBurstClicks = 3;
    private const int PlacementBurstDurationMilliseconds = 50;
    private const int UnitActionTapIntervalMilliseconds = 100;

    private readonly IRobloxAutomation _automation;
    private readonly SelectedUnitPanelPlayback
        _selectedUnitPanel;
    private readonly PlacementStepModeKeyResolver
        _keyResolver;

    public PlacementStepModePlayback(
        IRobloxAutomation automation,
        Func<char> targetingKey,
        Func<char> autoUpgradeKey,
        Func<int> quickPlacementKey)
    {
        _automation = automation;
        _selectedUnitPanel =
            new SelectedUnitPanelPlayback(automation);
        _keyResolver =
            new PlacementStepModeKeyResolver(
                targetingKey,
                autoUpgradeKey,
                quickPlacementKey);
    }

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

        PlayableStep[] playableSteps =
            CollectPlayableSteps(
                model,
                steps,
                status);
        if (playableSteps.Length == 0)
        {
            return;
        }

        PlacementStepModeKeys keys =
            _keyResolver.Resolve(
                playableSteps
                    .Select(step => step.Step)
                    .ToArray(),
                cancelPlacementKey);
        await PlaceBatchAsync(
                window,
                model,
                playableSteps,
                keys,
                keyHoldMilliseconds,
                afterKeyMilliseconds,
                attempt: 1,
                model.PlacementAttempts,
                status,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (PlayableStep playable in playableSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool selected =
                await TrySelectPlacedUnitAsync(
                        window,
                        model,
                        playable,
                        steps.Count,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
            for (int attempt = 2;
                 !selected &&
                 attempt <= model.PlacementAttempts;
                 attempt++)
            {
                await PlaceBatchAsync(
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
                    await TrySelectPlacedUnitAsync(
                            window,
                            model,
                            playable,
                            steps.Count,
                            status,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            if (!selected)
            {
                status?.Invoke(
                    $"Step {playable.SourceIndex + 1}/{steps.Count}: skipped Unit {playable.Step.UnitKey} at ({playable.Step.X}, {playable.Step.Y}) after {model.PlacementAttempts} placement attempt(s) because selected-unit proof never appeared.");
                continue;
            }

            await ConfigureSelectedUnitAsync(
                    window,
                    model,
                    playable,
                    steps.Count,
                    keys,
                    status,
                    cancellationToken)
                .ConfigureAwait(false);
            stepSent?.Invoke(
                playable.SourceIndex + 1,
                steps.Count,
                playable.Step);
            int delay =
                useDefaultInterval
                    ? defaultIntervalMilliseconds
                    : playable.Step
                        .DelayAfterMilliseconds;
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{steps.Count}: waiting {delay} ms before checking the next unit.");
            await Task.Delay(
                    delay,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task PlaceBatchAsync(
        RobloxWindow window,
        PlacementModel model,
        IReadOnlyList<PlayableStep> steps,
        PlacementStepModeKeys keys,
        int keyHoldMilliseconds,
        int afterKeyMilliseconds,
        int attempt,
        int totalAttempts,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        await EnsureSizeAsync(
                window,
                model.ClientWidth,
                model.ClientHeight,
                cancellationToken)
            .ConfigureAwait(false);
        EnsureFocus(window);
        status?.Invoke(
            $"Placement attempt {attempt}/{totalAttempts}: canceling any existing placement selection before the {steps.Count}-unit batch.");
        await _automation.TapLetterKeyAsync(
                window,
                keys.CancelPlacement,
                cancellationToken)
            .ConfigureAwait(false);

        await _automation.RunWithKeyHeldAsync(
                window,
                keys.QuickPlacement,
                async heldToken =>
                {
                    int? selectedUnit = null;
                    foreach (PlayableStep playable in steps)
                    {
                        await EnsureSizeAsync(
                                window,
                                model.ClientWidth,
                                model.ClientHeight,
                                heldToken)
                            .ConfigureAwait(false);
                        EnsureFocus(window);
                        PlacementStep step =
                            playable.Step;
                        if (selectedUnit !=
                            step.UnitKey)
                        {
                            status?.Invoke(
                                $"Step {playable.SourceIndex + 1}: selecting Unit {step.UnitKey} for placement attempt {attempt}/{totalAttempts}.");
                            await _automation
                                .TapUnitKeyAsync(
                                    window,
                                    step.UnitKey,
                                    keyHoldMilliseconds,
                                    heldToken)
                                .ConfigureAwait(false);
                            selectedUnit = step.UnitKey;
                            if (afterKeyMilliseconds > 0)
                            {
                                await Task.Delay(
                                        afterKeyMilliseconds,
                                        heldToken)
                                    .ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            status?.Invoke(
                                $"Step {playable.SourceIndex + 1}: reusing Unit {step.UnitKey}, which Quick Placement kept selected.");
                        }

                        EnsureFocus(window);
                        status?.Invoke(
                            $"Step {playable.SourceIndex + 1}: clicking ({step.X}, {step.Y}) {PlacementBurstClicks} times over {PlacementBurstDurationMilliseconds} ms.");
                        await _automation
                            .ClickClientBurstRetainingCursorAsync(
                                window,
                                step.X,
                                step.Y,
                                PlacementBurstClicks,
                                PlacementBurstDurationMilliseconds,
                                heldToken)
                            .ConfigureAwait(false);
                    }
                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);

        EnsureFocus(window);
        status?.Invoke(
            $"Placement attempt {attempt}/{totalAttempts}: Quick Placement released; canceling placement mode before selected-unit checks.");
        await _automation.TapLetterKeyAsync(
                window,
                keys.CancelPlacement,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TrySelectPlacedUnitAsync(
        RobloxWindow window,
        PlacementModel model,
        PlayableStep playable,
        int stepCount,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        await EnsureSizeAsync(
                window,
                model.ClientWidth,
                model.ClientHeight,
                cancellationToken)
            .ConfigureAwait(false);
        EnsureFocus(window);
        PlacementStep step = playable.Step;
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: clicking placed Unit {step.UnitKey} at ({step.X}, {step.Y}) for selected-unit proof.");
        await _automation.ClickClientRetainingCursorAsync(
                window,
                step.X,
                step.Y,
                cancellationToken)
            .ConfigureAwait(false);
        await _automation.ParkCursorAsync(
                window,
                cancellationToken)
            .ConfigureAwait(false);
        bool selected =
            await _selectedUnitPanel.WaitForVisibleAsync(
                    window,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!selected)
        {
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: selected-unit proof did not appear.");
        }
        return selected;
    }

    private async Task ConfigureSelectedUnitAsync(
        RobloxWindow window,
        PlacementModel model,
        PlayableStep playable,
        int stepCount,
        PlacementStepModeKeys keys,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        PlacementStep step = playable.Step;
        int targetingTaps =
            (int)step.TargetingPriority;
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: selected unit confirmed; applying {step.TargetingPriority} targeting ({targetingTaps} key taps).");
        await TapActionKeyAsync(
                window,
                keys.Targeting,
                targetingTaps,
                cancellationToken)
            .ConfigureAwait(false);

        int autoUpgradeTaps =
            (int)step.AutoUpgradePriority;
        if (autoUpgradeTaps > 0)
        {
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: applying Auto Upgrade {FormatAutoUpgradePriority(step.AutoUpgradePriority)} ({autoUpgradeTaps} key taps).");
            await TapActionKeyAsync(
                    window,
                    keys.AutoUpgrade,
                    autoUpgradeTaps,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: closing the selected-unit panel before the next check.");
        await _selectedUnitPanel.DismissAsync(
                window,
                model.ClientWidth,
                model.ClientHeight,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task TapActionKeyAsync(
        RobloxWindow window,
        char key,
        int tapCount,
        CancellationToken cancellationToken)
    {
        for (int tap = 0; tap < tapCount; tap++)
        {
            EnsureFocus(window);
            await _automation.TapLetterKeyAsync(
                    window,
                    key,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken
                .ThrowIfCancellationRequested();
            if (tap + 1 < tapCount)
            {
                await Task.Delay(
                        UnitActionTapIntervalMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static PlayableStep[] CollectPlayableSteps(
        PlacementModel model,
        IReadOnlyList<PlacementStep> steps,
        Action<string>? status)
    {
        List<PlayableStep> playable = [];
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
                    new PlayableStep(
                        index,
                        step));
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

    private async Task EnsureSizeAsync(
        RobloxWindow window,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != width ||
            bounds.Height != height)
        {
            await _automation.ResizeClientAsync(
                    window,
                    width,
                    height,
                    cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(
                    250,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        ClientBounds actual =
            _automation.GetClientBounds(window);
        if (actual.Width != width ||
            actual.Height != height)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox did not accept the placement model's client size.");
        }
    }

    private void EnsureFocus(
        RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }

    private static string FormatAutoUpgradePriority(
        UnitAutoUpgradePriority priority) =>
        priority == UnitAutoUpgradePriority.Off
            ? "Off"
            : $"Priority {(int)priority}";

    private sealed record PlayableStep(
        int SourceIndex,
        PlacementStep Step);

}
