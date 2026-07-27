using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Tests;

internal sealed class CameraPoseTestAutomation(
    ImageFrame screenCapture) : IRobloxAutomation
{
    private readonly RobloxWindow _window =
        new((nint)42, "Roblox");
    private ClientBounds _client =
        new(300, 200, 1000, 700);

    public (int Width, int Height)? ResizeRequest
    {
        get;
        private set;
    }

    public List<(int X, int Y)> Drags { get; } = [];

    public List<bool> DragShiftLockStates { get; } = [];

    public int MoveToCenterCount { get; private set; }

    public List<int> ShiftLockKeys { get; } = [];

    public int ZoomTicks { get; private set; }

    public Exception? DragFailure { get; init; }

    public bool ShiftLockState { get; private set; }

    public RobloxWindow? FindWindow(
        string titleFragment = "Roblox") =>
        _window;

    public RobloxWindow? ForegroundWindow() =>
        _window;

    public ClientBounds GetClientBounds(
        RobloxWindow window) =>
        _client;

    public WindowBounds GetWindowBounds(
        RobloxWindow window) =>
        new(40, 50, 1100, 800);

    public bool Focus(
        RobloxWindow window) =>
        true;

    public Task ResizeClientAsync(
        RobloxWindow window,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        ResizeRequest = (width, height);
        _client = new ClientBounds(
            300,
            200,
            width,
            height);
        return Task.CompletedTask;
    }

    public void RestoreWindowBounds(
        RobloxWindow window,
        WindowBounds bounds)
    {
    }

    public ImageFrame CaptureScreen(
        ScreenRegion region) =>
        screenCapture.Clone();

    public ImageFrame CaptureClient(
        RobloxWindow window) =>
        screenCapture.Clone();

    public Task MoveCursorToClientCenterAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        MoveToCenterCount++;
        return Task.CompletedTask;
    }

    public Task ParkCursorAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ClickClientAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DragClientAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ScrollClientAsync(
        RobloxWindow window,
        int notches,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DragCameraAsync(
        RobloxWindow window,
        int deltaX,
        int deltaY,
        int chunkPixels,
        CancellationToken cancellationToken)
    {
        Drags.Add((deltaX, deltaY));
        DragShiftLockStates.Add(
            ShiftLockState);
        if (DragFailure is not null)
        {
            throw DragFailure;
        }
        return Task.CompletedTask;
    }

    public Task ZoomOutFullyAsync(
        RobloxWindow window,
        int ticks,
        CancellationToken cancellationToken)
    {
        ZoomTicks = ticks;
        return Task.CompletedTask;
    }

    public Task TapShiftLockKeyAsync(
        RobloxWindow window,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        ShiftLockKeys.Add(virtualKey);
        ShiftLockState = !ShiftLockState;
        return Task.CompletedTask;
    }

    public Task TapLetterKeyAsync(
        RobloxWindow window,
        char key,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task TapUnitKeyAsync(
        RobloxWindow window,
        int unitKey,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
