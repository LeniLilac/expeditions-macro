using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<ScheduledTaskResult>
        ExecuteBountyAsync(
        MacroTaskDefinition task,
        string webhook,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        ChallengeRotationState challengeRotation,
        BountyOperationSession bountySession,
        TeamOperationSession teamSession,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        IDetectorPack detector =
            await LoadDetectorAsync(
                    AnimeExpeditionsDetectorSpec.PackId,
                    cancellationToken)
                .ConfigureAwait(false);
        challengeRotation.Advance(
            DateTimeOffset.Now);
        BountyChallengeAvailability availability =
            challengeRotation
                .DailyLimitUntilUtc is
                DateTimeOffset dailyUntil &&
            dailyUntil >
                DateTimeOffset.UtcNow
                ? BountyChallengeAvailability
                    .DailyLimit
                : challengeRotation
                        .PreviousAllCooldownEpoch ==
                    challengeRotation.Epoch
                    ? BountyChallengeAvailability
                        .Cooldown
                    : BountyChallengeAvailability
                        .Available;
        DateTimeOffset? nextChallenge =
            availability ==
                BountyChallengeAvailability
                    .DailyLimit
                ? challengeRotation
                    .DailyLimitUntilUtc
                : ChallengeRunPolicy
                    .NextGlobalReset(
                        DateTimeOffset.Now)
                    .ToUniversalTime();

        BountyRunResult result =
            await _services.Bounties.RunAsync(
                bountySession,
                BountyPlanner
                    .AutomaticParkedNonViableLimit,
                availability,
                nextChallenge,
                detector,
                (route, token) =>
                    ExecuteBountyRouteAsync(
                        task,
                        route,
                        detector,
                        webhook,
                        playMenuKey,
                        unitMenuKey,
                        cancelPlacementKey,
                        macroTotals,
                        challengeRotation,
                        teamSession,
                        progress,
                        token),
                progress,
                entry => DispatchLog(entry),
                cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset? next =
            result.NextEligibleAtUtc;
        if (next is null &&
            !result.RetryOnNextMacroStart)
        {
            next = DateTimeOffset.UtcNow +
                SafeSkipDelay;
        }
        return new ScheduledTaskResult(
            0,
            0,
            result.Runtime,
            next,
            Skipped: true,
            SkipUntilSchedulerRestart:
                result.RetryOnNextMacroStart,
            ChallengeRotation:
                challengeRotation.Snapshot());
    }

    private Task<BountyRouteExecutionResult>
        ExecuteBountyRouteAsync(
        MacroTaskDefinition bountyTask,
        BountyWorkRoute route,
        IDetectorPack detector,
        string webhook,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        ChallengeRotationState challengeRotation,
        TeamOperationSession teamSession,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken) =>
        route.Kind switch
        {
            BountyObjectiveKind.RaidActOne =>
                ExecuteBountyRaidAsync(
                    bountyTask,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    cancelPlacementKey,
                    macroTotals,
                    teamSession,
                    progress,
                    cancellationToken),
            BountyObjectiveKind.StoryActOneHard =>
                ExecuteBountyStoryHardAsync(
                    bountyTask,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    cancelPlacementKey,
                    macroTotals,
                    teamSession,
                    progress,
                    cancellationToken),
            BountyObjectiveKind.InfiniteWave =>
                ExecuteBountyInfiniteAsync(
                    bountyTask,
                    route,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    cancelPlacementKey,
                    macroTotals,
                    teamSession,
                    progress,
                    cancellationToken),
            BountyObjectiveKind.Challenge =>
                ExecuteBountyChallengesAsync(
                    bountyTask,
                    route,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    cancelPlacementKey,
                    macroTotals,
                    challengeRotation,
                    teamSession,
                    progress,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(route)),
        };

    private async Task<BountyRouteExecutionResult>
        ExecuteBountyRaidAsync(
        MacroTaskDefinition bountyTask,
        IDetectorPack detector,
        string webhook,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        TeamOperationSession teamSession,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        MacroTaskDefinition task =
            RouteTask(
                bountyTask,
                MacroTaskKind.Raid,
                new PlacementTarget
                {
                    Mode =
                        PlacementTargetMode.Raid,
                    MapNumber = 1,
                    ActNumber = 1,
                });
        (RaidPreset preset,
            StageRuntimeModels models) =
            await BuildRaidSetupAsync(
                    task,
                    cancellationToken)
                .ConfigureAwait(false);
        StageRunResult result =
            await _services.Stages.RunRaidAsync(
                    preset,
                    models,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    progress,
                    entry => DispatchLog(entry),
                    cancellationToken,
                    continueScheduledRoute:
                        static (
                            _,
                            _,
                            _,
                            _) =>
                            Task.FromResult(
                                ScheduledTaskContinuation
                                    .ReturnToLobby),
                    macroTotals: macroTotals,
                    cancelPlacementKey:
                        cancelPlacementKey,
                    teamSession:
                        teamSession)
                .ConfigureAwait(false);
        return new()
        {
            Completed =
                result.Outcome ==
                StageRunOutcome.Victory,
            NextEligibleAtUtc =
                result.Outcome ==
                StageRunOutcome.Victory
                    ? null
                    : DateTimeOffset.UtcNow +
                        SafeSkipDelay,
        };
    }

    private async Task<BountyRouteExecutionResult>
        ExecuteBountyStoryHardAsync(
        MacroTaskDefinition bountyTask,
        IDetectorPack detector,
        string webhook,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        TeamOperationSession teamSession,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        MacroTaskDefinition task =
            RouteTask(
                bountyTask,
                MacroTaskKind.Story,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Story,
                    MapNumber =
                        (int)ChallengeMapId.SchoolGrounds,
                    StoryRunKind = StoryRunKind.Act,
                    ActNumber = 1,
                },
                hardMode: true);
        (StoryPreset preset,
            StageRuntimeModels models) =
            await BuildStorySetupAsync(
                    task,
                    cancellationToken)
                .ConfigureAwait(false);
        StageRunResult result =
            await _services.Stages.RunStoryAsync(
                    preset,
                    models,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    progress,
                    entry => DispatchLog(entry),
                    cancellationToken,
                    continueScheduledRoute:
                        static (
                            _,
                            _,
                            _,
                            _) =>
                            Task.FromResult(
                                ScheduledTaskContinuation
                                    .ReturnToLobby),
                    macroTotals: macroTotals,
                    cancelPlacementKey:
                        cancelPlacementKey,
                    teamSession:
                        teamSession)
                .ConfigureAwait(false);
        return new()
        {
            Completed =
                result.Outcome ==
                StageRunOutcome.Victory,
            NextEligibleAtUtc =
                result.Outcome ==
                StageRunOutcome.Victory
                    ? null
                    : DateTimeOffset.UtcNow +
                        SafeSkipDelay,
        };
    }

    private async Task<BountyRouteExecutionResult>
        ExecuteBountyInfiniteAsync(
        MacroTaskDefinition bountyTask,
        BountyWorkRoute route,
        IDetectorPack detector,
        string webhook,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        TeamOperationSession teamSession,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        MacroTaskDefinition task =
            RouteTask(
                bountyTask,
                MacroTaskKind.Story,
                new PlacementTarget
                {
                    Mode =
                        PlacementTargetMode.Story,
                    MapNumber =
                        (int)route.Map!.Value,
                    StoryRunKind =
                        StoryRunKind.Infinite,
                    ActNumber = 1,
                });
        (StoryPreset preset,
            StageRuntimeModels models) =
            await BuildStorySetupAsync(
                    task,
                    cancellationToken)
                .ConfigureAwait(false);
        StageRunResult result =
            await _services.Stages.RunStoryAsync(
                    preset,
                    models,
                    detector,
                    webhook,
                    playMenuKey,
                    unitMenuKey,
                    progress,
                    entry => DispatchLog(entry),
                    cancellationToken,
                    macroTotals: macroTotals,
                    cancelPlacementKey:
                        cancelPlacementKey,
                    waveObjective:
                        new StageWaveObjective
                        {
                            QuestWave =
                                route.TargetWave,
                        },
                    teamSession:
                        teamSession)
                .ConfigureAwait(false);
        return new()
        {
            Completed =
                result.Outcome ==
                StageRunOutcome
                    .ObjectiveComplete,
            NextEligibleAtUtc =
                result.Outcome ==
                StageRunOutcome
                    .ObjectiveComplete
                    ? null
                    : DateTimeOffset.UtcNow +
                        SafeSkipDelay,
        };
    }

    private async Task<BountyRouteExecutionResult>
        ExecuteBountyChallengesAsync(
        MacroTaskDefinition bountyTask,
        BountyWorkRoute route,
        IDetectorPack detector,
        string webhook,
        char playMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        MacroRunTotals macroTotals,
        ChallengeRotationState challengeRotation,
        TeamOperationSession teamSession,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        MacroTaskDefinition task =
            RouteTask(
                bountyTask,
                MacroTaskKind.Challenge,
                target: null);
        (ChallengePreset preset,
            IReadOnlyDictionary<
                ChallengeMapId,
                ChallengeMapRuntimeModels> models) =
            await BuildChallengeSetupAsync(
                    task,
                    cancellationToken,
                    useStoryInfinitePlacements: true)
                .ConfigureAwait(false);
        ChallengeRunSummary? summary = null;
        await _services.Challenges.RunAsync(
                preset,
                models,
                detector,
                challengeRotation,
                webhook,
                playMenuKey,
                progress,
                entry => DispatchLog(entry),
                value => summary = value,
                cancellationToken,
                maximumCompletedRuns:
                    route.ChallengeRuns,
                returnWhenUnavailable: true,
                unitMenuKey: unitMenuKey,
                macroTotals: macroTotals,
                cancelPlacementKey:
                    cancelPlacementKey,
                teamSession:
                    teamSession)
            .ConfigureAwait(false);
        ChallengeRunSummary actual =
            summary ??
            throw new InvalidOperationException(
                "Bounty Challenge work returned without a summary.");
        bool daily =
            challengeRotation.DailyLimitUntilUtc >
            DateTimeOffset.UtcNow;
        bool complete =
            actual.Completed >=
            route.ChallengeRuns;
        return new()
        {
            Completed = complete,
            CompletedChallengeRuns =
                actual.Completed,
            ChallengeAvailability = complete
                ? BountyChallengeAvailability
                    .Available
                : daily
                    ? BountyChallengeAvailability
                        .DailyLimit
                    : BountyChallengeAvailability
                        .Cooldown,
            NextEligibleAtUtc =
                actual.WaitingUntilUtc,
        };
    }

    private static MacroTaskDefinition RouteTask(
        MacroTaskDefinition bountyTask,
        MacroTaskKind kind,
        PlacementTarget? target,
        bool hardMode = false) =>
        new()
        {
            Id =
                $"{bountyTask.Id}-{kind.ToString().ToLowerInvariant()}",
            Kind = kind,
            Name = $"Bounty {kind}",
            PlacementTarget = target,
            HardMode = hardMode,
            DefeatRetries =
                bountyTask.DefeatRetries,
        };
}
