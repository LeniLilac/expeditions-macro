using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Tests;

public sealed partial class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task
        PreparationChecksDisabled_RequiresStableLobbyWithoutSettingsInput()
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
                normalizeUiScale: false,
                normalizeGameSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
        Assert.Empty(automation.Drags);
        Assert.Empty(automation.AppliedScaleValues);
        Assert.Equal(0, automation.PitchPreparationCount);
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
                normalizeUiScale: false,
                normalizeGameSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
        Assert.Equal(0, automation.PitchPreparationCount);
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
                    normalizeUiScale: true,
                    normalizeGameSettings: true,
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
        Assert.DoesNotContain(
            RobloxKeyboardKey.Backslash,
            automation.Keys);
        Assert.Equal(
            2,
            automation.Clicks.Count(
                point => point ==
                    (
                        RobloxSettingsButtonDetector
                            .NoVoiceActionX,
                        RobloxSettingsButtonDetector
                            .ActionY)));
        Assert.Empty(automation.Drags);
        Assert.Equal(1, automation.PitchPreparationCount);
        Assert.Equal(
            "pitch",
            automation.ActionSequence[0]);
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
        Assert.Equal(1, automation.PitchPreparationCount);
        Assert.Equal(
            "pitch",
            automation.ActionSequence[0]);
        Assert.Same(
            frames.NonLobby,
            automation.CurrentFrame);
    }

    [Fact]
    public async Task GameSettingsDebug_PreparesPitchBeforeSettingsGear()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby);
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunGameSettingsAsync(
                new LobbyDetector(frames.Lobby),
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.Equal(1, automation.PitchPreparationCount);
        Assert.Equal(
            "pitch",
            automation.ActionSequence[0]);
        Assert.StartsWith(
            "click:",
            automation.ActionSequence[1],
            StringComparison.Ordinal);
        Assert.Same(frames.Lobby, automation.CurrentFrame);
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
                normalizeUiScale: false,
                normalizeGameSettings: false,
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
                normalizeUiScale: true,
                normalizeGameSettings: true,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(1, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Single(
            automation.Clicks,
            point => point == (638, 222));
        Assert.Single(automation.Drags);
        Assert.Equal(1, automation.PitchPreparationCount);
        Assert.Same(frames.Lobby, automation.CurrentFrame);
    }

    [Fact]
    public async Task NonCanonicalScale_UsesDetectedInputThenReopensSettings()
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
                normalizeUiScale: true,
                normalizeGameSettings: true,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.DoesNotContain(
            RobloxKeyboardKey.Backslash,
            automation.Keys);
        Assert.Contains(
            RobloxKeyboardKey.Digit1,
            automation.Keys);
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(frames.Scale080);
        Assert.Contains(
            (input.ActionX, input.ActionY),
            automation.Clicks);
        Assert.Equal(
            8,
            automation.Keys.Count(
                key =>
                    key == RobloxKeyboardKey.Backspace));
        Assert.DoesNotContain(
            RobloxKeyboardKey.DownArrow,
            automation.Keys);
        Assert.DoesNotContain(
            RobloxKeyboardKey.LeftArrow,
            automation.Keys);
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
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(frames.Scale120);
        Assert.Equal(
            2,
            automation.Clicks.Count(
                point =>
                    point ==
                    (input.ActionX, input.ActionY)));
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
            clock.DelayAsync,
            automation.PrepareSettingsCameraAsync);

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

        public void Advance(TimeSpan duration) =>
            UtcNow += duration;

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

    private sealed class PreflightAutomation : IRobloxAutomation
    {
        private static readonly RobloxWindow Window =
            new((nint)42, "Roblox", 1234, "RobloxPlayerBeta");
        private readonly TestFrames _frames;
        private ClientBounds _client =
            new(0, 0, 808, 611);
        private bool _editingScale;
        private string _scaleInput = string.Empty;
        private bool _scaleNormalized;
        private bool _settingsOpen;
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

        public Func<ImageFrame, ImageFrame>? CaptureOverride
        {
            get;
            init;
        }

        public bool IgnoreSettingsGearClicks { get; init; }

        public bool IgnoreGameplayToggleClicks { get; init; }

        public bool IgnoreUnitsScrollbarDrags { get; init; }

        public List<(int X, int Y)> Clicks { get; } = [];

        public List<RobloxKeyboardKey> Keys { get; } = [];

        public List<string> AppliedScaleValues { get; } = [];

        public List<string> ActionSequence { get; } = [];

        public int PitchPreparationCount { get; private set; }

        public List<(
            int StartX,
            int StartY,
            int EndX,
            int EndY)> Drags
        { get; } = [];

        public (int Width, int Height)? ResizeRequest { get; private set; }

        public Task PrepareSettingsCameraAsync(
            RobloxWindow window,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PitchPreparationCount++;
            ActionSequence.Add("pitch");
            return Task.CompletedTask;
        }

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
            CaptureOverride?.Invoke(CurrentFrame) ??
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
            ActionSequence.Add($"click:{x},{y}");
            RobloxSettingsButtonMatch settingsButton =
                RobloxSettingsButtonDetector.Detect(
                    CurrentFrame);
            if (settingsButton.Available &&
                (x, y) ==
                (
                    settingsButton.ActionX,
                    settingsButton.ActionY))
            {
                if (IgnoreSettingsGearClicks)
                {
                    return Task.CompletedTask;
                }
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
            GameSettingsNavigationActionMatch miscAction =
                GameSettingsNavigationDetector
                    .DetectPageAction(
                        CurrentFrame,
                        GameSettingsPage.Miscellaneous);
            if (miscAction.Available &&
                (x, y) ==
                (
                    miscAction.ActionX,
                    miscAction.ActionY))
            {
                CurrentFrame = _frames.Miscellaneous;
                return Task.CompletedTask;
            }
            GameSettingsUiScaleInputMatch scaleInput =
                GameSettingsNavigationDetector
                    .DetectUiScaleInput(CurrentFrame);
            if (scaleInput.Available &&
                (x, y) ==
                (
                    scaleInput.ActionX,
                    scaleInput.ActionY))
            {
                _editingScale = true;
                _scaleInput = string.Empty;
                return Task.CompletedTask;
            }
            if ((x, y) == (638, 222))
            {
                if (!IgnoreGameplayToggleClicks)
                {
                    CurrentFrame = GameplayFrameAfterToggle;
                }
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
            if (!IgnoreUnitsScrollbarDrags)
            {
                CurrentFrame =
                    endY >= 400
                        ? _frames.UnitsBottom
                        : _frames.UnitsTop;
            }
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
            ActionSequence.Add($"key:{key}");
            Keys.Add(key);
            if (key == RobloxKeyboardKey.Enter &&
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
                    GameSettingsScreenDetector
                        .IsCanonicalUiScale(
                            panel.UiScale);
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

}
