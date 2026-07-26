using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private async Task<bool> CompleteScheduledHandoffAsync(
        RobloxWindow window,
        EventTerminalObservation terminal,
        RepeatedRoutePreparationState preparation,
        IDetectorPack detector,
        char playMenuKey,
        StageRunOutcome outcome,
        TimeSpan runtime,
        Func<
            int,
            int,
            TimeSpan,
            CancellationToken,
            Task<ScheduledTaskContinuation>> continueScheduledRoute,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        ScheduledTaskContinuation continuation =
            await continueScheduledRoute(
                outcome == StageRunOutcome.Victory ? 1 : 0,
                outcome == StageRunOutcome.Defeat ? 1 : 0,
                runtime,
                cancellationToken).ConfigureAwait(false);
        switch (continuation)
        {
            case ScheduledTaskContinuation.RepeatStage:
                (int X, int Y)? repeatAction =
                    StageScreenDetector.RepeatStageAction(
                        terminal.Frame,
                        terminal.State ==
                            EventScreenState.Victory
                            ? StageScreenState.Victory
                            : StageScreenState.Defeat);
                if (repeatAction is null)
                {
                    throw new RobloxUiUnavailableException(
                        "The Event Repeat Stage button could not be located.");
                }
                report(
                    "Handoff",
                    100,
                    "The same Event route is next. Repeating the stage.",
                    "repeat_stage",
                    terminal.Confidence);
                log(
                    "The scheduler kept the same Event route; using Repeat Stage.",
                    MacroEventLevel.Success,
                    "repeat_stage",
                    terminal.Confidence);
                await ClickAsync(
                    window,
                    repeatAction.Value.X,
                    repeatAction.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                preparation.MarkRepeatStageRequested();
                await WaitForStateAsync(
                    window,
                    EventScreenState.Prestart,
                    TimeSpan.FromSeconds(45),
                    detector,
                    cancellationToken).ConfigureAwait(false);
                return true;
            case ScheduledTaskContinuation.ReturnToLobby:
                report(
                    "Handoff",
                    100,
                    "Another Event route is next. Returning to the Lobby.",
                    "return_to_lobby",
                    terminal.Confidence);
                log(
                    "The next scheduled route is Event-only; returning directly to the Lobby instead of opening Play.",
                    MacroEventLevel.Information,
                    "return_to_lobby",
                    terminal.Confidence);
                await _lobby.ReturnAsync(
                    window,
                    detector,
                    cancellationToken).ConfigureAwait(false);
                _fastNoAlign.ObserveLobby(window);
                return false;
            case ScheduledTaskContinuation.Handoff:
                report(
                    "Handoff",
                    100,
                    "A Play-accessible route is next. Opening shared navigation.",
                    "event_handoff",
                    terminal.Confidence);
                await OpenGameModeSelectorAsync(
                    window,
                    detector,
                    playMenuKey,
                    cancellationToken).ConfigureAwait(false);
                return false;
            default:
                throw new InvalidOperationException(
                    "The Event handoff policy returned an unknown continuation.");
        }
    }
}
