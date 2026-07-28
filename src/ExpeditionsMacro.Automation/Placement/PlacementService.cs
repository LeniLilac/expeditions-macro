using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

public sealed class PlacementService
{
    private const int PlacementSelectionAttempts = 8;
    private const int UnitActionTapIntervalMilliseconds = 100;

    private readonly IRobloxAutomation _automation;
    private readonly IPlacementCaptureService _capture;
    private readonly PlacementModelRepository _models;
    private readonly SelectedUnitPanelPlayback _selectedUnitPanel;
    private readonly Func<char> _targetingKey;
    private readonly Func<char> _autoUpgradeKey;
    private readonly Func<int> _quickPlacementKey;

    public PlacementService(
        IRobloxAutomation automation,
        IPlacementCaptureService capture,
        PlacementModelRepository models,
        Func<char>? targetingKey = null,
        Func<char>? autoUpgradeKey = null,
        Func<int>? quickPlacementKey = null)
    {
        _automation = automation;
        _capture = capture;
        _models = models;
        _selectedUnitPanel =
            new SelectedUnitPanelPlayback(automation);
        _targetingKey = targetingKey ?? (() => 'T');
        _autoUpgradeKey = autoUpgradeKey ?? (() => 'Y');
        _quickPlacementKey =
            quickPlacementKey ??
            (() => KeyboardKey.LeftShift);
    }

