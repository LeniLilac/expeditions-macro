using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class PlacementBatchPlayback(
    IRobloxAutomation automation)
{
    private const int PlacementBurstClicks = 3;
    private const int DefaultBurstDurationMilliseconds = 50;

    public async Task PlaceAsync(
        RobloxWindow window,
        PlacementModel model,
        IReadOnlyList<MatchStepPlaybackItem> steps,
        PlacementStepModeKeys keys,
        int keyHoldMilliseconds,
        int fallbackUnitSelectionDelayMilliseconds,
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
        await automation.TapLetterKeyAsync(
                window,
                keys.CancelPlacement,
                cancellationToken)
            .ConfigureAwait(false);

        PlacementAdvancedSettings advanced =
            model.AdvancedSettings;
        int selectionDelay =
            advanced.Enabled
                ? advanced
                    .UnitSelectionDelayMilliseconds
                : fallbackUnitSelectionDelayMilliseconds;
        int burstDuration =
            advanced.Enabled
                ? advanced
                    .PlacementBurstDurationMilliseconds
                : DefaultBurstDurationMilliseconds;

        await automation.RunWithKeyHeldAsync(
                window,
                keys.QuickPlacement,
                async heldToken =>
                {
                    int? selectedUnit = null;
                    foreach (MatchStepPlaybackItem playable in
                             steps)
                    {
                        await EnsureSizeAsync(
                                window,
                                model.ClientWidth,
                                model.ClientHeight,
                                heldToken)
                            .ConfigureAwait(false);
                        EnsureFocus(window);
                        PlacementStep step = playable.Step;
                        if (selectedUnit != step.UnitKey)
                        {
                            status?.Invoke(
                                $"Step {playable.SourceIndex + 1}: selecting Unit {step.UnitKey} for placement attempt {attempt}/{totalAttempts}.");
                            await automation.TapUnitKeyAsync(
                                    window,
                                    step.UnitKey,
                                    keyHoldMilliseconds,
                                    heldToken)
                                .ConfigureAwait(false);
                            selectedUnit = step.UnitKey;
                            if (selectionDelay > 0)
                            {
                                await Task.Delay(
                                        selectionDelay,
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
                            $"Step {playable.SourceIndex + 1}: clicking ({step.X}, {step.Y}) {PlacementBurstClicks} times over {burstDuration} ms.");
                        await automation
                            .ClickClientBurstRetainingCursorAsync(
                                window,
                                step.X,
                                step.Y,
                                PlacementBurstClicks,
                                burstDuration,
                                heldToken)
                            .ConfigureAwait(false);
                    }
                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);

        EnsureFocus(window);
        status?.Invoke(
            $"Placement attempt {attempt}/{totalAttempts}: Quick Placement released; canceling placement mode before unit actions.");
        await automation.TapLetterKeyAsync(
                window,
                keys.CancelPlacement,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EnsureSizeAsync(
        RobloxWindow window,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        ClientBounds bounds =
            automation.GetClientBounds(window);
        if (bounds.Width != width ||
            bounds.Height != height)
        {
            await automation.ResizeClientAsync(
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
            automation.GetClientBounds(window);
        if (actual.Width != width ||
            actual.Height != height)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox did not accept the placement model's client size.");
        }
    }

    private void EnsureFocus(RobloxWindow window)
    {
        if (!automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }
}
