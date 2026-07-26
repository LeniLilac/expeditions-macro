using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task<bool> CompleteScheduledHandoffAsync(
        RobloxWindow window,
        IDetectorPack detector,
        RunTerminal terminal,
        ExpeditionPreset preset,
        char playMenuKey,
        RepeatedRoutePreparationState preparation,
        TimeSpan runtime,
        Func<
            int,
            int,
            TimeSpan,
            CancellationToken,
            Task<ScheduledTaskContinuation>>
            continueScheduledRoute,
        Action<string, int, string, string?, double?>
            report,
        Action<string, MacroEventLevel, string?, double?>
            log,
        CancellationToken cancellationToken)
    {
        ScheduledTaskContinuation continuation =
            await continueScheduledRoute(
                terminal.State == "victory" ? 1 : 0,
                terminal.State == "defeat" ? 1 : 0,
                runtime,
                cancellationToken).ConfigureAwait(false);
        switch (continuation)
        {
            case ScheduledTaskContinuation.RepeatStage:
                report(
                    "Completed",
                    100,
                    "The same Expedition route is next. Repeating the stage.",
                    terminal.State,
                    null);
                log(
                    "The scheduler kept the same Expedition route; using Repeat Stage.",
                    MacroEventLevel.Success,
                    "repeat_stage",
                    null);
                await ClickActionAsync(
                    window,
                    detector,
                    terminal.State,
                    terminal.Frame,
                    cancellationToken).ConfigureAwait(false);
                preparation.MarkRepeatStageRequested();
                await Task.Delay(
                    4500,
                    cancellationToken).ConfigureAwait(false);
                return true;
            case ScheduledTaskContinuation.ReturnToLobby:
                report(
                    "Completed",
                    100,
                    "An Event route is next. Returning to the Lobby.",
                    "return_to_lobby",
                    null);
                log(
                    "The next scheduled route is Event-only; returning from Expedition directly to the Lobby.",
                    MacroEventLevel.Information,
                    "return_to_lobby",
                    null);
                await new MatchLobbyNavigator(_automation)
                    .ReturnAsync(
                        window,
                        detector,
                        cancellationToken)
                    .ConfigureAwait(false);
                _fastNoAlign.ObserveLobby(window);
                preparation.Invalidate();
                return false;
            case ScheduledTaskContinuation.Handoff:
                report(
                    "Completed",
                    100,
                    "Scheduled Expedition match finished. Returning to shared navigation.",
                    terminal.State,
                    null);
                log(
                    "The next scheduled route is Play-accessible. Leaving the completed Expedition through the Play selector.",
                    MacroEventLevel.Information,
                    "game_mode_selector",
                    null);
                await OpenPlayMenuForModeSwitchAsync(
                    window,
                    detector,
                    terminal,
                    preset,
                    playMenuKey,
                    report,
                    log,
                    cancellationToken).ConfigureAwait(false);
                return false;
            default:
                throw new InvalidOperationException(
                    "The Expedition handoff policy returned an unknown continuation.");
        }
    }
}