    public async Task<PlacementModel> RecordAsync(
        string name,
        int defaultDelayMilliseconds,
        bool useRecordedDelays,
        Action<PlacementCapture>? captured = null,
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a placement model name.", nameof(name));
        RobloxWindow window = _automation.FindWindow() ?? throw new RobloxSessionUnavailableException("No visible Roblox window was found.");
        ClientBounds initial = _automation.GetClientBounds(window);
        bool resized = initial.Width != RobloxClientProfile.Width || initial.Height != RobloxClientProfile.Height;
        (int width, int height, IReadOnlyList<PlacementCapture> captures) recording;
        EnsureFocus(window);
        if (resized)
        {
            status?.Invoke($"Resizing Roblox to {RobloxClientProfile.Width} × {RobloxClientProfile.Height}.");
            await _automation.ResizeClientAsync(window, RobloxClientProfile.Width, RobloxClientProfile.Height, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        ClientBounds client = _automation.GetClientBounds(window);
        if (client.Width != RobloxClientProfile.Width || client.Height != RobloxClientProfile.Height)
        {
            throw new RobloxSessionUnavailableException($"Roblox did not accept the standard {RobloxClientProfile.Width} × {RobloxClientProfile.Height} client size.");
        }
        recording = await _capture.RecordAsync(window, captured, status, cancellationToken).ConfigureAwait(false);
        (int width, int height, IReadOnlyList<PlacementCapture> captures) = recording;
        if (captures.Count == 0) throw new InvalidOperationException("Record at least one unit placement before saving.");
        string id = ModelId.FromName(name);
        PlacementModel model = new()
        {
            Id = id,
            Name = name.Trim(),
            ClientWidth = width,
            ClientHeight = height,
            Steps = PlacementModel.FromCaptures(captures, defaultDelayMilliseconds, useRecordedDelays),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        // The global macro hotkey ends a recording by cancelling the observation token. Saving the
        // completed captures must therefore use an independent token.
        await _models.SaveAsync(model, CancellationToken.None).ConfigureAwait(false);
        return model;
    }

    public async Task PlayAsync(
        PlacementModel model,
        bool useDefaultInterval,
        int defaultIntervalMilliseconds,
        int keyHoldMilliseconds = 110,
        int afterKeyMilliseconds = 250,
        char cancelPlacementKey =
            AppSettings.DefaultCancelPlacementKeyChar,
        Action<int, int, PlacementStep>? stepSent = null,
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        model.Validate();
        RobloxWindow window = _automation.FindWindow() ?? throw new RobloxSessionUnavailableException("No visible Roblox window was found.");
        await PlayStepsAsync(
            window,
            model,
            model.Steps,
            useDefaultInterval,
            defaultIntervalMilliseconds,
            keyHoldMilliseconds,
            afterKeyMilliseconds,
            cancelPlacementKey,
            stepSent,
            status,
            cancellationToken).ConfigureAwait(false);
    }
    public async Task PlayStepsAsync(
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
        if (defaultIntervalMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(defaultIntervalMilliseconds));
        if (afterKeyMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(afterKeyMilliseconds));
        if (steps.Count == 0)
        {
            return;
        }
        PlacementStep[] playableSteps = steps
            .Where(step =>
                PlacementSafetyRules
                    .GetPlaybackSkipReason(
                        model,
                        step) is null)
            .ToArray();
        int quickPlacementKey = default;
        if (playableSteps.Length > 0)
        {
            quickPlacementKey = _quickPlacementKey();
            if (!KeyboardKey
                    .IsSupportedQuickPlacementKey(
                        quickPlacementKey))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard, then set Quick Placement key to the same physical key assigned in Anime Expeditions.");
            }
        }
        char targetingKey = default;
        if (playableSteps.Any(step =>
                (int)step.TargetingPriority > 0))
        {
            targetingKey = _targetingKey();
            if (!char.IsAsciiLetter(targetingKey))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Change Unit Targeting key to the same A-Z letter assigned in Anime Expeditions.");
            }
            targetingKey = char.ToUpperInvariant(targetingKey);
        }
        char autoUpgradeKey = default;
        if (playableSteps.Any(step =>
                step.AutoUpgradePriority != UnitAutoUpgradePriority.Off))
        {
            autoUpgradeKey = _autoUpgradeKey();
            if (!char.IsAsciiLetter(autoUpgradeKey))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Auto Upgrade Unit key to the same A-Z letter assigned in Anime Expeditions.");
            }
            autoUpgradeKey = char.ToUpperInvariant(autoUpgradeKey);
        }
        for (int index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PlacementStep step = steps[index];
            string? skipReason = PlacementSafetyRules
                .GetPlaybackSkipReason(model, step);
            if (skipReason is not null)
            {
                status?.Invoke($"Step {index + 1}/{steps.Count}: skipped because {skipReason}");
                continue;
            }
            await EnsureSizeAsync(window, model.ClientWidth, model.ClientHeight, cancellationToken).ConfigureAwait(false);
            EnsureFocus(window);
            status?.Invoke(
                $"Step {index + 1}/{steps.Count}: holding Quick Placement and trying Unit {step.UnitKey} at ({step.X}, {step.Y}).");
            bool selected =
                await TrySelectPlacedUnitAsync(
                window,
                model,
                step,
                index,
                steps.Count,
                quickPlacementKey,
                keyHoldMilliseconds,
                afterKeyMilliseconds,
                status,
                cancellationToken).ConfigureAwait(false);
            if (!selected)
            {
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: skipped Unit {step.UnitKey} at ({step.X}, {step.Y}) after {PlacementSelectionAttempts} Quick Placement attempts because selected-unit proof never appeared.");
                continue;
            }

            int targetingTaps = (int)step.TargetingPriority;
            status?.Invoke(
                $"Step {index + 1}/{steps.Count}: selected unit confirmed and Quick Placement released; applying {step.TargetingPriority} targeting ({targetingTaps} key taps).");
            for (int tap = 0;
                 tap < targetingTaps;
                 tap++)
            {
                EnsureFocus(window);
                await _automation.TapLetterKeyAsync(
                    window,
                    targetingKey,
                    cancellationToken).ConfigureAwait(false);
                if (tap + 1 < targetingTaps)
                {
                    await Task.Delay(
                        UnitActionTapIntervalMilliseconds,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            int autoUpgradeTaps = (int)step.AutoUpgradePriority;
            if (autoUpgradeTaps > 0)
            {
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: applying Auto Upgrade {FormatAutoUpgradePriority(step.AutoUpgradePriority)} ({autoUpgradeTaps} key taps).");
                for (int tap = 0;
                     tap < autoUpgradeTaps;
                     tap++)
                {
                    EnsureFocus(window);
                    await _automation.TapLetterKeyAsync(
                        window,
                        autoUpgradeKey,
                        cancellationToken).ConfigureAwait(false);
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    if (tap + 1 < autoUpgradeTaps)
                    {
                        await Task.Delay(
                            UnitActionTapIntervalMilliseconds,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            status?.Invoke(
                $"Step {index + 1}/{steps.Count}: closing the selected-unit panel before the next action.");
            await _selectedUnitPanel.DismissAsync(
                window,
                model.ClientWidth,
                model.ClientHeight,
                cancellationToken).ConfigureAwait(false);
            stepSent?.Invoke(index + 1, steps.Count, step);
            int delay = useDefaultInterval ? defaultIntervalMilliseconds : step.DelayAfterMilliseconds;
            status?.Invoke($"Step {index + 1}/{steps.Count}: waiting {delay} ms after click.");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<bool> TrySelectPlacedUnitAsync(
        RobloxWindow window,
        PlacementModel model,
        PlacementStep step,
        int index,
        int stepCount,
        int quickPlacementKey,
        int keyHoldMilliseconds,
        int afterKeyMilliseconds,
        Action<string>? status,
        CancellationToken cancellationToken) =>
        _automation.RunWithKeyHeldAsync(
            window,
            quickPlacementKey,
            async heldToken =>
            {
                for (int attempt = 1;
                     attempt <= PlacementSelectionAttempts;
                     attempt++)
                {
                    await EnsureSizeAsync(
                        window,
                        model.ClientWidth,
                        model.ClientHeight,
                        heldToken).ConfigureAwait(false);
                    EnsureFocus(window);
                    status?.Invoke(
                        $"Step {index + 1}/{stepCount}: Quick Placement attempt {attempt}/{PlacementSelectionAttempts}; tapping Unit {step.UnitKey} and clicking ({step.X}, {step.Y}).");
                    await _automation.TapUnitKeyAsync(
                        window,
                        step.UnitKey,
                        keyHoldMilliseconds,
                        heldToken).ConfigureAwait(false);
                    if (afterKeyMilliseconds > 0)
                    {
                        await Task.Delay(
                            afterKeyMilliseconds,
                            heldToken).ConfigureAwait(false);
                    }
                    EnsureFocus(window);
                    await _automation
                        .ClickClientRetainingCursorAsync(
                            window,
                            step.X,
                            step.Y,
                            heldToken).ConfigureAwait(false);
                    // Clear hover before capture because its unit card can cover
                    // the red Close control required for selected-unit proof.
                    await _automation.ParkCursorAsync(
                        window,
                        heldToken).ConfigureAwait(false);
                    if (await _selectedUnitPanel
                            .WaitForVisibleAsync(
                                window,
                                heldToken)
                            .ConfigureAwait(false))
                    {
                        return true;
                    }
                    status?.Invoke(
                        $"Step {index + 1}/{stepCount}: selected-unit proof missing after Quick Placement attempt {attempt}/{PlacementSelectionAttempts}.");
                }
                return false;
            },
            cancellationToken);

    private static string FormatAutoUpgradePriority(
        UnitAutoUpgradePriority priority) =>
        priority == UnitAutoUpgradePriority.Off
            ? "Off"
            : $"Priority {(int)priority}";

    private async Task EnsureSizeAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
    {
        ClientBounds bounds = _automation.GetClientBounds(window);
        if (bounds.Width != width || bounds.Height != height)
        {
            await _automation.ResizeClientAsync(window, width, height, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        ClientBounds actual = _automation.GetClientBounds(window);
        if (actual.Width != width || actual.Height != height) throw new RobloxSessionUnavailableException("Roblox did not accept the placement model's client size.");
    }

    private void EnsureFocus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new RobloxSessionUnavailableException("Windows could not focus Roblox.");
    }
}
