using System.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task RecoverToPrestartAsync(
        RobloxWindow window,
        string initialState,
        ExpeditionPreset preset,
        IDetectorPack detector,
        DiscordRunReporter reporter,
        bool notify,
        Stopwatch runtime,
        int victories,
        int defeats,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        log(
            $"Automatic recovery started from {Label(initialState)}. Target: map {preset.MapNumber}, difficulty {preset.Difficulty}.",
            MacroEventLevel.Warning,
            initialState,
            null);
        if (notify)
        {
            ImageFrame? screenshot =
                TryCaptureClient(window, detector);
            reporter.Queue(
                "recovery",
                $"Automatic rejoin was needed after {Label(initialState)} was recognized.",
                screenshot,
                runtime.Elapsed,
                victories,
                defeats,
                new DiscordRunTarget(
                    preset.MapNumber,
                    preset.Difficulty,
                    string.Empty));
        }

        string completedState =
            await ExpeditionRecoveryTransitionLoop.RunAsync(
                initialState,
                static state => state.Equals(
                    "start",
                    StringComparison.OrdinalIgnoreCase),
                AdvanceAsync,
                cancellationToken).ConfigureAwait(false);
        report(
            "Recovery",
            100,
            "Returned to the configured Expedition prestart screen.",
            completedState,
            null);
        log(
            "Automatic recovery completed.",
            MacroEventLevel.Success,
            completedState,
            null);

        async Task<string?> AdvanceAsync(
            string state,
            CancellationToken token)
        {
            switch (state)
            {
                case "afk":
                    report(
                        "Recovery",
                        0,
                        "AFK Chamber recognized. Returning to the lobby before rejoining the configured route.",
                        state,
                        null);
                    if (!await TryClickRecoveryAsync(
                            window,
                            detector,
                            "afk",
                            log,
                            token).ConfigureAwait(false))
                    {
                        return null;
                    }
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        "afk",
                        TimeSpan.FromSeconds(20),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
                case "disconnect":
                    report(
                        "Recovery",
                        0,
                        "Disconnected. Clicking Reconnect and waiting for Roblox.",
                        state,
                        null);
                    if (!await TryClickRecoveryAsync(
                            window,
                            detector,
                            "disconnect",
                            log,
                            token).ConfigureAwait(false))
                    {
                        return null;
                    }
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        "disconnect",
                        TimeSpan.FromSeconds(12),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
                case "lobby":
                    _fastNoAlign.ObserveLobby(window);
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(
                            detector.RecoveryState(candidate),
                            "lobby",
                            StringComparison.OrdinalIgnoreCase),
                        candidate => string.Equals(
                            detector.RecoveryState(candidate),
                            "play",
                            StringComparison.OrdinalIgnoreCase),
                        (key, innerToken) =>
                            _automation.TapLetterKeyAsync(
                                window,
                                key,
                                innerToken),
                        async (
                            timeout,
                            initialOpenObservation,
                            innerToken) => string.Equals(
                            await WaitForRecoveryChangeAsync(
                                window,
                                detector,
                                "lobby",
                                timeout,
                                preset,
                                report,
                                log,
                                innerToken,
                                initialOpenObservation
                                    ? "play"
                                    : null).ConfigureAwait(false),
                            "play",
                            StringComparison.OrdinalIgnoreCase),
                        attempt => report(
                            "Recovery",
                            0,
                            $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).",
                            state,
                            null),
                        attempt => log(
                            $"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).",
                            MacroEventLevel.Warning,
                            state,
                            null),
                        token).ConfigureAwait(false);
                    return "play";
                case "play":
                    report(
                        "Recovery",
                        0,
                        "Play screen recognized. Opening Expeditions.",
                        state,
                        null);
                    if (!await TryClickRecoveryAsync(
                            window,
                            detector,
                            "play",
                            log,
                            token).ConfigureAwait(false))
                    {
                        return null;
                    }
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        "play",
                        TimeSpan.FromSeconds(15),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
                case "post_match_party":
                    report(
                        "Recovery",
                        0,
                        "A previous party is still open. Returning to the shared game-mode selector.",
                        state,
                        null);
                    await CompleteGameModeHandoffAsync(
                        window,
                        detector,
                        preset,
                        playMenuKey,
                        state,
                        pressPlayFirst: false,
                        report,
                        log,
                        token).ConfigureAwait(false);
                    return "play";
                case "map_select":
                    await ConfigureMapAndDifficultyAsync(
                        window,
                        preset,
                        detector,
                        report,
                        log,
                        token).ConfigureAwait(false);
                    report(
                        "Recovery",
                        0,
                        "Map and difficulty verified. Selecting the stage.",
                        state,
                        null);
                    if (!await TryClickRecoveryAsync(
                            window,
                            detector,
                            "select_stage",
                            log,
                            token).ConfigureAwait(false))
                    {
                        return null;
                    }
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        "map_select",
                        TimeSpan.FromSeconds(15),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
                case "map_preview":
                    report(
                        "Recovery",
                        0,
                        "Teleport preview recognized. Starting the private stage.",
                        state,
                        null);
                    if (!await TryClickRecoveryAsync(
                            window,
                            detector,
                            "map_preview",
                            log,
                            token).ConfigureAwait(false))
                    {
                        return null;
                    }
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        "map_preview",
                        TimeSpan.FromSeconds(20),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
                case "continue":
                    report(
                        "Recovery",
                        0,
                        "Initial Expedition checkpoint recognized. Continuing to the prestart screen.",
                        state,
                        null);
                    if (!await TryClickRecoveryAsync(
                            window,
                            detector,
                            "continue",
                            log,
                            token).ConfigureAwait(false))
                    {
                        return null;
                    }
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        "continue",
                        TimeSpan.FromSeconds(20),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
                default:
                    return await WaitForRecoveryChangeAsync(
                        window,
                        detector,
                        string.Empty,
                        TimeSpan.FromSeconds(20),
                        preset,
                        report,
                        log,
                        token).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> TryClickRecoveryAsync(
        RobloxWindow window,
        IDetectorPack detector,
        string state,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        try
        {
            await ClickActionAsync(
                window,
                detector,
                state,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            log(
                $"Recovery click '{state}' failed and will be retried: {error.Message}",
                MacroEventLevel.Warning,
                state,
                null);
            await Task.Delay(
                750,
                cancellationToken).ConfigureAwait(false);
            return false;
        }
    }
}
