using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

public sealed class PlacementService
{
    private const int PlacementApproachDistancePixels = 50;
    private const int PlacementApproachDurationMilliseconds = 200;
    private const int PlacementClickAttempts = 8;
    private const int UnitActionTapIntervalMilliseconds = 100;

    private readonly IRobloxAutomation _automation;
    private readonly IPlacementCaptureService _capture;
    private readonly PlacementModelRepository _models;
    private readonly SelectedUnitPanelPlayback _selectedUnitPanel;
    private readonly Func<char> _targetingKey;
    private readonly Func<char> _autoUpgradeKey;
    private readonly Func<int>? _quickPlacementKey;
    private readonly IQuickPlacementSelectionProof?
        _quickPlacementSelectionProof;

    public PlacementService(
        IRobloxAutomation automation,
        IPlacementCaptureService capture,
        PlacementModelRepository models,
        Func<char> targetingKey,
        Func<char> autoUpgradeKey,
        Func<int> quickPlacementKey)
        : this(
            automation,
            capture,
            models,
            targetingKey,
            autoUpgradeKey,
            quickPlacementKey,
            new QuickPlacementSelectionProof(
                automation))
    {
    }

    internal PlacementService(
        IRobloxAutomation automation,
        IPlacementCaptureService capture,
        PlacementModelRepository models,
        Func<char>? targetingKey = null,
        Func<char>? autoUpgradeKey = null,
        Func<int>? quickPlacementKey = null,
        IQuickPlacementSelectionProof?
            quickPlacementSelectionProof = null)
    {
        _automation = automation;
        _capture = capture;
        _models = models;
        _selectedUnitPanel =
            new SelectedUnitPanelPlayback(automation);
        _targetingKey = targetingKey ?? (() => 'T');
        _autoUpgradeKey = autoUpgradeKey ?? (() => 'Y');
        _quickPlacementKey = quickPlacementKey;
        _quickPlacementSelectionProof =
            quickPlacementSelectionProof;
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
        if (playableSteps.Length > 0 &&
            !char.IsAsciiLetter(cancelPlacementKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard and set Toggle Cancel Unit Placement key to the same A-Z letter assigned in Anime Expeditions.");
        }
        cancelPlacementKey = char.ToUpperInvariant(cancelPlacementKey);
        int quickPlacementKey = default;
        bool proveQuickPlacement =
            _quickPlacementKey is not null &&
            _quickPlacementSelectionProof is not null;
        if (playableSteps.Length > 0 &&
            proveQuickPlacement)
        {
            quickPlacementKey =
                _quickPlacementKey!();
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
                $"Step {index + 1}/{steps.Count}: normalizing unit {step.UnitKey} placement state at ({step.X}, {step.Y}).");
            await NormalizePlacementSelectionAsync(
                window,
                step,
                keyHoldMilliseconds,
                cancelPlacementKey,
                cancellationToken).ConfigureAwait(false);
            if (proveQuickPlacement)
            {
                bool ready =
                    await _quickPlacementSelectionProof!
                        .HasStableSelectionAsync(
                            window,
                            quickPlacementKey,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (!ready)
                {
                    status?.Invoke(
                        $"Step {index + 1}/{steps.Count}: Quick Placement selection proof was absent; selecting Unit {step.UnitKey} once more.");
                    EnsureFocus(window);
                    await _automation.TapUnitKeyAsync(
                        window,
                        step.UnitKey,
                        keyHoldMilliseconds,
                        cancellationToken).ConfigureAwait(false);
                    ready =
                        await _quickPlacementSelectionProof
                            .HasStableSelectionAsync(
                                window,
                                quickPlacementKey,
                                cancellationToken)
                            .ConfigureAwait(false);
                }
                if (!ready)
                {
                    status?.Invoke(
                        $"Step {index + 1}/{steps.Count}: skipped Unit {step.UnitKey} before coordinate input because Quick Placement did not confirm a selected unit.");
                    continue;
                }
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: Quick Placement confirmed Unit {step.UnitKey} is selected.");
            }
            bool selected = false;
            for (int firstClick = 1;
                 firstClick <= PlacementClickAttempts;
                 firstClick += 2)
            {
                await EnsureSizeAsync(
                    window,
                    model.ClientWidth,
                    model.ClientHeight,
                    cancellationToken).ConfigureAwait(false);
                EnsureFocus(window);
                bool useTimedApproach = firstClick > 1;
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: {(useTimedApproach ? "slow" : "fast")} place/select clicks {firstClick}-{firstClick + 1}/{PlacementClickAttempts} at ({step.X}, {step.Y}).");
                await ClickPlacementAsync(
                    window,
                    step,
                    useTimedApproach,
                    cancellationToken).ConfigureAwait(false);
                await ClickPlacementAsync(
                    window,
                    step,
                    useTimedApproach,
                    cancellationToken).ConfigureAwait(false);
                // Clear hover before capture because its unit card can cover the red Close control required for proof.
                await _automation.ParkCursorAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                selected = await _selectedUnitPanel
                    .WaitForVisibleAsync(
                        window,
                        cancellationToken).ConfigureAwait(false);
                if (selected) break;
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: selected-unit proof missing after {firstClick + 1}/{PlacementClickAttempts} clicks.");
            }
            if (!selected)
            {
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: skipped Unit {step.UnitKey} at ({step.X}, {step.Y}) after {PlacementClickAttempts} clicks because selected-unit proof never appeared.");
                continue;
            }

            if (selected)
            {
                int targetingTaps = (int)step.TargetingPriority;
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: selected unit confirmed; applying {step.TargetingPriority} targeting ({targetingTaps} key taps).");
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
            }
            stepSent?.Invoke(index + 1, steps.Count, step);
            int delay = useDefaultInterval ? defaultIntervalMilliseconds : step.DelayAfterMilliseconds;
            status?.Invoke($"Step {index + 1}/{steps.Count}: waiting {delay} ms after click.");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task NormalizePlacementSelectionAsync(
        RobloxWindow window,
        PlacementStep step,
        int keyHoldMilliseconds,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        await _automation.TapUnitKeyAsync(
            window,
            step.UnitKey,
            keyHoldMilliseconds,
            cancellationToken).ConfigureAwait(false);
        await _automation.TapLetterKeyAsync(
            window,
            cancelPlacementKey,
            cancellationToken).ConfigureAwait(false);
        await _automation.TapUnitKeyAsync(
            window,
            step.UnitKey,
            keyHoldMilliseconds,
            cancellationToken).ConfigureAwait(false);
        await _automation.TapUnitKeyAsync(
            window,
            step.UnitKey,
            keyHoldMilliseconds,
            cancellationToken).ConfigureAwait(false);
        await _automation.TapUnitKeyAsync(
            window,
            step.UnitKey,
            keyHoldMilliseconds,
            cancellationToken).ConfigureAwait(false);
    }

    private static string FormatAutoUpgradePriority(
        UnitAutoUpgradePriority priority) =>
        priority == UnitAutoUpgradePriority.Off
            ? "Off"
            : $"Priority {(int)priority}";

    private async Task ClickPlacementAsync(
        RobloxWindow window,
        PlacementStep step,
        bool useTimedApproach,
        CancellationToken cancellationToken)
    {
        EnsureFocus(window);
        if (useTimedApproach)
        {
            int approachX =
                step.X >= PlacementApproachDistancePixels
                    ? step.X - PlacementApproachDistancePixels
                    : step.X + PlacementApproachDistancePixels;
            await _automation.MoveCursorBetweenClientPointsAsync(
                window,
                approachX,
                step.Y,
                step.X,
                step.Y,
                PlacementApproachDurationMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        await _automation.ClickClientRetainingCursorAsync(
            window,
            step.X,
            step.Y,
            cancellationToken).ConfigureAwait(false);
    }

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
