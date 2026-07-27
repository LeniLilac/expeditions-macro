using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Automation.Settings;

public sealed partial class MacroStartupPreflightService
{
    private const int StableFrames = 3;
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(160);
    private readonly IRobloxAutomation _automation;
    private readonly GameSettingsNormalizer _normalizer;
    private readonly UiScaleNormalizer _uiScale;
    private readonly AccessibilityNavigationController
        _accessibility;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly Func<
        RobloxWindow,
        CancellationToken,
        Task> _prepareCamera;

    public MacroStartupPreflightService(
        IRobloxAutomation automation,
        CameraPosePreparationService cameraPose)
        : this(
            automation,
            static () => DateTimeOffset.UtcNow,
            static (duration, token) =>
                Task.Delay(duration, token),
            (window, token) =>
                cameraPose.PreparePitchOnlyAsync(
                    window,
                    cancellationToken: token))
    {
    }

    internal MacroStartupPreflightService(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<RobloxWindow, CancellationToken, Task>
            prepareCamera)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
        _prepareCamera = prepareCamera;
        _normalizer = new GameSettingsNormalizer(
            automation,
            utcNow,
            delay);
        _uiScale = new UiScaleNormalizer(
            automation,
            utcNow,
            delay);
        _accessibility =
            new AccessibilityNavigationController(
                automation,
                ValidateWindow,
                delay);
    }

    public async Task<GameSettingsNormalizationResult>
        RunUiScaleAsync(
            IDetectorPack detector,
            IProgress<MacroProgress>? progress,
            Action<MacroEvent>? log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detector);
        RobloxWindow window =
            await AcquireWindowAsync(
                "Preparing Roblox for the UI Scale check.",
                progress,
                cancellationToken).ConfigureAwait(false);
        await _prepareCamera(
            window,
            cancellationToken).ConfigureAwait(false);
        bool changed = await _uiScale.NormalizeAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        Report(
            changed
                ? "Anime Expeditions UI Scale is set to 1.00."
                : "Anime Expeditions UI Scale already matches 1.00.",
            progress,
            log,
            MacroEventLevel.Success);
        return new GameSettingsNormalizationResult(
            0,
            changed);
    }

    public async Task<GameSettingsNormalizationResult>
        RunGameSettingsAsync(
            IDetectorPack detector,
            IProgress<MacroProgress>? progress,
            Action<MacroEvent>? log,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detector);
        RobloxWindow window =
            await AcquireWindowAsync(
                "Waiting for a stable lobby before checking game settings.",
                progress,
                cancellationToken).ConfigureAwait(false);
        await _prepareCamera(
            window,
            cancellationToken).ConfigureAwait(false);
        await WaitForLobbyAsync(
            window,
            detector,
            TimeSpan.FromSeconds(12),
            cancellationToken).ConfigureAwait(false);
        await OpenSettingsPanelAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        GameSettingsPanelMatch panel =
            await WaitForPanelAsync(
                window,
                canonicalScale: false,
                cancellationToken).ConfigureAwait(false);
        if (!GameSettingsScreenDetector
                .IsCanonicalUiScale(
                    panel.UiScale))
        {
            await CloseSettingsPanelAsync(
                window,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                "UI Scale must be 1.00 before checking game settings. Run the UI Scale debug action first.");
        }

        int changes = await _normalizer.NormalizeAsync(
            window,
            message => Report(
                message,
                progress,
                log,
                MacroEventLevel.Information),
            cancellationToken).ConfigureAwait(false);
        await CloseSettingsPanelAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        await WaitForLobbyAsync(
            window,
            detector,
            TimeSpan.FromSeconds(7),
            cancellationToken).ConfigureAwait(false);
        Report(
            changes == 0
                ? "Anime Expeditions settings already match the required profile."
                : $"Anime Expeditions settings ready: {changes} toggle(s) corrected.",
            progress,
            log,
            MacroEventLevel.Success);
        return new GameSettingsNormalizationResult(
            changes,
            false);
    }

    public async Task<GameSettingsNormalizationResult> RunAsync(
        IDetectorPack detector,
        bool normalizeUiScale,
        bool normalizeGameSettings,
        IProgress<MacroProgress>? progress,
        Action<MacroEvent>? log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detector);
        RobloxWindow window =
            await AcquireWindowAsync(
                StartupPreparationMessage.Progress(
                    normalizeUiScale,
                    normalizeGameSettings),
                progress,
                cancellationToken).ConfigureAwait(false);
        if (!normalizeUiScale &&
            !normalizeGameSettings)
        {
            await WaitForLobbyAsync(
                window,
                detector,
                TimeSpan.FromSeconds(12),
                cancellationToken).ConfigureAwait(false);
            Report(
                "Lobby verified. UI Scale and required game-settings checks are disabled; no Settings input was sent.",
                progress,
                log,
                MacroEventLevel.Information);
            return new GameSettingsNormalizationResult(
                0,
                false);
        }

        await _prepareCamera(
            window,
            cancellationToken).ConfigureAwait(false);
        bool scaleChanged = false;
        if (normalizeUiScale)
        {
            Report(
                "Checking Anime Expeditions UI Scale before lobby verification.",
                progress,
                log,
                MacroEventLevel.Information);
            scaleChanged =
                await _uiScale.NormalizeAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
        }

        await WaitForLobbyAsync(
            window,
            detector,
            TimeSpan.FromSeconds(12),
            cancellationToken).ConfigureAwait(false);

        int changes = 0;
        if (normalizeGameSettings)
        {
            Report(
                "Lobby verified. Checking required Anime Expeditions settings.",
                progress,
                log,
                MacroEventLevel.Information);
            GameSettingsPanelMatch panel =
                await OpenSettingsPanelAsync(
                window,
                cancellationToken).ConfigureAwait(false);
            if (!GameSettingsScreenDetector
                    .IsCanonicalUiScale(
                        panel.UiScale))
            {
                await CloseSettingsPanelAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Required game settings cannot be checked at the current UI Scale. Enable Check and fix UI Scale at macro start, or set the in-game value to 1.00.");
            }
            changes = await _normalizer.NormalizeAsync(
                window,
                message => Report(
                    message,
                    progress,
                    log,
                    MacroEventLevel.Information),
                cancellationToken).ConfigureAwait(false);
            await CloseSettingsPanelAsync(
                window,
                cancellationToken).ConfigureAwait(false);
            await WaitForLobbyAsync(
                window,
                detector,
                TimeSpan.FromSeconds(7),
                cancellationToken).ConfigureAwait(false);
        }

        Report(
            StartupPreparationMessage.Result(
                normalizeUiScale,
                normalizeGameSettings,
                changes,
                scaleChanged),
            progress,
            log,
            MacroEventLevel.Success);
        return new GameSettingsNormalizationResult(
            changes,
            scaleChanged);
    }

    private async Task<RobloxWindow> AcquireWindowAsync(
        string progressMessage,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ReportProgress(
            progressMessage,
            progress);
        RobloxWindow window = _automation.FindWindow()
            ?? throw new RobloxSessionUnavailableException(
                "Open Roblox, join the Anime Expeditions lobby, and close Play, Areas, Units, and Settings before starting the macro.");
        await _automation.ResizeClientAsync(
            window,
            GameSettingsScreenDetector.ClientWidth,
            GameSettingsScreenDetector.ClientHeight,
            cancellationToken).ConfigureAwait(false);
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox for the startup check.");
        }
        return window;
    }

    private async Task CloseSettingsPanelAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        await _accessibility.RunEnabledAsync(
            window,
            async token =>
            {
                await _accessibility.TapAsync(
                    window,
                    RobloxKeyboardKey.RightArrow,
                    token).ConfigureAwait(false);
                await _accessibility.TapAsync(
                    window,
                    RobloxKeyboardKey.Enter,
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await WaitForSettingsClosedAsync(
            window,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<GameSettingsPanelMatch>
        OpenSettingsPanelAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        await _accessibility.RunEnabledAsync(
            window,
            async token =>
            {
                await _accessibility.TapAsync(
                    window,
                    RobloxKeyboardKey.RightArrow,
                    token).ConfigureAwait(false);
                await _accessibility.TapAsync(
                    window,
                    RobloxKeyboardKey.Enter,
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return await WaitForPanelAsync(
            window,
            canonicalScale: false,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<GameSettingsPanelMatch> WaitForPanelAsync(
        RobloxWindow window,
        bool canonicalScale,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            _utcNow() + TimeSpan.FromSeconds(7);
        int stable = 0;
        GameSettingsPanelMatch last = default;
        while (_utcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateWindow(window);
            last = GameSettingsScreenDetector.DetectPanel(
                _automation.CaptureClient(window));
            bool expected =
                last.Visible &&
                last.Settled &&
                (!canonicalScale ||
                 GameSettingsScreenDetector
                     .IsCanonicalUiScale(
                         last.UiScale));
            stable = expected ? stable + 1 : 0;
            if (stable >= 2) return last;
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            canonicalScale
                ? $"Anime Expeditions UI Scale did not settle at 1.00 (last detected {last.UiScale:0.00})."
                : "Anime Expeditions Settings did not finish opening.");
    }

    private async Task WaitForSettingsClosedAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            _utcNow() + TimeSpan.FromSeconds(7);
        int stable = 0;
        while (_utcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateWindow(window);
            GameSettingsPanelMatch panel =
                GameSettingsScreenDetector.DetectPanel(
                    _automation.CaptureClient(window));
            stable =
                !panel.Visible && panel.CloseX == 0
                    ? stable + 1
                    : 0;
            if (stable >= 2) return;
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "Anime Expeditions Settings did not finish closing.");
    }

    private void ValidateWindow(RobloxWindow window)
    {
        RobloxWindow? current = _automation.FindWindow();
        if (current is null ||
            current.Value.Handle != window.Handle)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox closed or changed while checking startup settings.");
        }
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox while checking startup settings.");
        }
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width !=
                GameSettingsScreenDetector.ClientWidth ||
            bounds.Height !=
                GameSettingsScreenDetector.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox changed size while checking startup settings.");
        }
    }

    private static void Report(
        string message,
        IProgress<MacroProgress>? progress,
        Action<MacroEvent>? log,
        MacroEventLevel level)
    {
        ReportProgress(message, progress);
        log?.Invoke(
            new MacroEvent(
                DateTimeOffset.Now,
                level,
                message,
                "startup_settings"));
    }

    private static void ReportProgress(
        string message,
        IProgress<MacroProgress>? progress) =>
        progress?.Report(
            new MacroProgress(
                "Startup checks",
                0,
                message,
                "startup_settings"));
}
