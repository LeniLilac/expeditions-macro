using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Diagnostics;

public sealed class DeepDebugRobloxAutomation : IRobloxAutomation, IDisposable
{
    private readonly IRobloxAutomation _inner;
    private readonly DeepDebugSessionService _debug;
    private readonly DeepDebugActionFrameRecorder
        _actionFrames;

    public DeepDebugRobloxAutomation(IRobloxAutomation inner, DeepDebugSessionService debug)
    {
        _inner = inner;
        _debug = debug;
        _actionFrames =
            new DeepDebugActionFrameRecorder(
                inner,
                debug);
    }

    public RobloxWindow? FindWindow(string titleFragment = "Roblox")
    {
        RobloxWindow? result = _inner.FindWindow(titleFragment);
        _debug.RecordEvent("window", "find", new
        {
            TitleFragment = titleFragment,
            Found = result is not null,
            Window = result is null ? null : WindowData(result.Value),
        });
        return result;
    }

    public RobloxWindow? ForegroundWindow()
    {
        RobloxWindow? result = _inner.ForegroundWindow();
        _debug.RecordEvent("window", "foreground", new
        {
            Found = result is not null,
            Window = result is null ? null : WindowData(result.Value),
        });
        return result;
    }

    public ClientBounds GetClientBounds(RobloxWindow window)
    {
        ClientBounds result = _inner.GetClientBounds(window);
        _debug.RecordEvent("window", "client_bounds", new { Window = WindowData(window), Bounds = result });
        return result;
    }

    public WindowBounds GetWindowBounds(RobloxWindow window)
    {
        WindowBounds result = _inner.GetWindowBounds(window);
        _debug.RecordEvent("window", "window_bounds", new { Window = WindowData(window), Bounds = result });
        return result;
    }

    public bool Focus(RobloxWindow window)
    {
        bool result = _inner.Focus(window);
        _debug.RecordEvent("window", "focus", new { Window = WindowData(window), Succeeded = result });
        return result;
    }

