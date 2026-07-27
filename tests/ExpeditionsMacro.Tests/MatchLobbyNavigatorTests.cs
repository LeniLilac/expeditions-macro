using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class MatchLobbyNavigatorTests
{
    [Theory]
    [InlineData("MatchLobbyDoor_NoVoiceChat.png", 270)]
    [InlineData("MatchLobbyDoor_VoiceChat.png", 314)]
    public async Task ReturnClicksStableDoorThenDetectedConfirmation(
        string doorFile,
        int expectedDoorX)
    {
        ImageFrame confirmation = ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                "LobbyExitConfirmation.png"));
        ImageFrame door = ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                doorFile));
        LobbyReturnAutomation automation =
            new(door, confirmation);
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
        IDetectorPack detector =
            new AlwaysLobbyDetector();

        await navigator.ReturnAsync(
            automation.Window,
            detector,
            CancellationToken.None);

        Assert.Equal(
            [
                (expectedDoorX, 35),
                (345, 328),
            ],
            automation.Clicks);
        Assert.Empty(automation.Keys);
    }

    [Fact]
    public async Task IgnoredDoorClick_RedetectsBeforeRetrying()
    {
        LobbyReturnAutomation automation = CreateAutomation(
            "MatchLobbyDoor_NoVoiceChat.png");
        automation.DoorAcceptAfter = 2;
        DateTimeOffset now =
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        MatchLobbyNavigator navigator = CreateNavigator(
            automation,
            () => now,
            duration => now += duration);

        await navigator.ReturnAsync(
            automation.Window,
            new AlwaysLobbyDetector(),
            CancellationToken.None);

        Assert.Equal(
            [
                (270, 35),
                (270, 35),
                (345, 328),
            ],
            automation.Clicks);
        Assert.Empty(automation.Keys);
    }

    [Fact]
    public async Task MovingBetweenTopBarLayouts_SendsNoInput()
    {
        LobbyReturnAutomation automation = CreateAutomation(
            "MatchLobbyDoor_NoVoiceChat.png");
        automation.AlternateDoorFrame = ImageCodec.Load(
            Path.Combine(
                TestPaths.NavigationVariantDatasets,
                "MatchLobbyDoor_VoiceChat.png"));
        DateTimeOffset now =
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        MatchLobbyNavigator navigator = CreateNavigator(
            automation,
            () => now,
            duration => now += duration);

        await Assert.ThrowsAsync<RobloxUiUnavailableException>(
            () => navigator.ReturnAsync(
                automation.Window,
                new AlwaysLobbyDetector(),
                CancellationToken.None));

        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
    }

    [Fact]
    public async Task CancellationBeforeStableDoor_SendsNoInput()
    {
        LobbyReturnAutomation automation = CreateAutomation(
            "MatchLobbyDoor_NoVoiceChat.png");
        using CancellationTokenSource cancellation = new();
        DateTimeOffset now =
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        MatchLobbyNavigator navigator = new(
            automation,
            () => now,
            (duration, token) =>
            {
                now += duration;
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => navigator.ReturnAsync(
                automation.Window,
                new AlwaysLobbyDetector(),
                cancellation.Token));

        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
    }

    private static LobbyReturnAutomation CreateAutomation(
        string doorFile) =>
        new(
            ImageCodec.Load(
                Path.Combine(
                    TestPaths.NavigationVariantDatasets,
                    doorFile)),
            ImageCodec.Load(
                Path.Combine(
                    TestPaths.NavigationVariantDatasets,
                    "LobbyExitConfirmation.png")));

    private static MatchLobbyNavigator CreateNavigator(
        LobbyReturnAutomation automation,
        Func<DateTimeOffset> utcNow,
        Action<TimeSpan> advance) =>
        new(
            automation,
            utcNow,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                advance(duration);
                return Task.CompletedTask;
            });

    private sealed class LobbyReturnAutomation :
        IRobloxAutomation
    {
        private readonly ImageFrame _door;
        private readonly ImageFrame _confirmation;
        private bool _confirmationOpen;
        private bool _lobby;
        private int _doorClicks;
        private int _doorCaptureCount;

        public LobbyReturnAutomation(
            ImageFrame door,
            ImageFrame confirmation)
        {
            _door = door;
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

        public int DoorAcceptAfter { get; set; } = 1;

        public ImageFrame? AlternateDoorFrame { get; set; }

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
            RobloxWindow window)
        {
            if (_confirmationOpen)
            {
                return _confirmation.Clone();
            }
            if (_lobby)
            {
                return LobbyFrame.Clone();
            }
            if (AlternateDoorFrame is not null &&
                _doorCaptureCount++ % 2 == 1)
            {
                return AlternateDoorFrame.Clone();
            }
            return _door.Clone();
        }

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
            if (y == 35)
            {
                _doorClicks++;
                if (_doorClicks >= DoorAcceptAfter)
                {
                    _confirmationOpen = true;
                }
            }
            else if ((x, y) == (345, 328) &&
                     _confirmationOpen)
            {
                _confirmationOpen = false;
                _lobby = true;
            }
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
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class AlwaysLobbyDetector :
        IDetectorPack
    {
        public DetectorPackManifest Manifest =>
            null!;

        public IReadOnlyDictionary<string, double>
            ScoreStates(
            ImageFrame clientImage) =>
            new Dictionary<string, double>();

        public string? Classify(
            IReadOnlyDictionary<string, double> scores) =>
            null;

        public string? RecoveryState(
            ImageFrame clientImage) =>
            "lobby";

        public string? CurrentNodeType(
            ImageFrame clientImage) =>
            null;

        public int? SelectedMap(
            ImageFrame clientImage) =>
            null;

        public int? SelectedDifficulty(
            ImageFrame clientImage) =>
            null;

        public IReadOnlyList<int> RemainingUnitKeys(
            ImageFrame clientImage,
            IReadOnlySet<int> unitKeys) =>
            [];

        public (int X, int Y) ActionFor(
            string state,
            ImageFrame? clientImage = null) =>
            throw new NotSupportedException();
    }
}
