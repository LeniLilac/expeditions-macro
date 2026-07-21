using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Abstractions;

public readonly record struct RobloxWindow(nint Handle, string Title);

public enum CameraYawDirection
{
    Left = -1,
    Right = 1,
}

public interface IRobloxAutomation
{
    RobloxWindow? FindWindow(string titleFragment = "Roblox");

    RobloxWindow? ForegroundWindow();

    ClientBounds GetClientBounds(RobloxWindow window);

    WindowBounds GetWindowBounds(RobloxWindow window);

    bool Focus(RobloxWindow window);

    Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken);

    void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds);

    ImageFrame CaptureScreen(ScreenRegion region);

    ImageFrame CaptureClient(RobloxWindow window);

    Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken);

    Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken);

    Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken);

    Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken);

    Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken);

    Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken);

    Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken);

    Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken);

    Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken);
}

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? Pressed;

    event EventHandler? BindingChanged;

    bool IsRegistered { get; }

    int VirtualKey { get; }

    string DisplayName { get; }

    void Configure(int virtualKey);

    void Rebind(int virtualKey);

    void Start();

    void Stop();
}

public interface IPlacementCaptureService
{
    Task<(int ClientWidth, int ClientHeight, IReadOnlyList<PlacementCapture> Captures)> RecordAsync(
        RobloxWindow window,
        Action<PlacementCapture>? captured,
        Action<string>? status,
        CancellationToken cancellationToken);
}

public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}

public interface IDiscordNotifier
{
    Task SendAsync(DiscordNotification notification, CancellationToken cancellationToken);
}

public sealed record DiscordNotification
{
    public required string WebhookUrl { get; init; }

    public required string Event { get; init; }

    public required TimeSpan Runtime { get; init; }

    public required int Victories { get; init; }

    public required int Defeats { get; init; }

    public required int MapNumber { get; init; }

    public required int Difficulty { get; init; }

    public required string Detail { get; init; }

    public string MacroName { get; init; } = "Expeditions Macro";

    public string Route { get; init; } = string.Empty;

    public string AttachmentPrefix { get; init; } = "expeditions";

    public ImageFrame? Screenshot { get; init; }
}