    public Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "window",
            "resize_client",
            new { Window = WindowData(window), Width = width, Height = height },
            () => _inner.ResizeClientAsync(window, width, height, cancellationToken));

    public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds)
    {
        _debug.RecordEvent("window", "restore_bounds_requested", new { Window = WindowData(window), Bounds = bounds });
        _actionFrames.Record(
            window,
            "restore_bounds",
            "before");
        try
        {
            _inner.RestoreWindowBounds(window, bounds);
            _debug.RecordEvent("window", "restore_bounds_completed", new { Window = WindowData(window), Bounds = bounds });
            _actionFrames.Record(
                window,
                "restore_bounds",
                "after");
        }
        catch (Exception error)
        {
            _debug.RecordEvent("window", "restore_bounds_failed", new { Error = error.ToString() });
            _actionFrames.Record(
                window,
                "restore_bounds",
                "failed");
            throw;
        }
    }

    public ImageFrame CaptureScreen(ScreenRegion region)
    {
        ImageFrame frame = _inner.CaptureScreen(region);
        if (_debug.IsActive)
        {
            _debug.RecordFrame(frame, "capture_screen", new
            {
                Region = region,
                CallSite = CaptureCallSite(),
            });
        }
        return frame;
    }

    public ImageFrame CaptureClient(RobloxWindow window)
    {
        ImageFrame frame = _inner.CaptureClient(window);
        if (_debug.IsActive)
        {
            _debug.RecordFrame(frame, "capture_client", new
            {
                Window = WindowData(window),
                CallSite = CaptureCallSite(),
            });
        }
        return frame;
    }

    public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "move_cursor_to_client_center",
            new { Window = WindowData(window) },
            () => _inner.MoveCursorToClientCenterAsync(window, cancellationToken));

    public Task MoveCursorToClientAsync(
        RobloxWindow window,
        int x,
        int y,
        int jitterCycles,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "move_cursor_to_client",
            new
            {
                Window = WindowData(window),
                X = x,
                Y = y,
                JitterCycles = jitterCycles,
            },
            () => _inner.MoveCursorToClientAsync(
                window,
                x,
                y,
                jitterCycles,
                cancellationToken));

    public Task MoveCursorBetweenClientPointsAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "move_cursor_between_client_points",
            new
            {
                Window = WindowData(window),
                StartX = startX,
                StartY = startY,
                EndX = endX,
                EndY = endY,
                DurationMilliseconds =
                    durationMilliseconds,
            },
            () => _inner.MoveCursorBetweenClientPointsAsync(
                window,
                startX,
                startY,
                endX,
                endY,
                durationMilliseconds,
                cancellationToken));

    public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "park_cursor",
            new { Window = WindowData(window) },
            () => _inner.ParkCursorAsync(window, cancellationToken));

    public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "click_client",
            new { Window = WindowData(window), X = x, Y = y },
            () => _inner.ClickClientAsync(window, x, y, cancellationToken));

    public Task ClickClientRetainingCursorAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "click_client_retaining_cursor",
            new { Window = WindowData(window), X = x, Y = y },
            () => _inner.ClickClientRetainingCursorAsync(
                window,
                x,
                y,
                cancellationToken));

    public Task ClickClientBurstRetainingCursorAsync(
        RobloxWindow window,
        int x,
        int y,
        int clickCount,
        int durationMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "click_client_burst_retaining_cursor",
            new
            {
                Window = WindowData(window),
                X = x,
                Y = y,
                ClickCount = clickCount,
                DurationMilliseconds =
                    durationMilliseconds,
            },
            () =>
                _inner
                    .ClickClientBurstRetainingCursorAsync(
                        window,
                        x,
                        y,
                        clickCount,
                        durationMilliseconds,
                        cancellationToken));

    public Task DragClientAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "drag_client",
            new { Window = WindowData(window), StartX = startX, StartY = startY, EndX = endX, EndY = endY },
            () => _inner.DragClientAsync(window, startX, startY, endX, endY, cancellationToken));

    public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "scroll_client",
            new { Window = WindowData(window), Notches = notches },
            () => _inner.ScrollClientAsync(window, notches, cancellationToken));

    public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "drag_camera",
            new { Window = WindowData(window), DeltaX = deltaX, DeltaY = deltaY, ChunkPixels = chunkPixels },
            () => _inner.DragCameraAsync(window, deltaX, deltaY, chunkPixels, cancellationToken));

    public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "zoom_out_fully",
            new { Window = WindowData(window), Ticks = ticks },
            () => _inner.ZoomOutFullyAsync(window, ticks, cancellationToken));

    public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "tap_shift_lock_key",
            new { Window = WindowData(window), VirtualKey = virtualKey, Key = KeyboardKey.GetDisplayName(virtualKey) },
            () => _inner.TapShiftLockKeyAsync(window, virtualKey, cancellationToken));

    public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "tap_letter_key",
            new { Window = WindowData(window), Key = key },
            () => _inner.TapLetterKeyAsync(window, key, cancellationToken));

    public Task TapKeyboardKeyAsync(
        RobloxWindow window,
        RobloxKeyboardKey key,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "tap_keyboard_key",
            new { Window = WindowData(window), Key = key.ToString() },
            () => _inner.TapKeyboardKeyAsync(
                window,
                key,
                cancellationToken));

    public Task HoldLetterKeyAsync(
        RobloxWindow window,
        char key,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "hold_letter_key",
            new
            {
                Window = WindowData(window),
                Key = key,
                HoldMilliseconds = holdMilliseconds,
            },
            () => _inner.HoldLetterKeyAsync(
                window,
                key,
                holdMilliseconds,
                cancellationToken));

    public Task HoldKeyAsync(
        RobloxWindow window,
        int virtualKey,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "hold_key",
            new
            {
                Window = WindowData(window),
                VirtualKey = virtualKey,
                Key = KeyboardKey.GetDisplayName(virtualKey),
                HoldMilliseconds = holdMilliseconds,
            },
            () => _inner.HoldKeyAsync(
                window,
                virtualKey,
                holdMilliseconds,
                cancellationToken));

    public async Task<TResult> RunWithKeyHeldAsync<TResult>(
        RobloxWindow window,
        int virtualKey,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        TResult result = default!;
        await TraceAsync(
                window,
                "automation",
                "run_with_key_held",
                new
                {
                    Window = WindowData(window),
                    VirtualKey = virtualKey,
                    Key = KeyboardKey.GetDisplayName(virtualKey),
                },
                async () =>
                {
                    result =
                        await _inner.RunWithKeyHeldAsync(
                                window,
                                virtualKey,
                                action,
                                cancellationToken)
                            .ConfigureAwait(false);
                })
            .ConfigureAwait(false);
        return result;
    }

    public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) =>
        TraceAsync(
            window,
            "automation",
            "tap_unit_key",
            new { Window = WindowData(window), UnitKey = unitKey, HoldMilliseconds = holdMilliseconds },
            () => _inner.TapUnitKeyAsync(window, unitKey, holdMilliseconds, cancellationToken));

    public void Dispose()
    {
        if (_inner is IDisposable disposable) disposable.Dispose();
    }

    private async Task TraceAsync(
        RobloxWindow window,
        string category,
        string action,
        object data,
        Func<Task> callback)
    {
        _debug.RecordEvent(category, $"{action}_requested", data);
        _actionFrames.Record(
            window,
            action,
            "before");
        try
        {
            await callback().ConfigureAwait(false);
            _debug.RecordEvent(category, $"{action}_completed", data);
            _actionFrames.Record(
                window,
                action,
                "after");
        }
        catch (OperationCanceledException)
        {
            _debug.RecordEvent(category, $"{action}_canceled", data);
            _actionFrames.Record(
                window,
                action,
                "canceled");
            throw;
        }
        catch (Exception error)
        {
            _debug.RecordEvent(category, $"{action}_failed", new { Request = data, Error = error.ToString() });
            _actionFrames.Record(
                window,
                action,
                "failed");
            throw;
        }
    }

    private static object WindowData(RobloxWindow window) => new
    {
        Handle = window.Handle.ToInt64(),
        window.Title,
        window.ProcessId,
        window.ProcessName,
    };

    private static string CaptureCallSite()
    {
        StackTrace trace = new(skipFrames: 1, fNeedFileInfo: false);
        foreach (StackFrame frame in trace.GetFrames())
        {
            System.Reflection.MethodBase? method = frame.GetMethod();
            Type? type = method?.DeclaringType;
            if (type is null || type == typeof(DeepDebugRobloxAutomation)) continue;
            if (type.Namespace?.StartsWith("ExpeditionsMacro", StringComparison.Ordinal) == true)
            {
                return $"{type.FullName}.{method!.Name}";
            }
        }
        return "unknown";
    }
}
