using System.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task<ChallengeTerminal>
        RunSelectedChallengeAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeType type,
        ChallengeMapId map,
        ChallengeMapRuntimeModels models,
        ManualInputRecording? manualRecording,
        (
            ImageFrame Frame,
            ChallengeScreenMatch Match)
            availableObservation,
        IDetectorPack detector,
        DiscordRunReporter reporter,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        Stopwatch runtime,
        int priorVictories,
        int priorDefeats,
        Action<int> retriesChanged,
        Action<
            string,
            MacroEventLevel,
            string?,
            double?> log,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        TeamOperationSession? teamSession,
        CancellationToken cancellationToken)
    {
        int victories = 0;
        int defeats = 0;
        int retry = 0;
        ChallengeMapProfile profile =
            preset.Maps.Single(value =>
                value.Map == map);
        await ClickAvailableStageAsync(
                window,
                preset,
                detector,
                availableObservation,
                report,
                cancellationToken)
            .ConfigureAwait(false);
        (_, ChallengeScreenMatch preview) =
            await WaitForPreviewStartAsync(
                    window,
                    preset,
                    detector,
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        await ClickAsync(
                window,
                preview.ActionX!.Value,
                preview.ActionY!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        bool attemptNotified = false;
        bool teamLoaded =
            profile.TeamSlot == 0 ||
            teamSession?.IsLoaded(
                window,
                profile.TeamSlot) == true;
        bool skipRepeatedPrestart = false;
        while (true)
        {
            bool skipThisPrestart =
                skipRepeatedPrestart;
            skipRepeatedPrestart = false;
            (ImageFrame prestart, teamLoaded) =
                await PrepareSelectedChallengeAttemptAsync(
                        window,
                        preset,
                        detector,
                        type,
                        map,
                        profile,
                        teamLoaded,
                        skipThisPrestart,
                        unitMenuKey,
                        report,
                        log,
                        teamSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            (
                Stopwatch matchRuntime,
                IReadOnlyList<PlacementStep>
                    configuredAfterStart) =
                await BeginConfiguredMatchAsync(
                        window,
                        preset,
                        models,
                        manualRecording,
                        prestart,
                        detector,
                        () =>
                        {
                            if (attemptNotified)
                            {
                                return;
                            }
                            reporter.Queue(
                                "attempt",
                                $"Starting the {Label(type)} Challenge on {Label(map)}.",
                                prestart,
                                runtime.Elapsed,
                                priorVictories,
                                priorDefeats,
                                new DiscordRunTarget(
                                    (int)map,
                                    0,
                                    ChallengeRoute(
                                        type,
                                        map)));
                            attemptNotified = true;
                        },
                        report,
                        log,
                        cancelPlacementKey,
                        cancellationToken)
                    .ConfigureAwait(false);
            MatchTerminal terminal =
                await MonitorMatchAsync(
                        window,
                        preset,
                        models,
                        configuredAfterStart,
                        detector,
                        matchRuntime,
                        log,
                        report,
                        cancelPlacementKey,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (terminal.State ==
                ChallengeScreenState.Victory)
            {
                victories++;
                string detail =
                    $"{Label(type)} on {Label(map)} ended in Victory.";
                log(
                    detail,
                    MacroEventLevel.Success,
                    "victory",
                    terminal.Confidence);
                reporter.Queue(
                    "victory",
                    detail,
                    terminal.Frame,
                    runtime.Elapsed,
                    priorVictories + victories,
                    priorDefeats + defeats,
                    new DiscordRunTarget(
                        (int)map,
                        0,
                        ChallengeRoute(
                            type,
                            map)),
                    matchRuntime:
                        matchRuntime.Elapsed);
                await ReturnFromTerminalAsync(
                        window,
                        preset,
                        detector,
                        playMenuKey,
                        report,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new ChallengeTerminal(
                    victories,
                    defeats);
            }

            defeats++;
            bool willRetry =
                ChallengeRunPolicy.TerminalContinuation(
                    victory: false,
                    retry,
                    preset.DefeatRetries) ==
                ChallengeTerminalContinuation
                    .RepeatStage;
            string defeatDetail = willRetry
                ? $"{Label(type)} on {Label(map)} ended in Defeat. Retry {retry + 1} of {preset.DefeatRetries} will start."
                : $"{Label(type)} on {Label(map)} ended in Defeat. The retry limit was reached.";
            log(
                defeatDetail,
                MacroEventLevel.Warning,
                "defeat",
                terminal.Confidence);
            reporter.Queue(
                "defeat",
                defeatDetail,
                terminal.Frame,
                runtime.Elapsed,
                priorVictories + victories,
                priorDefeats + defeats,
                new DiscordRunTarget(
                    (int)map,
                    0,
                    ChallengeRoute(
                        type,
                        map)),
                matchRuntime:
                    matchRuntime.Elapsed);
            if (willRetry)
            {
                retry++;
                retriesChanged(1);
                report(
                    "Retry",
                    0,
                    $"Retrying after defeat ({retry}/{preset.DefeatRetries}).",
                    "defeat",
                    terminal.Confidence);
                skipRepeatedPrestart =
                    await RetryDefeatAsync(
                            window,
                            preset,
                            detector,
                            terminal.Frame,
                            models.Placement,
                            report,
                            cancellationToken)
                        .ConfigureAwait(false);
                continue;
            }

            log(
                "Defeat retry limit reached. This Challenge will not be attempted again until the next global reset.",
                MacroEventLevel.Information,
                null,
                null);
            await ReturnFromTerminalAsync(
                    window,
                    preset,
                    detector,
                    playMenuKey,
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
            return new ChallengeTerminal(
                victories,
                defeats);
        }
    }
}
