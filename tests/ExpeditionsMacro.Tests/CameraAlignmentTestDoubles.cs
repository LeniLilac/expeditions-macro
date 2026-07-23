using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

internal sealed class FakeAutomation(ImageFrame screenCapture) : IRobloxAutomation
{
    private readonly RobloxWindow _window = new((nint)42, "Roblox");
    private ClientBounds _client = new(300, 200, 1000, 700);
    private int _unobservedYawDelta;

    public (int Width, int Height)? ResizeRequest { get; private set; }
    public WindowBounds? RestoredBounds { get; private set; }
    public List<ScreenRegion> CapturedRegions { get; } = [];
    public List<(int X, int Y)> Drags { get; } = [];
    public List<bool> DragShiftLockStates { get; } = [];
    public List<CameraYawDirection> ArrowPulses { get; } = [];
    public int MoveToCenterCount { get; private set; }
    public List<int> ShiftLockKeys { get; } = [];
    public int ZoomTicks { get; private set; }
    public Exception? CaptureFailure { get; init; }
    public Exception? DragFailure { get; init; }
    public bool CorruptOnYawPulseAfterMouseMovement { get; init; }
    public int RapidYawBatchOvershootDivisor { get; init; }
    public ImageFrame? CorruptedFrame { get; init; }
    public Func<int, int, ImageFrame>? CaptureAtYaw { get; init; }
    public Func<int, int, bool, ImageFrame>? CaptureAtCameraState { get; init; }
    public int FullYawSteps { get; init; } = 12;
    public int YawStep { get; set; }
    public int MouseOffset { get; private set; }
    public bool ShiftLockState { get; private set; }
    public bool CameraCorrupted { get; private set; }

    public RobloxWindow? FindWindow(string titleFragment = "Roblox") => _window;
    public RobloxWindow? ForegroundWindow() => _window;
    public ClientBounds GetClientBounds(RobloxWindow window) => _client;
    public WindowBounds GetWindowBounds(RobloxWindow window) => new(40, 50, 1100, 800);
    public bool Focus(RobloxWindow window) => true;
    public Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
    {
        ResizeRequest = (width, height);
        _client = new ClientBounds(300, 200, width, height);
        return Task.CompletedTask;
    }
    public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds) => RestoredBounds = bounds;
    public ImageFrame CaptureScreen(ScreenRegion region)
    {
        CapturedRegions.Add(region);
        if (CaptureFailure is not null) throw CaptureFailure;
        return screenCapture.Clone();
    }
    public ImageFrame CaptureClient(RobloxWindow window)
    {
        if (CaptureFailure is not null) throw CaptureFailure;
        ApplyRapidYawBatchOvershoot();
        if (CameraCorrupted && CorruptedFrame is not null) return CorruptedFrame.Clone();
        return (CaptureAtCameraState?.Invoke(YawStep, MouseOffset, ShiftLockState)
            ?? CaptureAtYaw?.Invoke(YawStep, MouseOffset)
            ?? screenCapture).Clone();
    }
    public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken)
    {
        MoveToCenterCount++;
        return Task.CompletedTask;
    }
    public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DragClientAsync(RobloxWindow window, int startX, int startY, int endX, int endY, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken)
    {
        Drags.Add((deltaX, deltaY));
        DragShiftLockStates.Add(ShiftLockState);
        if (DragFailure is not null) throw DragFailure;
        MouseOffset += deltaX;
        return Task.CompletedTask;
    }
    public Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken)
    {
        ArrowPulses.Add(direction);
        if (CorruptOnYawPulseAfterMouseMovement && MouseOffset != 0) CameraCorrupted = true;
        int delta = direction == CameraYawDirection.Right ? 1 : -1;
        YawStep = ((YawStep + delta) % FullYawSteps + FullYawSteps) % FullYawSteps;
        _unobservedYawDelta += delta;
        return Task.CompletedTask;
    }
    public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken)
    {
        ZoomTicks = ticks;
        return Task.CompletedTask;
    }
    public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken)
    {
        ShiftLockKeys.Add(virtualKey);
        ShiftLockState = !ShiftLockState;
        return Task.CompletedTask;
    }
    public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;

    private void ApplyRapidYawBatchOvershoot()
    {
        if (RapidYawBatchOvershootDivisor > 0)
        {
            int extra = Math.Abs(_unobservedYawDelta) / RapidYawBatchOvershootDivisor;
            if (extra > 0)
            {
                int direction = Math.Sign(_unobservedYawDelta);
                YawStep = ((YawStep + direction * extra) % FullYawSteps + FullYawSteps) % FullYawSteps;
            }
        }
        _unobservedYawDelta = 0;
    }
}

internal sealed class NullCameraRepository : ICameraModelRepository
{
    public Task<IReadOnlyList<CameraModelManifest>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CameraModelManifest>>([]);

    public Task<CameraModel?> LoadAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<CameraModel?>(null);

    public Task SaveAsync(CameraModel model, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
