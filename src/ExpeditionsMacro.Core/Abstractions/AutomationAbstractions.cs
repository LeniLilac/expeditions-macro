using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Core.Abstractions;

public readonly record struct RobloxWindow(
    nint Handle,
    string Title,
    int ProcessId = 0,
    string ProcessName = "")
{
    public string ProcessDescription => ProcessId > 0 && !string.IsNullOrWhiteSpace(ProcessName)
        ? $"{ProcessName}.exe, PID {ProcessId}"
        : "process unavailable";
}

public enum RobloxKeyboardKey
{
    Backspace,
    Enter,
    Backslash,
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    Period,
    LeftArrow,
    RightArrow,
    DownArrow,
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

    Task MoveCursorToClientAsync(
        RobloxWindow window,
        int x,
        int y,
        int jitterCycles,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support acknowledged client cursor movement.");

    Task MoveCursorBetweenClientPointsAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support timed client cursor movement.");

    Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken);

    Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken);

    Task ClickClientRetainingCursorAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support cursor-retaining client clicks.");

    async Task ClickClientBurstRetainingCursorAsync(
        RobloxWindow window,
        int x,
        int y,
        int clickCount,
        int durationMilliseconds,
        CancellationToken cancellationToken)
    {
        if (clickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clickCount));
        }
        if (durationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMilliseconds));
        }

        for (int click = 0; click < clickCount; click++)
        {
            await ClickClientRetainingCursorAsync(
                    window,
                    x,
                    y,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    Task DragClientAsync(
        RobloxWindow window,
        int startX,
        int startY,
        int endX,
        int endY,
        CancellationToken cancellationToken);

    Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken);

    Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken);

    Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken);

    Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken);

    Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken);

    Task TapKeyboardKeyAsync(
        RobloxWindow window,
        RobloxKeyboardKey key,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support general keyboard input.");

    Task HoldLetterKeyAsync(
        RobloxWindow window,
        char key,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support held letter input.");

    Task HoldKeyAsync(
        RobloxWindow window,
        int virtualKey,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support held keyboard input.");

    Task<TResult> RunWithKeyHeldAsync<TResult>(
        RobloxWindow window,
        int virtualKey,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This automation backend does not support scoped held keyboard input.");

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

public interface IRobloxProcessController
{
    Task CloseAsync(RobloxWindow? window, CancellationToken cancellationToken);

    Task LaunchAsync(Uri launchUri, CancellationToken cancellationToken);
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

    public TimeSpan? MatchRuntime { get; init; }

    public required int Victories { get; init; }

    public required int Defeats { get; init; }

    public required int MapNumber { get; init; }

    public required int Difficulty { get; init; }

    public required string Detail { get; init; }

    public string MacroName { get; init; } = "Expeditions Macro";

    public string Route { get; init; } = string.Empty;

    public string AttachmentPrefix { get; init; } = "expeditions";

    public ImageFrame? Screenshot { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string AppVersion { get; init; } = ProductVersion.Current;
}
