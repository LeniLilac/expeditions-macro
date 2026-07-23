using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task<bool> EnsureGameModeSelectorAsync(
        RobloxWindow window,
        StageMode mode,
        char playMenuKey,
        IDetectorPack detector,
        bool autoRecover,
        int stableDetections,
        Action<string, int, string, string?, double?>? report,
        Action<string, MacroEventLevel, string?, double?>? log,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + RecoveryTimeout;
        StableStateTracker<string> recoveryTracker =
            new(stableDetections);
        int playMenuAttempts = 0;
        string? lastRecovery = null;
        bool recovered = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch current = StageScreenDetector.Detect(frame);
            if (current.State == StageScreenState.GameModeSelector)
            {
                return recovered;
            }

            string? recovery = detector.RecoveryState(frame);
            string? stableRecovery = recoveryTracker.Update(
                IsRootRecovery(recovery) ? recovery : null);
            if (stableRecovery is not null)
            {
                if (!autoRecover)
                {
                    throw new StageRecoveryException(stableRecovery);
                }
                recovered = true;
                if (!string.Equals(
                    lastRecovery,
                    stableRecovery,
                    StringComparison.OrdinalIgnoreCase))
                {
                    lastRecovery = stableRecovery;
                    log?.Invoke(
                        $"Automatic {Label(mode)} recovery started from {RecoveryLabel(stableRecovery)}.",
                        MacroEventLevel.Warning,
                        stableRecovery,
                        null);
                }

                if (stableRecovery == "lobby")
                {
                    await LobbyPlayNavigator.OpenWithVerificationAsync(
                        playMenuKey,
                        () => CaptureClient(window, detector),
                        candidate => string.Equals(
                            detector.RecoveryState(candidate),
                            "lobby",
                            StringComparison.OrdinalIgnoreCase),
                        candidate =>
                            StageScreenDetector.Detect(candidate).State ==
                            StageScreenState.GameModeSelector,
                        (key, token) =>
                            _automation.TapLetterKeyAsync(
                                window,
                                key,
                                token),
                        (timeout, token) => TryWaitForStateAsync(
                            window,
                            StageScreenState.GameModeSelector,
                            timeout,
                            detector,
                            stableDetections,
                            token),
                        attempt => report?.Invoke(
                            "Recovery",
                            0,
                            $"Lobby recognized. Opening Play with {playMenuKey} (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).",
                            stableRecovery,
                            null),
                        attempt => log?.Invoke(
                            $"The {playMenuKey} Play-menu key did not open navigation from the lobby (attempt {attempt}/{LobbyPlayNavigator.MaximumAttempts}).",
                            MacroEventLevel.Warning,
                            stableRecovery,
                            null),
                        cancellationToken).ConfigureAwait(false);
                    return recovered;
                }

                report?.Invoke(
                    "Recovery",
                    0,
                    stableRecovery == "disconnect"
                        ? "Disconnected. Rejoining Roblox."
                        : "AFK Chamber recognized. Returning to the lobby.",
                    stableRecovery,
                    null);
                (int x, int y) = detector.ActionFor(
                    stableRecovery,
                    frame);
                await ClickAsync(
                    window,
                    x,
                    y,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(
                    stableRecovery == "disconnect" ? 5000 : 2200,
                    cancellationToken).ConfigureAwait(false);
                recoveryTracker.Reset();
                continue;
            }

            (int X, int Y)? changeMode =
                StageScreenDetector.PostMatchChangeModeAction(frame);
            GameModeHandoffCommand command =
                StageNavigationPolicy.SelectGameModeHandoffCommand(
                    current.State,
                    changeMode is not null);
            switch (command)
            {
                case GameModeHandoffCommand.Complete:
                    return recovered;
                case GameModeHandoffCommand.ChangeGamemode:
                    report?.Invoke(
                        "Handoff",
                        0,
                        $"Leaving the completed {Label(mode)} party through Change Gamemode.",
                        "stage_change_gamemode",
                        current.Confidence);
                    await ClickAsync(
                        window,
                        changeMode!.Value.X,
                        changeMode.Value.Y,
                        cancellationToken).ConfigureAwait(false);
                    playMenuAttempts = 0;
                    if (await TryWaitForStateAsync(
                        window,
                        StageScreenState.GameModeSelector,
                        NavigationTimeout,
                        detector,
                        stableDetections,
                        cancellationToken).ConfigureAwait(false))
                    {
                        return recovered;
                    }
                    continue;
                case GameModeHandoffCommand.Back:
                    report?.Invoke(
                        "Handoff",
                        0,
                        $"Leaving the nested {Label(mode)} interface through Back.",
                        "stage_back",
                        current.Confidence);
                    (int backX, int backY) =
                        StageScreenDetector.SelectorBackAction;
                    await ClickAsync(
                        window,
                        backX,
                        backY,
                        cancellationToken).ConfigureAwait(false);
                    playMenuAttempts = 0;
                    if (await TryWaitForStateAsync(
                        window,
                        StageScreenState.GameModeSelector,
                        NavigationTimeout,
                        detector,
                        stableDetections,
                        cancellationToken).ConfigureAwait(false))
                    {
                        return recovered;
                    }
                    continue;
                case GameModeHandoffCommand.PressPlayKey:
                    if (playMenuAttempts >=
                        LobbyPlayNavigator.MaximumAttempts)
                    {
                        throw new PlayMenuBindingException(
                            char.ToUpperInvariant(playMenuKey));
                    }
                    playMenuAttempts++;
                    report?.Invoke(
                        "Navigation",
                        0,
                        playMenuAttempts == 1
                            ? $"Opening the Play menu with {playMenuKey}."
                            : $"Retrying the {playMenuKey} Play-menu key ({playMenuAttempts}/{LobbyPlayNavigator.MaximumAttempts}).",
                        "play_menu_key",
                        null);
                    Focus(window);
                    await _automation.TapLetterKeyAsync(
                        window,
                        playMenuKey,
                        cancellationToken).ConfigureAwait(false);
                    GameModeHandoffCommand? transition =
                        await TryWaitForPlayKeyTransitionAsync(
                            window,
                            detector,
                            stableDetections,
                            TimeSpan.FromSeconds(4),
                            cancellationToken).ConfigureAwait(false);
                    if (transition == GameModeHandoffCommand.Complete)
                    {
                        return recovered;
                    }
                    if (transition ==
                        GameModeHandoffCommand.ChangeGamemode)
                    {
                        playMenuAttempts = 0;
                    }
                    continue;
                default:
                    throw new InvalidOperationException(
                        "The stage handoff policy returned an unknown command.");
            }
        }

        StageScreenMatch last = StageScreenDetector.Detect(
            CaptureClient(window, detector));
        throw new TimeoutException(
            $"Timed out opening the Play menu. Last detected state: {last.State} ({last.Confidence:P0}).");
    }

    private async Task<GameModeHandoffCommand?>
        TryWaitForPlayKeyTransitionAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int stableDetections,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<string> tracker = new(stableDetections);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            StageScreenMatch current =
                StageScreenDetector.Detect(frame);
            bool hasChangeMode =
                StageScreenDetector.PostMatchChangeModeAction(frame)
                is not null;
            GameModeHandoffCommand command =
                StageNavigationPolicy.SelectGameModeHandoffCommand(
                    current.State,
                    hasChangeMode);
            string? candidate = command is
                GameModeHandoffCommand.Complete or
                GameModeHandoffCommand.ChangeGamemode
                ? command.ToString()
                : null;
            if (tracker.Update(candidate) is string stable)
            {
                return Enum.Parse<GameModeHandoffCommand>(stable);
            }
            await Task.Delay(
                180,
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }
}
