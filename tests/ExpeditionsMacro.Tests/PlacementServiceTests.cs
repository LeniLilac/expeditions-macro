using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

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
    public async Task Playback_NormalizesSelectionThenDismissesFinalPanelAtIdlePoint()
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
                    new AppPaths(root)),
                () => 'T');
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
                        TargetingPriority =
                            UnitTargetingPriority.Strongest,
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
                    "key:4",
                    "letter:Z",
                    "key:4",
                    "key:4",
                    "key:4",
                    "move:270,280->320,280:200",
                    "click-retain:320,280",
                    "letter:T",
                    "letter:T",
                    "letter:T",
                    "letter:Y",
                    "park",
                    "click:783,586",
                    "park",
                ],
                automation.InputActions);
            Assert.NotNull(automation.TargetPrimedAt);
            Assert.Equal(
                2,
                automation.ClickTimes.Count);
            Assert.Equal(
                2,
                automation.InputActions.Count(
                    action => action == "park"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Playback_RetriesOnlyClickWhenSelectedPanelIsAbsent()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame negative = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanelHoverNegative_01.png"));
            ImageFrame positive = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanel_01.png"));
            FakeAutomation automation = new(
                Enumerable.Repeat(negative, 8)
                    .Concat([positive, positive])
                    .ToArray());
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T');
            PlacementModel model = ModelWithSteps(
                new PlacementStep
                {
                    UnitKey = 2,
                    X = 320,
                    Y = 280,
                    DelayAfterMilliseconds = 0,
                });

            await service.PlayAsync(
                model,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                4,
                automation.InputActions.Count(
                    action => action == "key:2"));
            Assert.Equal(
                2,
                automation.InputActions.Count(
                    action =>
                        action == "click-retain:320,280"));
            Assert.Equal(
                2,
                automation.InputActions.Count(
                    action =>
                        action ==
                        "move:270,280->320,280:200"));
            Assert.Single(
                automation.InputActions,
                action => action == "letter:Z");
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_AcceptsPanelAtTimeoutBoundaryWithoutAnotherClick()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame negative = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanelHoverNegative_01.png"));
            ImageFrame positive = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanel_01.png"));
            FakeAutomation automation = new(
                Enumerable.Repeat(negative, 7)
                    .Concat([positive, positive])
                    .ToArray());
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T');

            await service.PlayAsync(
                ModelWithSteps(
                    new PlacementStep
                    {
                        UnitKey = 2,
                        X = 320,
                        Y = 280,
                        DelayAfterMilliseconds = 0,
                    }),
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                1,
                automation.InputActions.Count(
                    action =>
                        action == "click-retain:320,280"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData(UnitTargetingPriority.First, 0)]
    [InlineData(UnitTargetingPriority.None, 8)]
    public async Task Playback_AppliesConfiguredTargetingTapCount(
        UnitTargetingPriority priority,
        int expectedTaps)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            FakeAutomation automation = new();
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T');

            await service.PlayAsync(
                ModelWithSteps(
                    new PlacementStep
                    {
                        UnitKey = 1,
                        X = 320,
                        Y = 280,
                        DelayAfterMilliseconds = 0,
                        TargetingPriority = priority,
                    }),
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                expectedTaps,
                automation.InputActions.Count(
                    action => action == "letter:T"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Playback_AppliesConfiguredAutoUpgrade(
        bool autoUpgrade,
        int expectedTaps)
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            FakeAutomation automation = new();
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T',
                () => 'Y');

            await service.PlayAsync(
                ModelWithSteps(
                    new PlacementStep
                    {
                        UnitKey = 1,
                        X = 320,
                        Y = 280,
                        DelayAfterMilliseconds = 0,
                        AutoUpgrade = autoUpgrade,
                    }),
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                expectedTaps,
                automation.InputActions.Count(
                    action => action == "letter:Y"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_DismissesSelectionAfterEveryPlacedUnit()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            FakeAutomation automation = new();
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T');
            PlacementModel model = ModelWithSteps(
                new PlacementStep
                {
                    UnitKey = 1,
                    X = 300,
                    Y = 280,
                    DelayAfterMilliseconds = 0,
                },
                new PlacementStep
                {
                    UnitKey = 2,
                    X = 340,
                    Y = 280,
                    DelayAfterMilliseconds = 0,
                });

            await service.PlayAsync(
                model,
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                4,
                automation.InputActions.Count(
                    action => action == "park"));
            Assert.Equal(
                2,
                automation.InputActions.Count(
                    action =>
                        action == "click:783,586"));
            int park =
                automation.InputActions.IndexOf(
                    "park");
            Assert.True(
                park >
                automation.InputActions.IndexOf(
                    "click-retain:300,280"));
            Assert.True(
                park <
                automation.InputActions.IndexOf(
                    "move:290,280->340,280:200"));
            int firstDismiss =
                automation.InputActions.IndexOf(
                    "click:783,586");
            Assert.True(
                firstDismiss <
                automation.InputActions.IndexOf(
                    "move:290,280->340,280:200"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_ClicksIdlePointUntilFinalSelectionCloses()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            FakeAutomation automation = new()
            {
                IdleClicksBeforeDismissal = 3,
            };
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T');

            await service.PlayAsync(
                ModelWithSteps(
                    new PlacementStep
                    {
                        UnitKey = 3,
                        X = 320,
                        Y = 280,
                        DelayAfterMilliseconds = 0,
                    }),
                useDefaultInterval: true,
                defaultIntervalMilliseconds: 0,
                keyHoldMilliseconds: 0,
                afterKeyMilliseconds: 0);

            Assert.Equal(
                3,
                automation.InputActions.Count(
                    action =>
                        action == "click:783,586"));
            Assert.Equal(
                "park",
                automation.InputActions[^1]);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Playback_StopsAfterEightUnconfirmedClicks()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            ImageFrame negative = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanelHoverNegative_01.png"));
            FakeAutomation automation = new(negative);
            PlacementService service = new(
                automation,
                new FakeCaptureService(automation),
                new PlacementModelRepository(
                    new AppPaths(root)),
                () => 'T');
            PlacementModel model = ModelWithSteps(
                new PlacementStep
                {
                    UnitKey = 6,
                    X = 320,
                    Y = 280,
                    DelayAfterMilliseconds = 0,
                });

            RobloxUiUnavailableException error =
                await Assert.ThrowsAsync<
                    RobloxUiUnavailableException>(
                    () => service.PlayAsync(
                        model,
                        useDefaultInterval: true,
                        defaultIntervalMilliseconds: 0,
                        keyHoldMilliseconds: 0,
                        afterKeyMilliseconds: 0));

            Assert.Contains(
                "after 8 click attempts",
                error.Message,
                StringComparison.Ordinal);
            Assert.Equal(
                4,
                automation.InputActions.Count(
                    action => action == "key:6"));
            Assert.Equal(
                8,
                automation.InputActions.Count(
                    action =>
                        action == "click-retain:320,280"));
            Assert.DoesNotContain(
                "letter:T",
                automation.InputActions);
            Assert.DoesNotContain(
                "park",
                automation.InputActions);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PlacementModel ModelWithSteps(
        params PlacementStep[] steps) =>
        new()
        {
            Id = "playback",
            Name = "Playback",
            ClientWidth = 808,
            ClientHeight = 611,
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };

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
        private readonly IReadOnlyList<ImageFrame> _captures;
        private readonly ImageFrame _dismissedCapture;
        private int _captureIndex;
        private int _idleClickCount;
        private bool _panelDismissed;

        public FakeAutomation(
            params ImageFrame[] captures)
        {
            _captures = captures.Length == 0
                ? [ImageCodec.Load(
                    Path.Combine(
                        TestPaths.StageDatasets,
                        "SelectedUnitPanel_01.png"))]
                : captures;
            _dismissedCapture = ImageCodec.Load(
                Path.Combine(
                    TestPaths.StageDatasets,
                    "SelectedUnitPanelHoverNegative_01.png"));
        }

        public (int Width, int Height)? ResizeRequest { get; private set; }

        public WindowBounds? RestoredBounds { get; private set; }

        public List<string> InputActions { get; } = [];

        public DateTimeOffset? TargetPrimedAt { get; private set; }

        public DateTimeOffset? ClickedAt { get; private set; }

        public List<DateTimeOffset> ClickTimes { get; } = [];

        public int IdleClicksBeforeDismissal { get; init; } = 1;

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

        public ImageFrame CaptureClient(RobloxWindow window)
        {
            if (_panelDismissed)
            {
                return _dismissedCapture;
            }
            ImageFrame frame = _captures[
                Math.Min(
                    _captureIndex,
                    _captures.Count - 1)];
            _captureIndex++;
            return frame;
        }

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

        public Task MoveCursorBetweenClientPointsAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            int durationMilliseconds,
            CancellationToken cancellationToken)
        {
            InputActions.Add(
                $"move:{startX},{startY}->{endX},{endY}:{durationMilliseconds}");
            TargetPrimedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        public Task ParkCursorAsync(
            RobloxWindow window,
            CancellationToken cancellationToken)
        {
            InputActions.Add("park");
            return Task.CompletedTask;
        }

        public Task ClickClientAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            InputActions.Add($"click:{x},{y}");
            ClickedAt = DateTimeOffset.UtcNow;
            ClickTimes.Add(ClickedAt.Value);
            if (x == 783 &&
                y == 586)
            {
                _idleClickCount++;
                _panelDismissed =
                    _idleClickCount >=
                    IdleClicksBeforeDismissal;
            }
            return Task.CompletedTask;
        }

        public Task ClickClientRetainingCursorAsync(
            RobloxWindow window,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            InputActions.Add($"click-retain:{x},{y}");
            ClickTimes.Add(DateTimeOffset.UtcNow);
            _idleClickCount = 0;
            _panelDismissed = false;
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
