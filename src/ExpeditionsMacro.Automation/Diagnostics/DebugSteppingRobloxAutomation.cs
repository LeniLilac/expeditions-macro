using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Diagnostics;

public sealed class DebugSteppingRobloxAutomation : IRobloxAutomation, IDisposable
{
    private readonly IRobloxAutomation _inner;
    private readonly DebugCheckpointController _checkpoints;

    public DebugSteppingRobloxAutomation(
        IRobloxAutomation inner,
        DebugCheckpointController checkpoints)
    {
        _inner = inner;
        _checkpoints = checkpoints;
    }

    public RobloxWindow? FindWindow(string titleFragment = "Roblox") =>
        _inner.FindWindow(titleFragment);

    public RobloxWindow? ForegroundWindow() => _inner.ForegroundWindow();

    public ClientBounds GetClientBounds(RobloxWindow window) =>
        _inner.GetClientBounds(window);

    public WindowBounds GetWindowBounds(RobloxWindow window) =>
        _inner.GetWindowBounds(window);

    public bool Focus(RobloxWindow window) => _inner.Focus(window);

    public Task ResizeClientAsync(
        RobloxWindow window,
        int width,
        int height,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Resize Roblox",
            $"Resize the Roblox client to {width} × {height}.",
            () => _inner.ResizeClientAsync(
                window,
                width,
                height,
                cancellationToken),
            cancellationToken);

    public void RestoreWindowBounds(
        RobloxWindow window,
        WindowBounds bounds)
    {
        _checkpoints.BeforeActionAsync(
                "Restore Roblox window",
                "Restore the saved Roblox window bounds.",
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        _inner.RestoreWindowBounds(window, bounds);
    }

    public ImageFrame CaptureScreen(ScreenRegion region)
    {
        ImageFrame frame = _inner.CaptureScreen(region);
        _checkpoints.RecordFrame(frame);
        return frame;
    }

    public ImageFrame CaptureClient(RobloxWindow window)
    {
        ImageFrame frame = _inner.CaptureClient(window);
        _checkpoints.RecordFrame(frame);
        return frame;
    }

    public Task MoveCursorToClientCenterAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Center cursor",
            "Move the cursor to the center of Roblox.",
            () => _inner.MoveCursorToClientCenterAsync(
                window,
                cancellationToken),
            cancellationToken);

    public Task ParkCursorAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Park cursor",
            "Move the cursor away from interactive Roblox controls.",
            () => _inner.ParkCursorAsync(window, cancellationToken),
            cancellationToken);

    public Task ClickClientAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Click Roblox",
            $"Click client coordinate ({x}, {y}).",
            () => _inner.ClickClientAsync(
                window,
                x,
                y,
                cancellationToken),
            cancellationToken);

    public Task DragClientAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Drag Roblox control",
            $"Drag from ({startX}, {startY}) to ({endX}, {endY}).",
            () => _inner.DragClientAsync(
                window,
                startX,
                startY,
                endX,
                endY,
                cancellationToken),
            cancellationToken);

    public Task ScrollClientAsync(
        RobloxWindow window,
        int notches,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Scroll Roblox",
            $"Scroll {notches} notch(es) over the client.",
            () => _inner.ScrollClientAsync(
                window,
                notches,
                cancellationToken),
            cancellationToken);

    public Task DragCameraAsync(
        RobloxWindow window,
        int deltaX,
        int deltaY,
        int chunkPixels,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Drag camera",
            $"Move the camera by ({deltaX}, {deltaY}) pixels.",
            () => _inner.DragCameraAsync(
                window,
                deltaX,
                deltaY,
                chunkPixels,
                cancellationToken),
            cancellationToken);

    public Task PulseCameraYawAsync(
        RobloxWindow window,
        CameraYawDirection direction,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Pulse camera yaw",
            $"Hold {direction} for {holdMilliseconds} ms.",
            () => _inner.PulseCameraYawAsync(
                window,
                direction,
                holdMilliseconds,
                cancellationToken),
            cancellationToken);

    public Task CaptureCameraYawSweepAsync(
        RobloxWindow window,
        CameraYawDirection direction,
        TimeSpan maximumDuration,
        int maximumSamples,
        int sampleIntervalMilliseconds,
        Func<CameraYawSweepSample, bool> observe,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Capture camera sweep",
            $"Sweep {direction} for up to {maximumDuration.TotalSeconds:0.#} seconds.",
            () => _inner.CaptureCameraYawSweepAsync(
                window,
                direction,
                maximumDuration,
                maximumSamples,
                sampleIntervalMilliseconds,
                sample =>
                {
                    _checkpoints.RecordFrame(sample.Frame);
                    return observe(sample);
                },
                cancellationToken),
            cancellationToken);

    public Task CaptureCameraFineYawSweepAsync(
        RobloxWindow window,
        int radiusPixels,
        int sampleStridePixels,
        Action<CameraFineYawSweepSample> observe,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Capture fine yaw sweep",
            $"Sweep ±{radiusPixels} pixels in {sampleStridePixels}-pixel samples.",
            () => _inner.CaptureCameraFineYawSweepAsync(
                window,
                radiusPixels,
                sampleStridePixels,
                sample =>
                {
                    _checkpoints.RecordFrame(sample.Frame);
                    observe(sample);
                },
                cancellationToken),
            cancellationToken);

    public Task ZoomOutFullyAsync(
        RobloxWindow window,
        int ticks,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Zoom camera out",
            $"Send {ticks} camera zoom-out ticks.",
            () => _inner.ZoomOutFullyAsync(
                window,
                ticks,
                cancellationToken),
            cancellationToken);

    public Task TapShiftLockKeyAsync(
        RobloxWindow window,
        int virtualKey,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "Toggle Shift Lock",
            $"Press virtual key 0x{virtualKey:X2}.",
            () => _inner.TapShiftLockKeyAsync(
                window,
                virtualKey,
                cancellationToken),
            cancellationToken);

    public Task TapLetterKeyAsync(
        RobloxWindow window,
        char key,
        CancellationToken cancellationToken) =>
        TraceAsync(
            $"Press {char.ToUpperInvariant(key)}",
            $"Send the {char.ToUpperInvariant(key)} key to Roblox.",
            () => _inner.TapLetterKeyAsync(
                window,
                key,
                cancellationToken),
            cancellationToken);

    public Task TapUnitKeyAsync(
        RobloxWindow window,
        int unitKey,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            $"Press unit {unitKey}",
            $"Hold unit key {unitKey} for {holdMilliseconds} ms.",
            () => _inner.TapUnitKeyAsync(
                window,
                unitKey,
                holdMilliseconds,
                cancellationToken),
            cancellationToken);

    public void Dispose()
    {
        if (_inner is IDisposable disposable) disposable.Dispose();
    }

    private async Task TraceAsync(
        string action,
        string detail,
        Func<Task> run,
        CancellationToken cancellationToken)
    {
        await _checkpoints.BeforeActionAsync(
            action,
            detail,
            cancellationToken).ConfigureAwait(false);
        await run().ConfigureAwait(false);
    }
}
