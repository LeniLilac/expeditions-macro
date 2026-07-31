using System.Diagnostics;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task<(ImageFrame Prestart,
        bool TeamLoaded)>
        PrepareSelectedChallengeAttemptAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        ChallengeType type,
        ChallengeMapId map,
        ChallengeMapProfile profile,
        bool teamLoaded,
        bool skipRepeatedPrestart,
        char? unitMenuKey,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        TeamOperationSession? teamSession,
        CancellationToken cancellationToken)
    {
        ImageFrame prestart =
            skipRepeatedPrestart
                ? CaptureClient(window, detector)
                : await WaitForPrestartAfterPreviewAsync(
                        window,
                        preset,
                        detector,
                        report,
                        cancellationToken)
                    .ConfigureAwait(false);
        if (!teamLoaded)
        {
            teamSession?.BeginSelection(
                profile.TeamSlot);
            await _teams.SelectAsync(
                    window,
                    profile.TeamSlot,
                    unitMenuKey!.Value,
                    progress: null,
                    cancellationToken)
                .ConfigureAwait(false);
            teamLoaded = true;
            teamSession?.MarkLoaded(
                window,
                profile.TeamSlot);
            prestart = await WaitForScreenAsync(
                    window,
                    preset,
                    detector,
                    ChallengeScreenState.Prestart,
                    TimeSpan.FromSeconds(10),
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (!skipRepeatedPrestart)
        {
            report(
                "Camera preparation",
                20,
                $"Preparing {Label(map)} for {Label(type)}.",
                "prestart",
                null);
            await PrepareCameraAsync(
                    window,
                    preset,
                    report,
                    log,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            log(
                "Advanced Recording Mode reused the preserved Challenge camera and skipped repeated Start-screen verification.",
                MacroEventLevel.Information,
                "recording_repeat_delay",
                null);
        }
        return (prestart, teamLoaded);
    }

    private async Task ClickAvailableStageAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        (
            ImageFrame Frame,
            ChallengeScreenMatch Match) initialObservation,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        CancellationToken cancellationToken)
    {
        (ImageFrame Frame, ChallengeScreenMatch Match)?
            observation =
                await TryWaitForActionAsync(
                        window,
                        preset,
                        detector,
                        ChallengeScreenState
                            .ChallengeAvailable,
                        TimeSpan.FromSeconds(5),
                        report,
                        cancellationToken,
                        initialObservation)
                    .ConfigureAwait(false);
        if (observation is null)
        {
            throw new RobloxUiUnavailableException(
                "The Challenge Select Stage button disappeared before it could be clicked.");
        }

        await ClickAsync(
                window,
                observation.Value.Match.ActionX!.Value,
                observation.Value.Match.ActionY!.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(Stopwatch Runtime,
        IReadOnlyList<PlacementStep> AfterStart)>
        BeginConfiguredMatchAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeMapRuntimeModels models,
        ManualInputRecording? manualRecording,
        ImageFrame prestart,
        IDetectorPack detector,
        Action attemptStarted,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        await new RobloxChatPanelNormalizer(_automation)
            .EnsureClosedAsync(
                window,
                cancellationToken)
            .ConfigureAwait(false);
        _placements.BeginMatch();
        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                models.Placement);
        ChallengePlacementPartition? partition =
            await PlaceVisiblePrestartAsync(
                    window,
                    preset,
                    models.Placement,
                    execution.BeforeStart,
                    prestart,
                    report,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);

        bool requireStartAction =
            !execution.ManualPlayback ||
            ManualPlaybackStartPolicy.RequiresPrestart(
                models.Placement);
        (int X, int Y)? start = null;
        if (requireStartAction)
        {
            start =
                await LocateActionAfterParkingAsync(
                    token =>
                        _automation.ParkCursorAsync(
                            window,
                            token),
                    () => CaptureClient(
                        window,
                        detector),
                    frame =>
                        ChallengeScreenDetector.ActionFor(
                            ChallengeScreenState.Prestart,
                            frame),
                    retryMilliseconds: 100,
                    maximumAttempts: 3,
                    cancellationToken,
                    softTimeout: TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
            if (start is null)
            {
                throw new RobloxUiUnavailableException(
                    "The Challenge Start Game button disappeared before it could be clicked.");
            }
        }

        Stopwatch runtime;
        if (execution.ManualPlayback)
        {
            if (_manualInputs is null ||
                manualRecording is null)
            {
                throw new InvalidOperationException(
                    "Manual input playback is unavailable.");
            }
            await ManualPlaybackStartPolicy
                .WaitBeforePlaybackAsync(
                    models.Placement!,
                    message => report(
                        "Recording playback",
                        50,
                        message,
                        null,
                        null),
                    cancellationToken)
                .ConfigureAwait(false);
            runtime =
                await ManualInputMatchPlayback.PlayAsync(
                    _manualInputs,
                    window,
                    manualRecording,
                    new Progress<MacroProgress>(
                        value => report(
                            value.Phase,
                            value.Percent,
                            value.Message,
                            value.DetectedState,
                            value.Confidence)),
                    attemptStarted,
                    cancellationToken)
                .ConfigureAwait(false);
            return (
                runtime,
                execution.AfterStart);
        }

        runtime = Stopwatch.StartNew();
        attemptStarted();
        await ClickAsync(
            window,
            start!.Value.X,
            start.Value.Y,
                cancellationToken)
            .ConfigureAwait(false);
        if (partition is
            { AfterStart.Count: > 0 } &&
            models.Placement is not null)
        {
            await Task.Delay(
                    550,
                    cancellationToken)
                .ConfigureAwait(false);
            report(
                "Placement",
                50,
                $"Placing {partition.AfterStart.Count} unit(s) that were covered by the Start Game dialog.",
                null,
                null);
            await PlaceAsync(
                    window,
                    preset,
                    models.Placement,
                    partition.AfterStart,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await Task.Delay(
                2200,
                cancellationToken)
            .ConfigureAwait(false);
        return (
            runtime,
            execution.AfterStart);
    }

    private async Task<ChallengePlacementPartition?>
        PlaceVisiblePrestartAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel? placement,
        IReadOnlyList<PlacementStep> steps,
        ImageFrame prestart,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        if (placement is null ||
            steps.Count == 0)
        {
            return null;
        }

        ScreenRegion occlusion =
            ChallengeScreenDetector.PrestartOcclusion(
                prestart) ??
            throw new RobloxUiUnavailableException(
                "The Challenge Start Game dialog could not be measured before placement.");
        ChallengePlacementPartition partition =
            ChallengeRunPolicy.PartitionPrestartPlacements(
                steps,
                occlusion);
        report(
            "Placement",
            45,
            "Placing units outside the Start Game dialog.",
            null,
            null);
        if (partition.BeforeStart.Count > 0)
        {
            await PlaceAsync(
                    window,
                    preset,
                    placement,
                    partition.BeforeStart,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (partition.AfterStart.Count > 0)
        {
            log(
                $"Deferred {partition.AfterStart.Count} before-start placement(s) hidden by the Start Game dialog.",
                MacroEventLevel.Information,
                null,
                null);
        }
        return partition;
    }
}
