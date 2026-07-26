using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

public sealed class CameraPosePreparationService
{
    private const double PoseClampSimilarity = 0.975;
    private const int MaximumPoseClampProbes = 4;

    private readonly IRobloxAutomation _automation;
    private readonly Func<int> _shiftLockVirtualKey;

    public CameraPosePreparationService(
        IRobloxAutomation automation,
        Func<int>? shiftLockVirtualKey = null)
    {
        _automation = automation;
        _shiftLockVirtualKey = shiftLockVirtualKey ??
            (() => AppSettings.DefaultShiftLockVirtualKey);
    }

    public async Task PrepareWithoutYawAsync(
        RobloxWindow? existingWindow = null,
        int zoomTicks = 30,
        int pitchDragPixels = 1800,
        int settleMilliseconds = 200,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (zoomTicks is < 5 or > 80)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomTicks));
        }
        if (pitchDragPixels is < 300 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(pitchDragPixels));
        }
        if (settleMilliseconds is < 25 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settleMilliseconds));
        }

        RobloxWindow window = existingWindow ??
            RequireWindow();
        Focus(window);
        await EnsureCanonicalClientAsync(
            window,
            progress,
            cancellationToken).ConfigureAwait(false);
        await ClampZoomAsync(
            window,
            zoomTicks,
            settleMilliseconds,
            regions: null,
            "Fast no align",
            20,
            progress,
            cancellationToken).ConfigureAwait(false);

        int? shiftLockKey = null;
        try
        {
            shiftLockKey = await EnableShiftLockAsync(
                window,
                "Fast no align",
                55,
                progress,
                cancellationToken).ConfigureAwait(false);
            await ClampPitchAsync(
                window,
                pitchDragPixels,
                settleMilliseconds,
                regions: null,
                "Fast no align",
                75,
                progress,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new MacroProgress(
                "Fast no align",
                100,
                "Camera is fully zoomed out and looking straight down; yaw was preserved."));
        }
        finally
        {
            if (shiftLockKey is int key)
            {
                await DisableShiftLockAsync(
                    window,
                    key).ConfigureAwait(false);
            }
        }
    }

    public async Task PreparePitchOnlyAsync(
        RobloxWindow existingWindow,
        int pitchDragPixels = 1800,
        int settleMilliseconds = 200,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (pitchDragPixels is < 300 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pitchDragPixels));
        }
        if (settleMilliseconds is < 25 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settleMilliseconds));
        }

        Focus(existingWindow);
        int? shiftLockKey = null;
        try
        {
            shiftLockKey = await EnableShiftLockAsync(
                existingWindow,
                "Settings preparation",
                25,
                progress,
                cancellationToken).ConfigureAwait(false);
            await ClampPitchAsync(
                existingWindow,
                pitchDragPixels,
                settleMilliseconds,
                regions: null,
                "Settings preparation",
                75,
                progress,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new MacroProgress(
                "Settings preparation",
                100,
                "Camera is looking straight down; zoom and yaw were preserved."));
        }
        finally
        {
            if (shiftLockKey is int key)
            {
                await DisableShiftLockAsync(
                    existingWindow,
                    key).ConfigureAwait(false);
            }
        }
    }

    internal async Task ClampZoomAsync(
        RobloxWindow window,
        int zoomTicks,
        int settleMilliseconds,
        IReadOnlyList<ScreenRegion>? regions,
        string operation,
        int percent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int batch = Math.Clamp(zoomTicks, 5, 80);
        progress?.Report(new MacroProgress(
            operation,
            percent,
            "Zooming out until the rendered view stops changing."));
        Focus(window);
        await _automation.ZoomOutFullyAsync(
            window,
            batch,
            cancellationToken).ConfigureAwait(false);
        await Task.Delay(
            Math.Max(75, settleMilliseconds),
            cancellationToken).ConfigureAwait(false);
        ImageFrame previous =
            CapturePoseThumbnail(window, regions);
        double similarity = 0;
        for (int probe = 1;
             probe <= MaximumPoseClampProbes;
             probe++)
        {
            await _automation.ZoomOutFullyAsync(
                window,
                batch,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                Math.Max(75, settleMilliseconds),
                cancellationToken).ConfigureAwait(false);
            ImageFrame current =
                CapturePoseThumbnail(window, regions);
            similarity = CameraRegisteredScorer.Score(
                previous,
                current,
                maximumTranslation: 2).Score;
            if (similarity >= PoseClampSimilarity)
            {
                progress?.Report(new MacroProgress(
                    operation,
                    percent,
                    $"Zoom clamp verified at {similarity:P0} frame agreement.",
                    Confidence: similarity));
                return;
            }
            previous = current;
        }
        progress?.Report(new MacroProgress(
            operation,
            percent,
            $"Zoom received the maximum extra zoom passes; the scene remained animated ({similarity:P0}).",
            Confidence: similarity));
    }

    internal async Task ClampPitchAsync(
        RobloxWindow window,
        int pitchDragPixels,
        int settleMilliseconds,
        IReadOnlyList<ScreenRegion>? regions,
        string operation,
        int percent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int initial = Math.Clamp(
            pitchDragPixels,
            300,
            5000);
        int probePixels = Math.Clamp(
            initial / 3,
            450,
            900);
        progress?.Report(new MacroProgress(
            operation,
            percent,
            "Dragging downward until the top-down pitch stops changing."));
        Focus(window);
        await _automation.DragCameraAsync(
            window,
            0,
            initial,
            90,
            cancellationToken).ConfigureAwait(false);
        await Task.Delay(
            Math.Max(75, settleMilliseconds),
            cancellationToken).ConfigureAwait(false);
        ImageFrame previous =
            CapturePoseThumbnail(window, regions);
        double similarity = 0;
        for (int probe = 1;
             probe <= MaximumPoseClampProbes;
             probe++)
        {
            await _automation.DragCameraAsync(
                window,
                0,
                probePixels,
                90,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                Math.Max(75, settleMilliseconds),
                cancellationToken).ConfigureAwait(false);
            ImageFrame current =
                CapturePoseThumbnail(window, regions);
            similarity = CameraRegisteredScorer.Score(
                previous,
                current,
                maximumTranslation: 2).Score;
            if (similarity >= PoseClampSimilarity)
            {
                progress?.Report(new MacroProgress(
                    operation,
                    percent,
                    $"Top-down pitch clamp verified at {similarity:P0} frame agreement.",
                    Confidence: similarity));
                return;
            }
            previous = current;
        }
        progress?.Report(new MacroProgress(
            operation,
            percent,
            $"Pitch received the maximum extra downward drags; the scene remained animated ({similarity:P0}).",
            Confidence: similarity));
    }

    internal async Task<int> EnableShiftLockAsync(
        RobloxWindow window,
        string operation,
        int percent,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int virtualKey = _shiftLockVirtualKey();
        if (!KeyboardKey.IsSupportedShiftLockKey(virtualKey))
        {
            throw new InvalidDataException(
                "The configured Shift Lock key is not supported.");
        }
        progress?.Report(new MacroProgress(
            operation,
            percent,
            $"Enabling shift lock with {KeyboardKey.GetDisplayName(virtualKey)} for stable camera movement."));
        await _automation.MoveCursorToClientCenterAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        await Task.Delay(
            120,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await _automation.TapShiftLockKeyAsync(
            window,
            virtualKey,
            CancellationToken.None).ConfigureAwait(false);
        await Task.Delay(
            250,
            CancellationToken.None).ConfigureAwait(false);
        return virtualKey;
    }

    internal async Task DisableShiftLockAsync(
        RobloxWindow window,
        int virtualKey)
    {
        Focus(window);
        await _automation.TapShiftLockKeyAsync(
            window,
            virtualKey,
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task EnsureCanonicalClientAsync(
        RobloxWindow window,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ClientBounds current =
            _automation.GetClientBounds(window);
        if (current.Width != RobloxClientProfile.Width ||
            current.Height != RobloxClientProfile.Height)
        {
            progress?.Report(new MacroProgress(
                "Fast no align",
                5,
                $"Resizing Roblox to {RobloxClientProfile.Width} × {RobloxClientProfile.Height}."));
            await _automation.ResizeClientAsync(
                window,
                RobloxClientProfile.Width,
                RobloxClientProfile.Height,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                250,
                cancellationToken).ConfigureAwait(false);
        }

        ClientBounds resized =
            _automation.GetClientBounds(window);
        if (resized.Width != RobloxClientProfile.Width ||
            resized.Height != RobloxClientProfile.Height)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox did not accept the standard {RobloxClientProfile.Width} × {RobloxClientProfile.Height} client size.");
        }
    }

    private ImageFrame CapturePoseThumbnail(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion>? regions)
    {
        ImageFrame frame =
            _automation.CaptureClient(window);
        return regions is null
            ? VisionScorer.PrepareGray(frame, 160, 101)
            : VisionScorer.MakeThumbnail(
                CameraRegionAnalyzer.BuildComposite(
                    frame,
                    regions),
                160);
    }

    private RobloxWindow RequireWindow() =>
        _automation.FindWindow() ??
        throw new RobloxSessionUnavailableException(
            "No supported Roblox player window was found.");

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus the Roblox window.");
        }
    }
}
