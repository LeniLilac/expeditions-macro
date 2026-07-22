using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Diagnostics;

public sealed class DeepDebugRobloxAutomation : IRobloxAutomation, IDisposable
{
    private readonly IRobloxAutomation _inner;
    private readonly DeepDebugSessionService _debug;

    public DeepDebugRobloxAutomation(IRobloxAutomation inner, DeepDebugSessionService debug)
    {
        _inner = inner;
        _debug = debug;
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
            "window",
            "resize_client",
            new { Window = WindowData(window), Width = width, Height = height },
            () => _inner.ResizeClientAsync(window, width, height, cancellationToken));

    public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds)
    {
        _debug.RecordEvent("window", "restore_bounds_requested", new { Window = WindowData(window), Bounds = bounds });
        try
        {
            _inner.RestoreWindowBounds(window, bounds);
            _debug.RecordEvent("window", "restore_bounds_completed", new { Window = WindowData(window), Bounds = bounds });
        }
        catch (Exception error)
        {
            _debug.RecordEvent("window", "restore_bounds_failed", new { Error = error.ToString() });
            throw;
        }
    }

    public ImageFrame CaptureScreen(ScreenRegion region)
    {
        ImageFrame frame = _inner.CaptureScreen(region);
        _debug.RecordFrame(frame, "capture_screen", new
        {
            Region = region,
            CallSite = CaptureCallSite(),
        });
        return frame;
    }

    public ImageFrame CaptureClient(RobloxWindow window)
    {
        ImageFrame frame = _inner.CaptureClient(window);
        _debug.RecordFrame(frame, "capture_client", new
        {
            Window = WindowData(window),
            CallSite = CaptureCallSite(),
        });
        return frame;
    }

    public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "move_cursor_to_client_center",
            new { Window = WindowData(window) },
            () => _inner.MoveCursorToClientCenterAsync(window, cancellationToken));

    public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "park_cursor",
            new { Window = WindowData(window) },
            () => _inner.ParkCursorAsync(window, cancellationToken));

    public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "click_client",
            new { Window = WindowData(window), X = x, Y = y },
            () => _inner.ClickClientAsync(window, x, y, cancellationToken));

    public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "scroll_client",
            new { Window = WindowData(window), Notches = notches },
            () => _inner.ScrollClientAsync(window, notches, cancellationToken));

    public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "drag_camera",
            new { Window = WindowData(window), DeltaX = deltaX, DeltaY = deltaY, ChunkPixels = chunkPixels },
            () => _inner.DragCameraAsync(window, deltaX, deltaY, chunkPixels, cancellationToken));

    public Task PulseCameraYawAsync(
        RobloxWindow window,
        CameraYawDirection direction,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "pulse_camera_yaw",
            new { Window = WindowData(window), Direction = direction, HoldMilliseconds = holdMilliseconds },
            () => _inner.PulseCameraYawAsync(window, direction, holdMilliseconds, cancellationToken));

    public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "zoom_out_fully",
            new { Window = WindowData(window), Ticks = ticks },
            () => _inner.ZoomOutFullyAsync(window, ticks, cancellationToken));

    public Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "tap_left_control",
            new { Window = WindowData(window) },
            () => _inner.TapLeftControlAsync(window, cancellationToken));

    public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "tap_letter_key",
            new { Window = WindowData(window), Key = key },
            () => _inner.TapLetterKeyAsync(window, key, cancellationToken));

    public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) =>
        TraceAsync(
            "automation",
            "tap_unit_key",
            new { Window = WindowData(window), UnitKey = unitKey, HoldMilliseconds = holdMilliseconds },
            () => _inner.TapUnitKeyAsync(window, unitKey, holdMilliseconds, cancellationToken));

    public void Dispose()
    {
        if (_inner is IDisposable disposable) disposable.Dispose();
    }

    private async Task TraceAsync(string category, string action, object data, Func<Task> callback)
    {
        _debug.RecordEvent(category, $"{action}_requested", data);
        try
        {
            await callback().ConfigureAwait(false);
            _debug.RecordEvent(category, $"{action}_completed", data);
        }
        catch (OperationCanceledException)
        {
            _debug.RecordEvent(category, $"{action}_canceled", data);
            throw;
        }
        catch (Exception error)
        {
            _debug.RecordEvent(category, $"{action}_failed", new { Request = data, Error = error.ToString() });
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
