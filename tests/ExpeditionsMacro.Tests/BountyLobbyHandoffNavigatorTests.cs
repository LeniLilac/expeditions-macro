using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class BountyLobbyHandoffNavigatorTests
{
    [Fact]
    public async Task ChallengeSelectorHandoff_ClosesPlayThenUsesVerifiedLobbyDoor()
    {
        BountyHandoffAutomation automation = new();
        DateTimeOffset now =
            new(2026, 7, 30, 20, 53, 56, TimeSpan.Zero);
        BountyLobbyHandoffNavigator navigator = new(
            automation,
            (duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                now += duration;
                return Task.CompletedTask;
            },
            () => now);

        await navigator.EnsureAsync(
            automation.Window,
            new HandoffDetector(automation),
            CancellationToken.None);

        Assert.True(automation.IsLobby);
        Assert.Equal(
            [
                (62, 588),
                (62, 588),
                (270, 35),
                (345, 328),
            ],
            automation.Clicks);
    }

    [Fact]
    public async Task ExistingLobby_SendsNoInput()
    {
        BountyHandoffAutomation automation = new(
            HandoffState.Lobby);
        BountyLobbyHandoffNavigator navigator = new(
            automation,
            static (duration, token) =>
                Task.CompletedTask,
            static () =>
                new DateTimeOffset(
                    2026,
                    7,
                    30,
                    20,
                    53,
                    56,
                    TimeSpan.Zero));

        await navigator.EnsureAsync(
            automation.Window,
            new HandoffDetector(automation),
            CancellationToken.None);

        Assert.Empty(automation.Clicks);
    }

    private enum HandoffState
    {
        Selector,
        Party,
        Match,
        Confirmation,
        Lobby,
    }

    private sealed class BountyHandoffAutomation :
        IRobloxAutomation
    {
        private readonly IReadOnlyDictionary<
            HandoffState,
            ImageFrame> _frames;
        private HandoffState _state;

        public BountyHandoffAutomation(
            HandoffState state =
                HandoffState.Selector)
        {
            _state = state;
            _frames = new Dictionary<
                HandoffState,
                ImageFrame>
            {
                [HandoffState.Selector] =
                    ImageCodec.Load(
                        Path.Combine(
                            TestPaths
                                .ChallengeDatasets,
                            "GameModeSelector",
                            "GameModeSelector_05.png")),
                [HandoffState.Party] =
                    ImageCodec.Load(
                        Path.Combine(
                            TestPaths
                                .ChallengeDatasets,
                            "PostMatchPreview",
                            "PostMatchPreview_01.png")),
                [HandoffState.Match] =
                    ImageCodec.Load(
                        Path.Combine(
                            TestPaths
                                .NavigationVariantDatasets,
                            "MatchLobbyDoor_NoVoiceChat.png")),
                [HandoffState.Confirmation] =
                    ImageCodec.Load(
                        Path.Combine(
                            TestPaths
                                .NavigationVariantDatasets,
                            "LobbyExitConfirmation.png")),
                [HandoffState.Lobby] =
                    ImageCodec.Load(
                        Path.Combine(
                            TestPaths
                                .NavigationVariantDatasets,
                            "ChatClosed.png")),
            };
        }

        public RobloxWindow Window { get; } =
            new((nint)46, "Roblox");

        public bool IsLobby =>
            _state == HandoffState.Lobby;

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
            _frames[_state].Clone();

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
            _state = (_state, x, y) switch
            {
                (HandoffState.Selector, 62, 588) =>
                    HandoffState.Party,
                (HandoffState.Party, 62, 588) =>
                    HandoffState.Match,
                (HandoffState.Match, 270, 35) =>
                    HandoffState.Confirmation,
                (HandoffState.Confirmation, 345, 328) =>
                    HandoffState.Lobby,
                _ => throw new InvalidOperationException(
                    $"Unexpected click ({x}, {y}) from {_state}."),
            };
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
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class HandoffDetector :
        IDetectorPack
    {
        private readonly BountyHandoffAutomation
            _automation;

        public HandoffDetector(
            BountyHandoffAutomation automation)
        {
            _automation = automation;
        }

        public DetectorPackManifest Manifest { get; } =
            new()
            {
                PackId = "handoff-test",
                Version = "1",
                GameId = "anime-expeditions",
                ModeId = "test",
                MinimumAppVersion = "1",
                ClientWidth = 808,
                ClientHeight = 611,
                States = [],
                MapSelections = [],
                DifficultySelections = [],
                NodeHuePrototypes =
                    new Dictionary<string, double>(),
                NodeHueRegion =
                    new ScreenRegion(0, 0, 1, 1),
                EmptyHotbarReferenceFile = "",
                ExtraActions =
                    new Dictionary<string, int[]>(),
                Files = [],
                BuiltAt =
                    new DateTimeOffset(
                        2026,
                        7,
                        30,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),
            };

        public IReadOnlyDictionary<string, double>
            ScoreStates(
            ImageFrame clientImage) =>
            new Dictionary<string, double>();

        public string? Classify(
            IReadOnlyDictionary<string, double>
                scores) =>
            null;

        public string? RecoveryState(
            ImageFrame clientImage) =>
            _automation.IsLobby ? "lobby" : null;

        public string? CurrentNodeType(
            ImageFrame clientImage) =>
            null;

        public int? SelectedMap(
            ImageFrame clientImage) =>
            null;

        public int? SelectedDifficulty(
            ImageFrame clientImage) =>
            null;

        public IReadOnlyList<int>
            RemainingUnitKeys(
            ImageFrame clientImage,
            IReadOnlySet<int> unitKeys) =>
            [];

        public (int X, int Y) ActionFor(
            string state,
            ImageFrame? clientImage = null) =>
            throw new NotSupportedException();
    }
}
