using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Tests;

public sealed class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task DisabledNormalization_StillRequiresStableLobby()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby);
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(frames.Lobby),
                normalizeSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
        Assert.Empty(automation.Drags);
        Assert.Equal((808, 611), automation.ResizeRequest);
    }

    [Fact]
    public async Task EventThemeLobby_IsAcceptedAsCleanBeforeSettingsNormalization()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.EventThemeLobby);
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(frames.EventThemeLobby),
                normalizeSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
    }

    [Fact]
    public async Task NonLobbyStart_NormalizesUiScaleBeforeLobbyGate()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.NonLobby)
            {
                SettingsOpenFrame = frames.Scale080,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<RobloxUiUnavailableException>(
                () => service.RunAsync(
                    new LobbyDetector(frames.Lobby),
                    normalizeSettings: true,
                    progress: null,
                    log: null,
                    CancellationToken.None));

        Assert.Contains(
            "Start the macro",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            RobloxKeyboardKey.Digit1,
            automation.Keys);
        Assert.Equal(
            4,
            automation.Keys.Count(
                key =>
                    key == RobloxKeyboardKey.Backslash));
        Assert.DoesNotContain(
            automation.Clicks,
            point =>
                point ==
                (
                    GameSettingsScreenDetector
                        .SettingsButtonX,
                    GameSettingsScreenDetector
                        .SettingsButtonY));
        Assert.Empty(automation.Drags);
    }

    [Fact]
    public async Task UiScaleDebug_NonLobbyStartDoesNotRequireLobby()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.NonLobby)
            {
                SettingsOpenFrame = frames.Scale080,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunUiScaleAsync(
                new LobbyDetector(frames.Lobby),
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.Contains(
            RobloxKeyboardKey.Digit1,
            automation.Keys);
        Assert.Same(
            frames.NonLobby,
            automation.CurrentFrame);
    }

    [Fact]
    public async Task OpenSettings_DoesNotCountAsACleanLobby()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Gameplay);
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        await Assert.ThrowsAsync<RobloxUiUnavailableException>(
            () => service.RunAsync(
                new LobbyDetector(
                    frames.Lobby,
                    alwaysLobby: true),
                normalizeSettings: false,
                progress: null,
                log: null,
                CancellationToken.None));

        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
        Assert.Empty(automation.Drags);
    }

    [Fact]
    public async Task CanonicalProfile_CorrectsOnlyTheWrongToggle()
    {
        TestFrames frames = new();
        ImageFrame wrongGameplay =
            ReplaceToggle(
                frames.Gameplay,
                638,
                222,
                enabled: false);
        PreflightAutomation automation =
            new(frames, frames.Lobby)
            {
                SettingsOpenFrame = wrongGameplay,
                GameplayFrameAfterToggle = frames.Gameplay,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(frames.Lobby),
                normalizeSettings: true,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(1, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Single(
            automation.Clicks,
            point => point == (638, 222));
        Assert.Single(automation.Drags);
        Assert.Same(frames.Lobby, automation.CurrentFrame);
    }

    [Fact]
    public async Task NonCanonicalScale_UsesAccessibilityKeysThenReopensSettings()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby)
            {
                SettingsOpenFrame = frames.Scale080,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(frames.Lobby),
                normalizeSettings: true,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.Equal(
            8,
            automation.Keys.Count(
                key =>
                    key == RobloxKeyboardKey.Backslash));
        Assert.Contains(
            RobloxKeyboardKey.Digit1,
            automation.Keys);
        Assert.Equal(
            2,
            clock.Delays.Count(
                delay =>
                    delay == TimeSpan.FromSeconds(1)));
        Assert.True(
            clock.Delays.Count(
                delay =>
                    delay ==
                    TimeSpan.FromMilliseconds(500)) >= 28);
        SequenceAssertions.ContainsContiguous(
            automation.Keys,
            [
                RobloxKeyboardKey.Backslash,
                RobloxKeyboardKey.RightArrow,
                RobloxKeyboardKey.Enter,
                RobloxKeyboardKey.LeftArrow,
                .. Enumerable.Repeat(
                    RobloxKeyboardKey.DownArrow,
                    7),
                RobloxKeyboardKey.Enter,
                RobloxKeyboardKey.RightArrow,
                RobloxKeyboardKey.DownArrow,
                RobloxKeyboardKey.DownArrow,
                RobloxKeyboardKey.LeftArrow,
                RobloxKeyboardKey.Enter,
            ]);
        Assert.Same(frames.Lobby, automation.CurrentFrame);
    }

    [Fact]
    public async Task DeviceDependentScale_UsesRenderedFeedback()
    {
        TestFrames frames = new();
        double observed =
            GameSettingsScreenDetector
                .DetectPanel(frames.Scale120)
                .UiScale;
        string corrected =
            UiScaleFeedbackPolicy.Format(
                UiScaleFeedbackPolicy.Correct(
                    1,
                    observed));
        PreflightAutomation automation =
            new(frames, frames.NonLobby)
            {
                SettingsOpenFrame = frames.Scale120,
                ScaleApplyFrame = value =>
                    value == corrected
                        ? frames.Gameplay
                        : frames.Scale120,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunUiScaleAsync(
                new LobbyDetector(frames.Lobby),
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.Equal(
            ["1.00", corrected],
            automation.AppliedScaleValues);
        Assert.Contains(
            RobloxKeyboardKey.Period,
            automation.Keys);
        Assert.Same(
            frames.NonLobby,
            automation.CurrentFrame);
    }

    private static MacroStartupPreflightService CreateService(
        PreflightAutomation automation,
        TestClock clock) =>
        new(
            automation,
            () => clock.UtcNow,
            clock.DelayAsync);

    private static ImageFrame ReplaceToggle(
        ImageFrame source,
        int centerX,
        int centerY,
        bool enabled)
    {
        byte[] pixels = source.Pixels.ToArray();
        (byte Red, byte Green, byte Blue) color =
            enabled
                ? ((byte)30, (byte)170, (byte)25)
                : ((byte)190, (byte)30, (byte)30);
        for (int y = centerY - 8; y <= centerY + 8; y++)
        {
            for (int x = centerX - 8;
                 x <= centerX + 8;
                 x++)
            {
                int pixel = (y * source.Width + x) * 3;
                pixels[pixel] = color.Red;
                pixels[pixel + 1] = color.Green;
                pixels[pixel + 2] = color.Blue;
            }
        }
        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }

    private sealed class TestClock
    {
        public DateTimeOffset UtcNow { get; private set; } =
            DateTimeOffset.Parse(
                "2026-07-25T12:00:00Z");

        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(duration);
            UtcNow += duration;
            return Task.CompletedTask;
        }
    }

    private sealed class TestFrames
    {
        public ImageFrame Lobby { get; } =
            Load("LobbyClosed.png");
        public ImageFrame NonLobby { get; } =
            Load("LobbyClosed.png");
        public ImageFrame EventThemeLobby { get; } =
            Load("LobbyEventTheme.png");
        public ImageFrame Scale080 { get; } =
            Load("SettingsScale080.png");
        public ImageFrame Scale120 { get; } =
            Load("SettingsScale120.png");
        public ImageFrame Gameplay { get; } =
            Load("GameplayPage.png");
        public ImageFrame Graphics { get; } =
            Load("GraphicsPageCurrent.png");
        public ImageFrame UnitsTop { get; } =
            Load("UnitsTop.png");
        public ImageFrame UnitsBottom { get; } =
            Load("UnitsBottom.png");
        public ImageFrame Miscellaneous { get; } =
            Load("MiscellaneousPageCurrent.png");

        private static ImageFrame Load(string name) =>
            ImageCodec.Load(
                Path.Combine(
                    TestPaths.SettingsDatasets,
                    name));
    }

    private sealed class PreflightAutomation : IRobloxAutomation
    {
        private static readonly RobloxWindow Window =
            new((nint)42, "Roblox", 1234, "RobloxPlayerBeta");
        private readonly TestFrames _frames;
        private ClientBounds _client =
            new(0, 0, 808, 611);
        private bool _scaleInputReady;
        private bool _editingScale;
        private string _scaleInput = string.Empty;
        private bool _scaleNormalized;
        private bool _settingsOpen;
        private bool _accessibilityEnabled;
        private bool _navigationAtRoot;
        private bool _navigationAtSettingsButton;
        private bool _miscSelected;
        private int _miscDownCount;
        private int _scaleNavigationStep;
        private readonly ImageFrame _closedFrame;

        public PreflightAutomation(
            TestFrames frames,
            ImageFrame initialFrame)
        {
            _frames = frames;
            _closedFrame = initialFrame;
            CurrentFrame = initialFrame;
            SettingsOpenFrame = frames.Gameplay;
            GameplayFrameAfterToggle = frames.Gameplay;
        }

        public ImageFrame CurrentFrame { get; private set; }

        public ImageFrame SettingsOpenFrame { get; init; }

        public ImageFrame GameplayFrameAfterToggle { get; init; }

        public Func<string, ImageFrame>? ScaleApplyFrame
        {
            get;
            init;
        }

        public List<(int X, int Y)> Clicks { get; } = [];

        public List<RobloxKeyboardKey> Keys { get; } = [];

        public List<string> AppliedScaleValues { get; } = [];

        public List<(
            int StartX,
            int StartY,
            int EndX,
            int EndY)> Drags
        { get; } = [];

        public (int Width, int Height)? ResizeRequest { get; private set; }

        public RobloxWindow? FindWindow(
            string titleFragment = "Roblox") =>
            Window;

        public RobloxWindow? ForegroundWindow() => Window;

        public ClientBounds GetClientBounds(
            RobloxWindow window) => _client;

        public WindowBounds GetWindowBounds(
            RobloxWindow window) =>
            new(0, 0, 808, 611);

        public bool Focus(RobloxWindow window) => true;

        public Task ResizeClientAsync(
            RobloxWindow window,
            int width,
            int height,
            CancellationToken cancellationToken)
        {
            ResizeRequest = (width, height);
            _client = _client with
            {
                Width = width,
                Height = height,
            };
            return Task.CompletedTask;
        }

        public void RestoreWindowBounds(
            RobloxWindow window,
            WindowBounds bounds)
        {
        }

        public ImageFrame CaptureScreen(ScreenRegion region) =>
            throw new NotSupportedException();

        public ImageFrame CaptureClient(
            RobloxWindow window) =>
            CurrentFrame;

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
            if ((x, y) ==
                (
                    GameSettingsScreenDetector.SettingsButtonX,
                    GameSettingsScreenDetector.SettingsButtonY))
            {
                _settingsOpen = !_settingsOpen;
                CurrentFrame = _settingsOpen
                    ? _scaleNormalized
                        ? _frames.Gameplay
                        : SettingsOpenFrame
                    : _closedFrame;
                return Task.CompletedTask;
            }
            if ((x, y) ==
                GameSettingsScreenDetector.PageAction(
                    GameSettingsPage.Gameplay))
            {
                CurrentFrame = _frames.Gameplay;
                return Task.CompletedTask;
            }
            if ((x, y) ==
                GameSettingsScreenDetector.PageAction(
                    GameSettingsPage.Graphics))
            {
                CurrentFrame = _frames.Graphics;
                return Task.CompletedTask;
            }
            if ((x, y) ==
                GameSettingsScreenDetector.PageAction(
                    GameSettingsPage.Units))
            {
                CurrentFrame = _frames.UnitsTop;
                return Task.CompletedTask;
            }
            if ((x, y) ==
                GameSettingsScreenDetector.PageAction(
                    GameSettingsPage.Miscellaneous))
            {
                CurrentFrame = _frames.Miscellaneous;
                return Task.CompletedTask;
            }
            if ((x, y) == (638, 222))
            {
                CurrentFrame = GameplayFrameAfterToggle;
                return Task.CompletedTask;
            }
            throw new InvalidOperationException(
                $"Unexpected click at ({x},{y}).");
        }

        public Task DragClientAsync(
            RobloxWindow window,
            int startX,
            int startY,
            int endX,
            int endY,
            CancellationToken cancellationToken)
        {
            Drags.Add((startX, startY, endX, endY));
            CurrentFrame =
                endY >= 400
                    ? _frames.UnitsBottom
                    : _frames.UnitsTop;
            return Task.CompletedTask;
        }

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
            if (key == RobloxKeyboardKey.Backslash)
            {
                _accessibilityEnabled =
                    !_accessibilityEnabled;
                if (_accessibilityEnabled)
                {
                    _navigationAtRoot = true;
                    _navigationAtSettingsButton = false;
                    _miscDownCount = 0;
                    _scaleNavigationStep = 0;
                }
            }
            else if (_accessibilityEnabled &&
                     _navigationAtRoot &&
                     key == RobloxKeyboardKey.RightArrow)
            {
                _navigationAtRoot = false;
                _navigationAtSettingsButton = true;
            }
            else if (_accessibilityEnabled &&
                     _navigationAtSettingsButton &&
                     key == RobloxKeyboardKey.Enter)
            {
                _navigationAtSettingsButton = false;
                _settingsOpen = !_settingsOpen;
                _miscSelected = false;
                _scaleInputReady = false;
                CurrentFrame = _settingsOpen
                    ? _scaleNormalized
                        ? _frames.Gameplay
                        : SettingsOpenFrame
                    : _closedFrame;
            }
            else if (_accessibilityEnabled &&
                     _settingsOpen &&
                     !_miscSelected &&
                     key == RobloxKeyboardKey.DownArrow)
            {
                _miscDownCount++;
            }
            else if (_accessibilityEnabled &&
                     _settingsOpen &&
                     !_miscSelected &&
                     key == RobloxKeyboardKey.Enter &&
                     _miscDownCount >= 7)
            {
                _miscSelected = true;
                _scaleNavigationStep = 0;
            }
            else if (_accessibilityEnabled &&
                     _miscSelected &&
                     key == RobloxKeyboardKey.RightArrow)
            {
                _scaleNavigationStep = 1;
            }
            else if (_accessibilityEnabled &&
                     _miscSelected &&
                     key == RobloxKeyboardKey.DownArrow &&
                     _scaleNavigationStep is 1 or 2)
            {
                _scaleNavigationStep++;
            }
            else if (_accessibilityEnabled &&
                     _miscSelected &&
                     key == RobloxKeyboardKey.LeftArrow &&
                     _scaleNavigationStep == 3)
            {
                _scaleInputReady = true;
            }
            else if (key == RobloxKeyboardKey.Enter &&
                     _scaleInputReady &&
                     !_editingScale)
            {
                _editingScale = true;
                _scaleInput = string.Empty;
            }
            else if (key == RobloxKeyboardKey.Enter &&
                     _editingScale)
            {
                AppliedScaleValues.Add(_scaleInput);
                CurrentFrame =
                    ScaleApplyFrame?.Invoke(_scaleInput) ??
                    _frames.Gameplay;
                GameSettingsPanelMatch panel =
                    GameSettingsScreenDetector
                        .DetectPanel(CurrentFrame);
                _scaleNormalized =
                    panel.Visible &&
                    Math.Abs(panel.UiScale - 1) <=
                    GameSettingsScreenDetector
                        .CanonicalScaleTolerance;
                _editingScale = false;
            }
            else if (key == RobloxKeyboardKey.Backspace &&
                     _editingScale &&
                     _scaleInput.Length > 0)
            {
                _scaleInput =
                    _scaleInput[..^1];
            }
            else if (_editingScale &&
                     KeyboardCharacter(key) is char character)
            {
                _scaleInput += character;
            }
            return Task.CompletedTask;
        }

        private static char? KeyboardCharacter(
            RobloxKeyboardKey key) => key switch
            {
                RobloxKeyboardKey.Digit0 => '0',
                RobloxKeyboardKey.Digit1 => '1',
                RobloxKeyboardKey.Digit2 => '2',
                RobloxKeyboardKey.Digit3 => '3',
                RobloxKeyboardKey.Digit4 => '4',
                RobloxKeyboardKey.Digit5 => '5',
                RobloxKeyboardKey.Digit6 => '6',
                RobloxKeyboardKey.Digit7 => '7',
                RobloxKeyboardKey.Digit8 => '8',
                RobloxKeyboardKey.Digit9 => '9',
                RobloxKeyboardKey.Period => '.',
                _ => null,
            };

        public Task TapUnitKeyAsync(
            RobloxWindow window,
            int unitKey,
            int holdMilliseconds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class LobbyDetector(
        ImageFrame lobby,
        bool alwaysLobby = false) : IDetectorPack
    {
        public DetectorPackManifest Manifest => null!;

        public IReadOnlyDictionary<string, double> ScoreStates(
            ImageFrame clientImage) =>
            new Dictionary<string, double>();

        public string? Classify(
            IReadOnlyDictionary<string, double> scores) =>
            null;

        public string? RecoveryState(
            ImageFrame clientImage) =>
            alwaysLobby ||
            ReferenceEquals(clientImage, lobby)
                ? "lobby"
                : null;

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
