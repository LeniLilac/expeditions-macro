using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

public sealed class PlacementService
{
    private readonly IRobloxAutomation _automation;
    private readonly IPlacementCaptureService _capture;
    private readonly PlacementModelRepository _models;
    private readonly PlacementStepModePlayback
        _stepModePlayback;

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
        _stepModePlayback =
            new PlacementStepModePlayback(
                automation,
                targetingKey ?? (() => 'T'),
                autoUpgradeKey ?? (() => 'Y'),
                quickPlacementKey ??
                    (() => KeyboardKey.LeftShift));
    }

    public async Task<PlacementModel> RecordAsync(
        string name,
        int defaultDelayMilliseconds,
        bool useRecordedDelays,
        Action<PlacementCapture>? captured = null,
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Enter a placement model name.",
                nameof(name));
        }
        RobloxWindow window =
            _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        ClientBounds initial =
            _automation.GetClientBounds(window);
        bool resized =
            initial.Width != RobloxClientProfile.Width ||
            initial.Height != RobloxClientProfile.Height;
        EnsureFocus(window);
        if (resized)
        {
            status?.Invoke(
                $"Resizing Roblox to {RobloxClientProfile.Width} × {RobloxClientProfile.Height}.");
            await _automation.ResizeClientAsync(
                    window,
                    RobloxClientProfile.Width,
                    RobloxClientProfile.Height,
                    cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(
                    250,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        ClientBounds client =
            _automation.GetClientBounds(window);
        if (client.Width != RobloxClientProfile.Width ||
            client.Height != RobloxClientProfile.Height)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox did not accept the standard {RobloxClientProfile.Width} × {RobloxClientProfile.Height} client size.");
        }

        (
            int width,
            int height,
            IReadOnlyList<PlacementCapture> captures) =
            await _capture.RecordAsync(
                    window,
                    captured,
                    status,
                    cancellationToken)
                .ConfigureAwait(false);
        if (captures.Count == 0)
        {
            throw new InvalidOperationException(
                "Record at least one unit placement before saving.");
        }

        string id = ModelId.FromName(name);
        PlacementModel model = new()
        {
            Id = id,
            Name = name.Trim(),
            ClientWidth = width,
            ClientHeight = height,
            Steps = PlacementModel.FromCaptures(
                captures,
                defaultDelayMilliseconds,
                useRecordedDelays),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        // The global macro hotkey ends a recording by cancelling the
        // observation token. Persist completed captures independently.
        await _models.SaveAsync(
                model,
                CancellationToken.None)
            .ConfigureAwait(false);
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
        RobloxWindow window =
            _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
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
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task PlayStepsAsync(
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
        CancellationToken cancellationToken) =>
        _stepModePlayback.PlayAsync(
            window,
            model,
            steps,
            useDefaultInterval,
            defaultIntervalMilliseconds,
            keyHoldMilliseconds,
            afterKeyMilliseconds,
            cancelPlacementKey,
            stepSent,
            status,
            cancellationToken);

    private void EnsureFocus(
        RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }
}
