using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class MatchLobbyNavigatorTests
{
    [Fact]
    public async Task ReturnUsesAccessibilityToOpenThenMouseToConfirm()
    {
        ImageFrame confirmation = ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                "LobbyExitConfirmation.png"));
        LobbyReturnAutomation automation =
            new(confirmation);
        DateTimeOffset now =
            new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
        MatchLobbyNavigator navigator = new(
            automation,
            () => now,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += duration;
                return Task.CompletedTask;
            });
        IDetectorPack detector = new LobbyDetector(
            automation.LobbyFrame,
            alwaysLobby: true);

        await navigator.ReturnAsync(
            automation.Window,
            detector,
            CancellationToken.None);

        Assert.Equal(
            [
                RobloxKeyboardKey.Backslash,
                RobloxKeyboardKey.RightArrow,
                RobloxKeyboardKey.RightArrow,
                RobloxKeyboardKey.Enter,
                RobloxKeyboardKey.Backslash,
            ],
            automation.Keys);
        Assert.Equal(
            [(345, 328)],
            automation.Clicks);
        Assert.DoesNotContain(
            RobloxKeyboardKey.DownArrow,
            automation.Keys);
    }

    private sealed class LobbyReturnAutomation :
        IRobloxAutomation
    {
        private readonly ImageFrame _confirmation;
        private bool _confirmationOpen;

        public LobbyReturnAutomation(
            ImageFrame confirmation)
        {
            _confirmation = confirmation;
            LobbyFrame = new ImageFrame(
                808,
                611,
                PixelFormat.Rgb24,
                new byte[808 * 611 * 3]);
        }

        public RobloxWindow Window { get; } =
            new((nint)42, "Roblox");

        public ImageFrame LobbyFrame { get; }

        public List<RobloxKeyboardKey> Keys { get; } =
            [];

        public List<(int X, int Y)> Clicks { get; } =
            [];

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            Window;

        public RobloxWindow? ForegroundWindow() =>
            Window;

        public ClientBounds GetClientBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public WindowBounds GetWindowBounds(
            RobloxWindow window) =>
            new(0, 0, 824, 650);

        public bool Focus(RobloxWindow window) =>
            true;

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds)
        {
        }

        public ImageFrame CaptureScreen(
            ScreenRegion region) =>
            CaptureClient(Window);

        public ImageFrame CaptureClient(
            RobloxWindow window) =>
            (_confirmationOpen
                ? _confirmation
                : LobbyFrame).Clone();

        public Task MoveCursorToClientCenterAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            Clicks.Add((x, y));
            _confirmationOpen = false;
            return Task.CompletedTask;
        }

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
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PulseCameraYawAsync(
            RobloxWindow window,
            CameraYawDirection direction,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ZoomOutFullyAsync(
            RobloxWindow window,
            int ticks,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapShiftLockKeyAsync(
            RobloxWindow window,
            int virtualKey,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapLetterKeyAsync(
            RobloxWindow window,
            char key,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapKeyboardKeyAsync(
            RobloxWindow window,
            RobloxKeyboardKey key,
            CancellationToken cancellationToken)
        {
            Keys.Add(key);
            if (key == RobloxKeyboardKey.Enter)
            {
                _confirmationOpen = true;
            }
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
