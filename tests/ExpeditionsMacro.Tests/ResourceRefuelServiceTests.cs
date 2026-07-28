using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Automation.Refuel;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class ResourceRefuelServiceTests
{
    [Fact]
    public async Task CurrentLobby_BothStations_UsesBlindRoutesAndOpensPlay()
    {
        RefuelAutomation automation = new();
        FakeRecovery recovery = new(automation);
        ResourceRefuelService service = CreateService(
            automation,
            recovery);

        ResourceRefuelResult result = await service.RunAsync(
            Request(
                ResourceRefuelStart.CurrentLobby,
                ResourceRefuelTarget.Both),
            new LobbyDetectorPack());

        Assert.Equal(
            ResourceRefuelTarget.Both,
            result.CompletedTargets);
        Assert.Equal(0, recovery.Restarts);
        Assert.Equal(
            [
                ('W', 110),
                ('A', 120),
                ('W', 130),
                ('W', 210),
                ('A', 220),
                ('W', 230),
                ('A', 240),
            ],
            automation.HeldKeys);
        Assert.Equal(2, automation.AreasPresses);
        Assert.Equal(2, automation.InteractionPresses);
        Assert.Equal(1, automation.PlayPresses);
        Assert.Equal(2, automation.MaxClicks);
        Assert.Equal(2, automation.ConfirmClicks);
        AssertNoCaptureDuringBlindRoutes(automation.Events);
    }

    [Fact]
    public async Task RestartStart_RestartsBeforeNavigating()
    {
        RefuelAutomation automation = new()
        {
            ClientBounds =
                new ClientBounds(0, 0, 1024, 768),
        };
        FakeRecovery recovery = new(automation);
        ResourceRefuelService service = CreateService(
            automation,
            recovery);

        await service.RunAsync(
            Request(
                ResourceRefuelStart.RestartPrivateServer,
                ResourceRefuelTarget.GoldMine) with
            {
                RestartTarget =
                    RobloxPrivateServerLaunchTarget.Parse(
                        "https://www.roblox.com/share?code=Test_Server&type=Server"),
                OpenPlayWhenComplete = false,
            },
            new LobbyDetectorPack());

        Assert.Equal(1, recovery.Restarts);
        Assert.Equal((808, 611), automation.ResizeRequest);
        Assert.Equal(1, automation.InteractionPresses);
        Assert.Equal(0, automation.PlayPresses);
    }

    [Fact]
    public async Task FailedInteraction_ReturnsToHubBeforeRetry()
    {
        RefuelAutomation automation = new()
        {
            FailedInteractionAttempts = 1,
        };
        FakeRecovery recovery = new(automation);
        ResourceRefuelService service = CreateService(
            automation,
            recovery);

        await service.RunAsync(
            Request(
                ResourceRefuelStart.CurrentLobby,
                ResourceRefuelTarget.GoldMine) with
            {
                OpenPlayWhenComplete = false,
            },
            new LobbyDetectorPack());

        Assert.Equal(2, automation.AreasPresses);
        Assert.Equal(2, automation.InteractionPresses);
        Assert.Equal(1, automation.MaxClicks);
        Assert.Equal(1, automation.ConfirmClicks);
    }

    [Fact]
    public async Task CancellationDuringHeldRoute_ReleasesOwnership()
    {
        RefuelAutomation automation = new()
        {
            CancelOnHeldKey = true,
        };
        FakeRecovery recovery = new(automation);
        ResourceRefuelService service = CreateService(
            automation,
            recovery);
        using CancellationTokenSource cancellation = new();
        automation.Cancellation = cancellation;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RunAsync(
                Request(
                    ResourceRefuelStart.CurrentLobby,
                    ResourceRefuelTarget.GoldMine),
                new LobbyDetectorPack(),
                cancellationToken: cancellation.Token));

        Assert.Single(automation.HeldKeys);
    }

    private static ResourceRefuelService CreateService(
        RefuelAutomation automation,
        FakeRecovery recovery) =>
        new(
            automation,
            recovery,
            static (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

    private static ResourceRefuelRequest Request(
        ResourceRefuelStart start,
        ResourceRefuelTarget targets) =>
        new()
        {
            Start = start,
            Targets = targets,
            AreasMenuKey = 'G',
            PlayMenuKey = 'P',
            Settings = new ResourceRefuelDebugSettings
            {
                GoldForward1Milliseconds = 110,
                GoldLeftMilliseconds = 120,
                GoldForward2Milliseconds = 130,
                DrillForward1Milliseconds = 210,
                DrillLeft1Milliseconds = 220,
                DrillForward2Milliseconds = 230,
                DrillLeft2Milliseconds = 240,
                RetryCount = 1,
            },
        };

    private static void AssertNoCaptureDuringBlindRoutes(
        IReadOnlyList<string> events)
    {
        int searchFrom = 0;
        for (int route = 0; route < 2; route++)
        {
            int hub = Find(events, "click:322,264", searchFrom);
            int interaction = Find(events, "key:E", hub + 1);
            Assert.DoesNotContain(
                "capture",
                events.Skip(hub + 1).Take(
                    interaction - hub - 1));
            searchFrom = interaction + 1;
        }
    }

    private static int Find(
        IReadOnlyList<string> values,
        string expected,
        int start)
    {
        for (int index = start; index < values.Count; index++)
        {
            if (values[index] == expected) return index;
        }
        throw new Xunit.Sdk.XunitException(
            $"Expected event '{expected}' after index {start}.");
    }

    private enum FakeScreen
    {
        Lobby,
        AreasMenu,
        AreasExpeditions,
        Hub,
        GoldMine,
        ResourceDrill,
        AddFuel,
        Play,
    }

    private sealed class RefuelAutomation : IRobloxAutomation
    {
        private readonly RobloxWindow _window =
            new(42, "Roblox", 84, "RobloxPlayerBeta");
        private readonly Dictionary<FakeScreen, ImageFrame> _frames =
            LoadFrames();
        private FakeScreen _screen = FakeScreen.Lobby;
        private int _routeKeys;
        private FakeScreen _stationBeforeDialog;

        public ClientBounds ClientBounds { get; set; } =
            new(0, 0, 808, 611);

        public (int Width, int Height)? ResizeRequest { get; private set; }

        public int FailedInteractionAttempts { get; init; }

        public bool CancelOnHeldKey { get; init; }

        public CancellationTokenSource? Cancellation { get; set; }

        public int AreasPresses { get; private set; }

        public int InteractionPresses { get; private set; }

        public int PlayPresses { get; private set; }

        public int MaxClicks { get; private set; }

        public int ConfirmClicks { get; private set; }

        public List<(char Key, int Milliseconds)> HeldKeys { get; } = [];

        public List<string> Events { get; } = [];

        public RobloxWindow? FindWindow(string titleFragment = "Roblox") =>
            _window;

        public RobloxWindow? ForegroundWindow() => _window;

        public ClientBounds GetClientBounds(RobloxWindow window) =>
            ClientBounds;

        public WindowBounds GetWindowBounds(RobloxWindow window) =>
            new(0, 0, ClientBounds.Width, ClientBounds.Height);

        public bool Focus(RobloxWindow window)
        {
            Events.Add("focus");
            return true;
        }

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken)
        {
            ResizeRequest = (width, height);
            ClientBounds = new ClientBounds(
                ClientBounds.X,
                ClientBounds.Y,
                width,
                height);
            Events.Add($"resize:{width},{height}");
            return Task.CompletedTask;
        }

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds)
        {
        }

        public ImageFrame CaptureScreen(ScreenRegion region) =>
            CaptureClient(_window);

        public ImageFrame CaptureClient(RobloxWindow window)
        {
            Events.Add("capture");
            return _frames[_screen].Clone();
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
            Events.Add($"click:{x},{y}");
            if (_screen == FakeScreen.AreasMenu &&
                (x, y) == (198, 304))
            {
                _screen = FakeScreen.AreasExpeditions;
            }
            else if (_screen == FakeScreen.AreasExpeditions &&
                     (x, y) == (322, 264))
            {
                _screen = FakeScreen.Hub;
                _routeKeys = 0;
            }
            else if (_screen is
                     FakeScreen.GoldMine or
                     FakeScreen.ResourceDrill)
            {
                _stationBeforeDialog = _screen;
                _screen = FakeScreen.AddFuel;
            }
            else if (_screen == FakeScreen.AddFuel &&
                     (x, y) == (516, 312))
            {
                MaxClicks++;
            }
            else if (_screen == FakeScreen.AddFuel &&
                     (x, y) == (337, 345))
            {
                ConfirmClicks++;
                _screen = _stationBeforeDialog;
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
            CancellationToken cancellationToken)
        {
            Events.Add($"key:{key}");
            if (key == 'G')
            {
                AreasPresses++;
                _screen = FakeScreen.AreasMenu;
            }
            else if (key == 'E')
            {
                InteractionPresses++;
                if (InteractionPresses > FailedInteractionAttempts)
                {
                    _screen = _routeKeys == 4
                        ? FakeScreen.ResourceDrill
                        : FakeScreen.GoldMine;
                }
            }
            else if (key == 'P')
            {
                PlayPresses++;
                _screen = FakeScreen.Play;
            }
            return Task.CompletedTask;
        }

        public Task HoldLetterKeyAsync(
            RobloxWindow window,
            char key,
            int holdMilliseconds,
            CancellationToken cancellationToken)
        {
            Events.Add($"hold:{key}:{holdMilliseconds}");
            HeldKeys.Add((key, holdMilliseconds));
            _routeKeys++;
            if (CancelOnHeldKey)
            {
                Cancellation?.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        }

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ReturnToLobby() => _screen = FakeScreen.Lobby;

        private static Dictionary<FakeScreen, ImageFrame> LoadFrames() =>
            new()
            {
                [FakeScreen.Lobby] = ImageCodec.Load(Path.Combine(
                    TestPaths.Datasets,
                    "Lobby_UI",
                    "Lobby_UI_001.png")),
                [FakeScreen.AreasMenu] = LoadRefuel("AreasMenu_01.png"),
                [FakeScreen.AreasExpeditions] =
                    LoadRefuel("AreasExpeditions_01.png"),
                [FakeScreen.Hub] = ImageCodec.Load(Path.Combine(
                    TestPaths.Datasets,
                    "Lobby_UI",
                    "Lobby_UI_001.png")),
                [FakeScreen.GoldMine] =
                    LoadRefuel("GoldMine_01.png"),
                [FakeScreen.ResourceDrill] =
                    LoadRefuel("ResourceDrill_01.png"),
                [FakeScreen.AddFuel] =
                    LoadRefuel("GoldMine_AddFuel_01.png"),
                [FakeScreen.Play] = ImageCodec.Load(Path.Combine(
                    TestPaths.Datasets,
                    "Play_UI",
                    "Play_UI_001.png")),
            };

        private static ImageFrame LoadRefuel(string file) =>
            ImageCodec.Load(
                Path.Combine(TestPaths.RefuelDatasets, file));
    }

    private sealed class FakeRecovery(
        RefuelAutomation automation)
        : IRobloxRuntimeRecoveryService
    {
        public int Restarts { get; private set; }

        public Task LaunchAsync(
            RobloxPrivateServerLaunchTarget target,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<RobloxWindow> RestartAsync(
            RobloxPrivateServerLaunchTarget target,
            IProgress<MacroProgress>? progress = null,
            Action<MacroEvent>? log = null,
            CancellationToken cancellationToken = default)
        {
            Restarts++;
            automation.ReturnToLobby();
            return Task.FromResult(
                automation.FindWindow()!.Value);
        }
    }

    private sealed class LobbyDetectorPack : IDetectorPack
    {
        public DetectorPackManifest Manifest =>
            throw new NotSupportedException();

        public IReadOnlyDictionary<string, double> ScoreStates(
            ImageFrame clientImage) =>
            new Dictionary<string, double>();

        public string? Classify(
            IReadOnlyDictionary<string, double> scores) =>
            null;

        public string? RecoveryState(ImageFrame clientImage) =>
            AreasScreenDetectorForTest(clientImage)
                ? "unknown"
                : "lobby";

        public string? CurrentNodeType(ImageFrame clientImage) =>
            null;

        public int? SelectedMap(ImageFrame clientImage) => null;

        public int? SelectedDifficulty(ImageFrame clientImage) =>
            null;

        public IReadOnlyList<int> RemainingUnitKeys(
            ImageFrame clientImage,
            IReadOnlySet<int> unitKeys) =>
            [];

        public (int X, int Y) ActionFor(
            string state,
            ImageFrame? clientImage = null) =>
            throw new NotSupportedException();

        private static bool AreasScreenDetectorForTest(
            ImageFrame image) =>
            ExpeditionsMacro.Vision.Refuel.AreasScreenDetector
                .Detect(image).State !=
            ExpeditionsMacro.Vision.Refuel.AreasScreenState.None;
    }
}
