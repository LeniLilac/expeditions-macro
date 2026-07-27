using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Automation.Placement;

public sealed class PlacementService
{
    private const int PlacementApproachDistancePixels = 50;
    private const int PlacementApproachDurationMilliseconds = 200;
    private const int PlacementClickAttempts = 8;
    private const int SelectionPollMilliseconds = 100;
    private const int SelectionTimeoutMilliseconds = 800;
    private const int RequiredStableSelectionFrames = 2;
    private const int SelectionDismissAttempts = 8;
    private const int SelectionDismissPollMilliseconds = 100;
    private const int SelectionDismissSamples = 4;
    private const int IdleCursorInsetPixels = 24;
    private const int TargetingTapIntervalMilliseconds = 100;

    private readonly IRobloxAutomation _automation;
    private readonly IPlacementCaptureService _capture;
    private readonly PlacementModelRepository _models;
    private readonly Func<char> _targetingKey;
    private readonly Func<char> _autoUpgradeKey;

    public PlacementService(
        IRobloxAutomation automation,
        IPlacementCaptureService capture,
        PlacementModelRepository models,
        Func<char>? targetingKey = null,
        Func<char>? autoUpgradeKey = null)
    {
        _automation = automation;
        _capture = capture;
        _models = models;
        _targetingKey = targetingKey ?? (() => 'T');
        _autoUpgradeKey = autoUpgradeKey ?? (() => 'Y');
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
        if (!char.IsAsciiLetter(cancelPlacementKey))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cancelPlacementKey));
        }
        cancelPlacementKey =
            char.ToUpperInvariant(cancelPlacementKey);
        char targetingKey = _targetingKey();
        if (!char.IsAsciiLetter(targetingKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard and set Change Unit Targeting key to the same A-Z letter assigned in Anime Expeditions.");
        }
        targetingKey = char.ToUpperInvariant(targetingKey);
        char autoUpgradeKey = default;
        if (steps.Any(step => step.AutoUpgrade))
        {
            autoUpgradeKey = _autoUpgradeKey();
            if (!char.IsAsciiLetter(autoUpgradeKey))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Auto Upgrade Unit key to the same A-Z letter assigned in Anime Expeditions.");
            }
            autoUpgradeKey =
                char.ToUpperInvariant(autoUpgradeKey);
        }
        EnsureFocus(window);
        await EnsureSizeAsync(window, model.ClientWidth, model.ClientHeight, cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PlacementStep step = steps[index];
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
            bool selected = false;
            for (int attempt = 1;
                 attempt <= PlacementClickAttempts;
                 attempt++)
            {
                await EnsureSizeAsync(
                    window,
                    model.ClientWidth,
                    model.ClientHeight,
                    cancellationToken).ConfigureAwait(false);
                EnsureFocus(window);
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: placement click {attempt}/{PlacementClickAttempts} at ({step.X}, {step.Y}).");
                await ClickPlacementAsync(
                    window,
                    step,
                    cancellationToken).ConfigureAwait(false);
                selected = await WaitForSelectedUnitAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                if (selected) break;
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: the selected-unit panel did not appear after click {attempt}; retrying only the timed approach and click.");
            }
            if (!selected)
            {
                throw new RobloxUiUnavailableException(
                    $"Unit {step.UnitKey} could not be placed and selected at ({step.X}, {step.Y}) after {PlacementClickAttempts} click attempts.");
            }

            int targetingTaps =
                (int)step.TargetingPriority;
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
                        TargetingTapIntervalMilliseconds,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            if (step.AutoUpgrade)
            {
                status?.Invoke(
                    $"Step {index + 1}/{steps.Count}: enabling Auto Upgrade for the selected unit.");
                EnsureFocus(window);
                await _automation.TapLetterKeyAsync(
                    window,
                    autoUpgradeKey,
                    cancellationToken).ConfigureAwait(false);
            }
            status?.Invoke(
                $"Step {index + 1}/{steps.Count}: closing the selected-unit panel before the next action.");
            await DismissSelectedUnitPanelAsync(
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

    private async Task ClickPlacementAsync(
        RobloxWindow window,
        PlacementStep step,
        CancellationToken cancellationToken)
    {
        EnsureFocus(window);
        int approachX =
            step.X >= PlacementApproachDistancePixels
                ? step.X -
                    PlacementApproachDistancePixels
                : step.X +
                    PlacementApproachDistancePixels;
        await _automation.MoveCursorBetweenClientPointsAsync(
            window,
            approachX,
            step.Y,
            step.X,
            step.Y,
            PlacementApproachDurationMilliseconds,
            cancellationToken).ConfigureAwait(false);
        await _automation.ClickClientRetainingCursorAsync(
            window,
            step.X,
            step.Y,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> WaitForSelectedUnitAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        int stable = 0;
        int samples = Math.Max(
            RequiredStableSelectionFrames,
            1 +
            SelectionTimeoutMilliseconds /
            SelectionPollMilliseconds);
        for (int sample = 0;
             sample < samples;
             sample++)
        {
            EnsureFocus(window);
            ImageFrame frame =
                _automation.CaptureClient(window);
            SelectedUnitPanelMatch match =
                SelectedUnitPanelDetector.Detect(frame);
            stable = match.Visible ? stable + 1 : 0;
            if (stable >= RequiredStableSelectionFrames)
            {
                return true;
            }
            if (sample + 1 < samples)
            {
                await Task.Delay(
                    SelectionPollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        return false;
    }

    private async Task DismissSelectedUnitPanelAsync(
        RobloxWindow window,
        int clientWidth,
        int clientHeight,
        CancellationToken cancellationToken)
    {
        int idleX = Math.Max(
            0,
            clientWidth -
            1 -
            IdleCursorInsetPixels);
        int idleY = Math.Max(
            0,
            clientHeight -
            1 -
            IdleCursorInsetPixels);
        await _automation.ParkCursorAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        if (await WaitForSelectedUnitHiddenAsync(
            window,
            cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        for (int attempt = 0;
             attempt < SelectionDismissAttempts;
             attempt++)
        {
            EnsureFocus(window);
            await _automation.ClickClientAsync(
                window,
                idleX,
                idleY,
                cancellationToken).ConfigureAwait(false);
            if (await WaitForSelectedUnitHiddenAsync(
                window,
                cancellationToken).ConfigureAwait(false))
            {
                await _automation.ParkCursorAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            "The selected-unit panel remained open after " +
            $"{SelectionDismissAttempts} clicks at the safe idle point.");
    }

    private async Task<bool> WaitForSelectedUnitHiddenAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        int stable = 0;
        for (int sample = 0;
             sample < SelectionDismissSamples;
             sample++)
        {
            EnsureFocus(window);
            stable =
                SelectedUnitPanelIsVisible(window)
                    ? 0
                    : stable + 1;
            if (stable >=
                RequiredStableSelectionFrames)
            {
                return true;
            }
            if (sample + 1 <
                SelectionDismissSamples)
            {
                await Task.Delay(
                    SelectionDismissPollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        return false;
    }

    private bool SelectedUnitPanelIsVisible(
        RobloxWindow window)
    {
        ImageFrame frame =
            _automation.CaptureClient(window);
        return SelectedUnitPanelDetector
            .Detect(frame)
            .PanelVisible;
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
