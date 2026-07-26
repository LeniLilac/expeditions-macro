using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementServiceTests
{
    [Fact]
    public async Task Record_UsesStandardClientSizeAndKeepsIt()
    {
        string root = Path.Combine(Path.GetTempPath(), $"expeditions-placement-{Guid.NewGuid():N}");
        try
        {
            FakeAutomation automation = new();
            FakeCaptureService capture = new(automation);
            PlacementService service = new(automation, capture, new PlacementModelRepository(new AppPaths(root)));

            PlacementModel model = await service.RecordAsync("Canonical placement", 900, useRecordedDelays: false);

            Assert.Equal((808, 611), automation.ResizeRequest);
            Assert.Equal((808, 611), capture.ClientSizeAtCapture);
            Assert.Equal(808, model.ClientWidth);
            Assert.Equal(611, model.ClientHeight);
            Assert.Null(automation.RestoredBounds);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Playback_NormalizesSelectionThenPrimesBeforePlacement()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"expeditions-placement-{Guid.NewGuid():N}");
        try
        {
            FakeAutomation automation = new();
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)));
            PlacementModel model = new()
            {
                Id = "playback",
                Name = "Playback",
                ClientWidth = 808,
                ClientHeight = 611,
                Steps =
                [
                    new PlacementStep
                    {
                        UnitKey = 4,
                        X = 320,
                        Y = 280,
                        DelayAfterMilliseconds = 0,
                    },
                ],
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await service.PlayAsync(
                model,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                [
                    "hover:320,280:2",
                    "key:4",
                    "letter:Z",
                    "click:320,280",
                    "key:4",
                    "hover:320,280:2",
                    "click:320,280",
                ],
                automation.InputActions);
            Assert.NotNull(automation.TargetPrimedAt);
            Assert.NotNull(automation.ClickedAt);
            Assert.True(
                automation.ClickedAt -
                automation.TargetPrimedAt >=
                TimeSpan.FromMilliseconds(180));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeCaptureService(FakeAutomation automation) : IPlacementCaptureService
    {
        public (int Width, int Height)? ClientSizeAtCapture { get; private set; }

        public Task<(int ClientWidth, int ClientHeight, IReadOnlyList<PlacementCapture> Captures)> RecordAsync(
            RobloxWindow window,
            Action<PlacementCapture>? captured,
            Action<string>? status,
            CancellationToken cancellationToken)
        {
            ClientBounds client = automation.GetClientBounds(window);
            ClientSizeAtCapture = (client.Width, client.Height);
            PlacementCapture capture = new(1, 320, 280, 0, 100);
            captured?.Invoke(capture);
            return Task.FromResult<(int, int, IReadOnlyList<PlacementCapture>)>((client.Width, client.Height, [capture]));
        }
    }

    private sealed class FakeAutomation : IRobloxAutomation
    {
        private readonly RobloxWindow _window = new((nint)42, "Roblox");
        private ClientBounds _client = new(100, 120, 800, 599);

        public (int Width, int Height)? ResizeRequest { get; private set; }

        public WindowBounds? RestoredBounds { get; private set; }

        public List<string> InputActions { get; } = [];

        public DateTimeOffset? TargetPrimedAt { get; private set; }

        public DateTimeOffset? ClickedAt { get; private set; }

        public RobloxWindow? FindWindow(string titleFragment = "Roblox") => _window;

        public RobloxWindow? ForegroundWindow() => _window;

        public ClientBounds GetClientBounds(RobloxWindow window) => _client;

        public WindowBounds GetWindowBounds(RobloxWindow window) => new(40, 50, 920, 720);

        public bool Focus(RobloxWindow window) => true;

        public Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
        {
            ResizeRequest = (width, height);
            _client = _client with { Width = width, Height = height };
            return Task.CompletedTask;
        }

        public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds) => RestoredBounds = bounds;

        public ImageFrame CaptureScreen(ScreenRegion region) => throw new NotSupportedException();

        public ImageFrame CaptureClient(RobloxWindow window) => throw new NotSupportedException();

        public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MoveCursorToClientAsync(
            RobloxWindow window,
            int x,
            int y,
            int jitterCycles,
            CancellationToken cancellationToken)
        {
            InputActions.Add(
                $"hover:{x},{y}:{jitterCycles}");
            TargetPrimedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        public Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            InputActions.Add($"click:{x},{y}");
            ClickedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PulseCameraYawAsync(RobloxWindow window, CameraYawDirection direction, int holdMilliseconds, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task TapLetterKeyAsync(
            RobloxWindow window,
            char key,
            CancellationToken cancellationToken)
        {
            InputActions.Add(
                $"letter:{char.ToUpperInvariant(key)}");
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken)
        {
            InputActions.Add($"key:{unitKey}");
            return Task.CompletedTask;
        }
    }
}
