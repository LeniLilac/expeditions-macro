using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class PlacementUnitActionPlayback
{
    private const int DefaultActionKeyIntervalMilliseconds =
        100;
    private const int SellAttempts = 3;

    private readonly IRobloxAutomation _automation;
    private readonly SelectedUnitPanelPlayback _selectedUnitPanel;
    private readonly PlacementUpgradeActionPlayback
        _upgradeActions;
    private readonly Dictionary<string, UnitState> _states =
        [];

    public PlacementUnitActionPlayback(
        IRobloxAutomation automation)
    {
        _automation = automation;
        _selectedUnitPanel =
            new SelectedUnitPanelPlayback(automation);
        _upgradeActions =
            new PlacementUpgradeActionPlayback(automation);
    }

    public void BeginMatch() =>
        _states.Clear();

    public async Task<bool> TrySelectAsync(
        RobloxWindow window,
        PlacementModel model,
        MatchStepPlaybackItem playable,
        int stepCount,
        bool requireProof,
        bool requireFirstPriorityProof,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        await EnsureSizeAsync(
                window,
                model.ClientWidth,
                model.ClientHeight,
                cancellationToken)
            .ConfigureAwait(false);
        PlacementAdvancedSettings advanced =
            model.AdvancedSettings;
        if (advanced.Enabled &&
            advanced.BeforeSelectionClickMilliseconds > 0)
        {
            await Task.Delay(
                    advanced
                        .BeforeSelectionClickMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        EnsureFocus(window);
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: clicking Unit {playable.UnitKey} at ({playable.X}, {playable.Y}).");
        await _automation.ClickClientRetainingCursorAsync(
                window,
                playable.X,
                playable.Y,
                cancellationToken)
            .ConfigureAwait(false);
        await _automation.ParkCursorAsync(
                window,
                cancellationToken)
            .ConfigureAwait(false);

        if (advanced.Enabled &&
            advanced.BeforeSelectedUnitProofMilliseconds > 0)
        {
            await Task.Delay(
                    advanced
                        .BeforeSelectedUnitProofMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (!requireProof)
        {
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: advanced mode skipped selected-unit proof.");
            return true;
        }

        bool selected = requireFirstPriorityProof
            ? await _selectedUnitPanel
                .WaitForVisibleAsync(
                    window,
                    cancellationToken)
                .ConfigureAwait(false)
            : await _selectedUnitPanel
                .WaitForPanelVisibleAsync(
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

    public async Task ApplyPlacementAsync(
        RobloxWindow window,
        PlacementModel model,
        MatchStepPlaybackItem playable,
        int stepCount,
        PlacementStepModeKeys keys,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        PlacementStep step = playable.Step;
        int targetingTaps =
            (int)step.TargetingPriority;
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: applying {step.TargetingPriority} targeting ({targetingTaps} key taps).");
        await TapActionKeyAsync(
                window,
                keys.Targeting,
                targetingTaps,
                ActionInterval(model),
                cancellationToken)
            .ConfigureAwait(false);

        int autoUpgradeTaps =
            (int)step.AutoUpgradePriority;
        if (autoUpgradeTaps > 0)
        {
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: enabling Auto Upgrade Priority {autoUpgradeTaps}.");
            await TapActionKeyAsync(
                    window,
                    keys.AutoUpgrade,
                    autoUpgradeTaps,
                    ActionInterval(model),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        _states[playable.PlacementId] =
            new UnitState(
                step.TargetingPriority,
                step.AutoUpgradePriority);
        await DismissAsync(
                window,
                model,
                playable,
                stepCount,
                status,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyReconfigureAsync(
        RobloxWindow window,
        PlacementModel model,
        MatchStepPlaybackItem playable,
        int stepCount,
        PlacementStepModeKeys keys,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        PlacementStep step = playable.Step;
        UnitState current =
            _states.GetValueOrDefault(
                playable.PlacementId,
                new UnitState(
                    UnitTargetingPriority.First,
                    UnitAutoUpgradePriority.Off));
        UnitTargetingPriority targeting =
            current.Targeting;
        UnitAutoUpgradePriority autoUpgrade =
            current.AutoUpgrade;
        int interval = ActionInterval(model);

        if (step.ChangeTargetingPriority)
        {
            int stateCount =
                Enum.GetValues<UnitTargetingPriority>()
                    .Length;
            int targetingTaps =
                ((int)step.TargetingPriority -
                 (int)current.Targeting +
                 stateCount) %
                stateCount;
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: changing targeting from {current.Targeting} to {step.TargetingPriority} ({targetingTaps} key taps).");
            await TapActionKeyAsync(
                    window,
                    keys.Targeting,
                    targetingTaps,
                    interval,
                    cancellationToken)
                .ConfigureAwait(false);
            targeting = step.TargetingPriority;
        }

        if (step.AutoUpgradeAction !=
            MatchAutoUpgradeAction.NoChange)
        {
            UnitAutoUpgradePriority requestedAutoUpgrade =
                AutoUpgradePriority(
                    step.AutoUpgradeAction);
            int stateCount =
                Enum.GetValues<
                        UnitAutoUpgradePriority>()
                    .Length;
            int autoUpgradeTaps =
                ((int)requestedAutoUpgrade -
                 (int)current.AutoUpgrade +
                 stateCount) %
                stateCount;
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: changing Auto Upgrade from {current.AutoUpgrade} to {requestedAutoUpgrade} ({autoUpgradeTaps} key taps).");
            await TapActionKeyAsync(
                    window,
                    keys.AutoUpgrade,
                    autoUpgradeTaps,
                    interval,
                    cancellationToken)
                .ConfigureAwait(false);
            autoUpgrade = requestedAutoUpgrade;
        }

        _states[playable.PlacementId] =
            new UnitState(targeting, autoUpgrade);
        await DismissAsync(
                window,
                model,
                playable,
                stepCount,
                status,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyUpgradeAsync(
        RobloxWindow window,
        PlacementModel model,
        MatchStepPlaybackItem playable,
        int stepCount,
        PlacementStepModeKeys keys,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        await _upgradeActions.ApplyAsync(
                window,
                keys.Upgrade,
                playable.Step.UpgradeCount,
                ActionInterval(model),
                !model.AdvancedSettings.Enabled ||
                model.AdvancedSettings
                    .RequireUpgradeUnitReadiness,
                playable.SourceIndex + 1,
                stepCount,
                status,
                cancellationToken)
            .ConfigureAwait(false);
        await DismissAsync(
                window,
                model,
                playable,
                stepCount,
                status,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplySellAsync(
        RobloxWindow window,
        PlacementModel model,
        MatchStepPlaybackItem playable,
        int stepCount,
        PlacementStepModeKeys keys,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: selling placed Unit {playable.UnitKey}.");
        for (int attempt = 1;
             attempt <= SellAttempts;
             attempt++)
        {
            await TapActionKeyAsync(
                    window,
                    keys.Sell,
                    1,
                    ActionInterval(model),
                    cancellationToken)
                .ConfigureAwait(false);
            if (await _selectedUnitPanel
                    .WaitForHiddenAfterActionAsync(
                        window,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                _states.Remove(playable.PlacementId);
                return;
            }
            if (attempt == SellAttempts)
            {
                break;
            }
            if (!await _selectedUnitPanel
                    .WaitForPanelVisibleAsync(
                        window,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new RobloxUiUnavailableException(
                    "The selected-unit panel could not be reverified after Sell Unit was pressed, so the macro sent no retry.");
            }
            status?.Invoke(
                $"Step {playable.SourceIndex + 1}/{stepCount}: Sell Unit was ignored; retrying verified input ({attempt + 1}/{SellAttempts}).");
        }
        throw new RobloxUiUnavailableException(
            $"The selected-unit panel remained open after {SellAttempts} verified Sell Unit key attempts.");
    }

    private async Task DismissAsync(
        RobloxWindow window,
        PlacementModel model,
        MatchStepPlaybackItem playable,
        int stepCount,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        status?.Invoke(
            $"Step {playable.SourceIndex + 1}/{stepCount}: closing the selected-unit panel.");
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
        int intervalMilliseconds,
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
            if (tap + 1 < tapCount &&
                intervalMilliseconds > 0)
            {
                await Task.Delay(
                        intervalMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
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

    private void EnsureFocus(RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }

    private static int ActionInterval(
        PlacementModel model) =>
        model.AdvancedSettings.Enabled
            ? model.AdvancedSettings
                .ActionKeyIntervalMilliseconds
            : DefaultActionKeyIntervalMilliseconds;

    private static UnitAutoUpgradePriority
        AutoUpgradePriority(
        MatchAutoUpgradeAction action) =>
        action switch
        {
            MatchAutoUpgradeAction.Disable =>
                UnitAutoUpgradePriority.Off,
            MatchAutoUpgradeAction.Priority1 =>
                UnitAutoUpgradePriority.Priority1,
            MatchAutoUpgradeAction.Priority2 =>
                UnitAutoUpgradePriority.Priority2,
            MatchAutoUpgradeAction.Priority3 =>
                UnitAutoUpgradePriority.Priority3,
            MatchAutoUpgradeAction.Priority4 =>
                UnitAutoUpgradePriority.Priority4,
            MatchAutoUpgradeAction.Priority5 =>
                UnitAutoUpgradePriority.Priority5,
            MatchAutoUpgradeAction.Priority6 =>
                UnitAutoUpgradePriority.Priority6,
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "The Auto Upgrade action must select a state."),
        };

    private readonly record struct UnitState(
        UnitTargetingPriority Targeting,
        UnitAutoUpgradePriority AutoUpgrade);
}
