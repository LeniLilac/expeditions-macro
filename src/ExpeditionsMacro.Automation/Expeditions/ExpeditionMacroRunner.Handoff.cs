using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private Task OpenPlayMenuForModeSwitchAsync(
        RobloxWindow window,
        IDetectorPack detector,
        RunTerminal terminal,
        ExpeditionPreset preset,
        char playMenuKey,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        CompleteGameModeHandoffAsync(
            window,
            detector,
            preset,
            playMenuKey,
            terminal.State,
            pressPlayFirst: true,
            report,
            log,
            cancellationToken);

    private async Task CompleteGameModeHandoffAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        char playMenuKey,
        string handoffState,
        bool pressPlayFirst,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        if (pressPlayFirst)
        {
            report(
                "Handoff",
                100,
                $"Opening Play with {playMenuKey}.",
                handoffState,
                null);
            await OpenPlayMenuAsync(
                window,
                detector,
                preset,
                playMenuKey,
                "Handoff",
                handoffState,
                report,
                log,
                cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset deadline =
            DateTimeOffset.UtcNow + GameModeHandoffTimeout;
        StableStateTracker<ChallengeScreenState> tracker =
            new(Math.Max(1, preset.StableDetections));
        ChallengeScreenMatch last =
            new(ChallengeScreenState.None, 0);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame = CaptureClient(window, detector);
            last = ChallengeScreenDetector.Detect(frame);
            ChallengeScreenState? stable = tracker.Update(last.State);
            if (stable is null)
            {
                await Task.Delay(
                    preset.PollMilliseconds,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            switch (SelectGameModeHandoffCommand(stable.Value))
            {
                case GameModeHandoffCommand.Complete:
                    log(
                        "Expedition handoff reached the shared game-mode selector.",
                        MacroEventLevel.Success,
                        "game_mode_selector",
                        last.Confidence);
                    return;
                case GameModeHandoffCommand.ChangeGamemode:
                {
                    (int X, int Y)? changeMode =
                        ChallengeScreenDetector.ActionFor(
                            ChallengeScreenState.PostMatchPreview,
                            frame);
                    if (changeMode is null)
                    {
                        await Task.Delay(
                            preset.PollMilliseconds,
                            cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    report(
                        "Handoff",
                        100,
                        "Leaving the Expedition party through Change Gamemode.",
                        "expedition_change_gamemode",
                        last.Confidence);
                    Focus(window);
                    await _automation.ClickClientAsync(
                        window,
                        changeMode.Value.X,
                        changeMode.Value.Y,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }
                case GameModeHandoffCommand.PressPlayKey:
                    await OpenPlayMenuAsync(
                        window,
                        detector,
                        preset,
                        playMenuKey,
                        "Handoff",
                        last.State.ToString(),
                        report,
                        log,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case GameModeHandoffCommand.Wait:
                    await Task.Delay(
                        preset.PollMilliseconds,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                default:
                    throw new InvalidOperationException(
                        "The Expedition handoff policy returned an unknown command.");
            }
            tracker.Reset();
            await Task.Delay(
                700,
                cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out leaving the completed Expedition. Last state: {last.State} ({last.Confidence:P0}).");
    }

    internal static GameModeHandoffCommand
        SelectGameModeHandoffCommand(
            ChallengeScreenState state) => state switch
        {
            ChallengeScreenState.GameModeSelector =>
                GameModeHandoffCommand.Complete,
            ChallengeScreenState.Victory or
            ChallengeScreenState.Defeat =>
                GameModeHandoffCommand.PressPlayKey,
            ChallengeScreenState.PostMatchPreview =>
                GameModeHandoffCommand.ChangeGamemode,
            ChallengeScreenState.PostMatchHud =>
                GameModeHandoffCommand.PressPlayKey,
            _ => GameModeHandoffCommand.Wait,
        };

    private Task<ImageFrame> OpenPlayMenuAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        char playMenuKey,
        string phase,
        string state,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken) =>
        PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey,
            () => CaptureClient(window, detector),
            (key, token) =>
                _automation.TapLetterKeyAsync(window, key, token),
            (timeout, token) => TryWaitForPlayMenuAsync(
                window,
                detector,
                preset,
                timeout,
                token),
            attempt => report(
                phase,
                100,
                attempt == 1
                    ? $"Opening the Play menu with {playMenuKey}."
                    : $"Retrying the {playMenuKey} Play-menu key ({attempt}/{PlayMenuNavigator.MaximumAttempts}).",
                state,
                null),
            attempt => log(
                $"The {playMenuKey} Play-menu key did not open navigation (attempt {attempt}/{PlayMenuNavigator.MaximumAttempts}).",
                MacroEventLevel.Warning,
                state,
                null),
            cancellationToken);

    private async Task<ImageFrame?> TryWaitForPlayMenuAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        StableStateTracker<ChallengeScreenState> tracker =
            new(Math.Max(1, preset.StableDetections));
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame current = CaptureClient(window, detector);
            ChallengeScreenMatch match =
                ChallengeScreenDetector.Detect(current);
            ChallengeScreenState? stable = tracker.Update(
                match.State == ChallengeScreenState.PostMatchPreview
                    ? ChallengeScreenState.PostMatchPreview
                    : ChallengeScreenState.None);
            if (stable == ChallengeScreenState.PostMatchPreview)
            {
                return current;
            }
            await Task.Delay(
                preset.PollMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
