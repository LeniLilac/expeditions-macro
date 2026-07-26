using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task<bool> CompleteScheduledHandoffAsync(
        RobloxWindow window,
        StageMode mode,
        TerminalObservation terminal,
        RepeatedRoutePreparationState preparation,
        IDetectorPack detector,
        char playMenuKey,
        bool autoRecover,
        int stableDetections,
        StageRunOutcome outcome,
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
                outcome == StageRunOutcome.Victory ? 1 : 0,
                outcome == StageRunOutcome.Defeat ? 1 : 0,
                runtime,
                cancellationToken).ConfigureAwait(false);
        switch (continuation)
        {
            case ScheduledTaskContinuation.RepeatStage:
                (int X, int Y)? repeat =
                    StageScreenDetector.RepeatStageAction(
                        terminal.Frame,
                        terminal.State);
                if (repeat is null)
                {
                    throw new RobloxUiUnavailableException(
                        $"The {Label(mode)} Repeat Stage button could not be located.");
                }
                report(
                    "Handoff",
                    100,
                    $"The same {Label(mode)} route is next. Repeating the stage.",
                    "repeat_stage",
                    terminal.Confidence);
                log(
                    $"The scheduler kept the same {Label(mode)} route; using Repeat Stage.",
                    MacroEventLevel.Success,
                    "repeat_stage",
                    terminal.Confidence);
                await ClickAsync(
                    window,
                    repeat.Value.X,
                    repeat.Value.Y,
                    cancellationToken).ConfigureAwait(false);
                preparation.MarkRepeatStageRequested();
                await WaitForStateAsync(
                    window,
                    StageScreenState.Prestart,
                    TimeSpan.FromSeconds(45),
                    detector,
                    stableDetections,
                    cancellationToken).ConfigureAwait(false);
                return true;
            case ScheduledTaskContinuation.ReturnToLobby:
                report(
                    "Handoff",
                    100,
                    "An Event route is next. Returning to the Lobby.",
                    "return_to_lobby",
                    terminal.Confidence);
                log(
                    $"The next scheduled route is Event-only; returning from {Label(mode)} directly to the Lobby.",
                    MacroEventLevel.Information,
                    "return_to_lobby",
                    terminal.Confidence);
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
                bool recovered =
                    await EnsureGameModeSelectorAsync(
                        window,
                        mode,
                        playMenuKey,
                        detector,
                        autoRecover,
                        stableDetections,
                        report,
                        log,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (recovered)
                {
                    preparation.Invalidate();
                }
                return false;
            default:
                throw new InvalidOperationException(
                    "The stage handoff policy returned an unknown continuation.");
        }
    }
}
