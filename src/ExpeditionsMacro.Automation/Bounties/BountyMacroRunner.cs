using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Bounties;

public sealed record BountyRouteExecutionResult
{
    public required bool Completed { get; init; }
    public int CompletedChallengeRuns { get; init; }
    public BountyChallengeAvailability?
        ChallengeAvailability
    { get; init; }
    public DateTimeOffset? NextEligibleAtUtc { get; init; }
}

public sealed record BountyRunResult
{
    public required TimeSpan Runtime { get; init; }
    public required int ClaimedToday { get; init; }
    public bool GoldUnavailable { get; init; }
    public bool RetryOnNextMacroStart { get; init; }
    public DateTimeOffset? NextEligibleAtUtc { get; init; }
}

public sealed class BountyMacroRunner
{
    private readonly IRobloxAutomation _automation;
    private readonly BountyStateRepository _states;
    private readonly BountyBoardNavigator _navigator;
    private readonly BountyBoardProcessor _processor;

    public BountyMacroRunner(
        IRobloxAutomation automation,
        BountyStateRepository states)
    {
        _automation = automation;
        _states = states;
        _navigator = new BountyBoardNavigator(
            automation);
        _processor = new BountyBoardProcessor(
            _navigator);
    }

    public async Task<BountyRunResult> RunAsync(
        int parkedNonViableLimit,
        BountyChallengeAvailability
            challengeAvailability,
        DateTimeOffset?
            challengeNextEligibleAtUtc,
        IDetectorPack detector,
        Func<
            BountyWorkRoute,
            CancellationToken,
            Task<BountyRouteExecutionResult>>
            executeRoute,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (parkedNonViableLimit is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parkedNonViableLimit));
        }
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(executeRoute);

        Stopwatch runtime = Stopwatch.StartNew();
        BountyProgressState state =
            (await _states.LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false))
            .AdvanceDay(DateTimeOffset.UtcNow);
        bool noGold = false;
        RobloxWindow window =
            _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");

        void Write(
            string message,
            MacroEventLevel level =
                MacroEventLevel.Information,
            string stateName = "bounty") =>
            log?.Invoke(
                new MacroEvent(
                    DateTimeOffset.Now,
                    level,
                    message,
                    stateName));
        void Report(
            string message,
            int percent,
            string stateName = "bounty") =>
            progress?.Report(
                new MacroProgress(
                    "Bounty",
                    percent,
                    message,
                    stateName));

        while (true)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            state = state.AdvanceDay(
                DateTimeOffset.UtcNow);
            if (state.ClaimedToday >=
                BountyCatalog.DailyClaimLimit)
            {
                return Result(
                    state,
                    runtime.Elapsed,
                    noGold,
                    retryOnNextStart: false,
                    NextUtcDay());
            }

            Report(
                "Opening the Bounty Board.",
                5,
                "bounty_navigation");
            await _navigator.OpenAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            BountyBoardProcessingResult processed =
                await _processor.ProcessAsync(
                        window,
                        detector,
                        state,
                        parkedNonViableLimit,
                        challengeAvailability,
                        rerollEnabled: !noGold,
                        message => Write(message),
                        cancellationToken)
                    .ConfigureAwait(false);
            state = processed.State;
            noGold |= processed.GoldUnavailable;
            await _states.SaveAsync(
                state,
                cancellationToken).ConfigureAwait(false);

            if (state.ClaimedToday >=
                BountyCatalog.DailyClaimLimit)
            {
                Write(
                    "All 10 daily Bounties have been claimed.",
                    MacroEventLevel.Success,
                    "bounty_daily_complete");
                await _navigator.ReturnToLobbyAsync(
                    window,
                    detector,
                    cancellationToken).ConfigureAwait(false);
                return Result(
                    state,
                    runtime.Elapsed,
                    noGold,
                    retryOnNextStart: false,
                    NextUtcDay());
            }

            IReadOnlyList<BountyWorkRoute> routes =
                BountyPlanner.BuildRoutes(
                    state.Active,
                    challengeAvailability);
            if (routes.Count == 0)
            {
                await _navigator.ReturnToLobbyAsync(
                    window,
                    detector,
                    cancellationToken).ConfigureAwait(false);
                DateTimeOffset? next =
                    NextEligible(
                        state,
                        challengeAvailability,
                        challengeNextEligibleAtUtc);
                if (noGold)
                {
                    Write(
                        "Bounty work is complete for this macro session. Rerolling will retry on the next macro start.",
                        MacroEventLevel.Warning,
                        "bounty_gold_unavailable");
                }
                else if (next is not null)
                {
                    Write(
                        $"Bounty work is waiting until {next.Value.LocalDateTime:t}.",
                        MacroEventLevel.Information,
                        "bounty_waiting");
                }
                return Result(
                    state,
                    runtime.Elapsed,
                    noGold,
                    retryOnNextStart: noGold,
                    next);
            }

            BountyWorkRoute route = routes[0];
            Report(
                Describe(route),
                25,
                "bounty_objective");
            await _navigator.ReturnToLobbyAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            BountyRouteExecutionResult execution =
                await executeRoute(
                    route,
                    cancellationToken).ConfigureAwait(false);
            if (execution.CompletedChallengeRuns > 0)
            {
                state = state with
                {
                    Active =
                        BountyPlanner.ApplyCompletedRoute(
                            state.Active,
                            route,
                            execution
                                .CompletedChallengeRuns),
                    UpdatedAtUtc =
                        DateTimeOffset.UtcNow,
                };
                await _states.SaveAsync(
                    state,
                    cancellationToken).ConfigureAwait(false);
            }
            if (execution.ChallengeAvailability is
                BountyChallengeAvailability updated)
            {
                challengeAvailability = updated;
                challengeNextEligibleAtUtc =
                    execution.NextEligibleAtUtc;
            }
            if (!execution.Completed)
            {
                if (execution.ChallengeAvailability is not null)
                {
                    continue;
                }
                return Result(
                    state,
                    runtime.Elapsed,
                    noGold,
                    retryOnNextStart: noGold,
                    execution.NextEligibleAtUtc);
            }
            if (execution.CompletedChallengeRuns == 0)
            {
                state = state with
                {
                    Active =
                        BountyPlanner.ApplyCompletedRoute(
                            state.Active,
                            route),
                    UpdatedAtUtc =
                        DateTimeOffset.UtcNow,
                };
                await _states.SaveAsync(
                    state,
                    cancellationToken).ConfigureAwait(false);
            }
            Write(
                $"Completed {Describe(route)}.",
                MacroEventLevel.Success,
                "bounty_objective_complete");
        }
    }

    private static BountyRunResult Result(
        BountyProgressState state,
        TimeSpan runtime,
        bool noGold,
        bool retryOnNextStart,
        DateTimeOffset? next) =>
        new()
        {
            Runtime = runtime,
            ClaimedToday = state.ClaimedToday,
            GoldUnavailable = noGold,
            RetryOnNextMacroStart =
                retryOnNextStart,
            NextEligibleAtUtc = next,
        };

    private static DateTimeOffset? NextEligible(
        BountyProgressState state,
        BountyChallengeAvailability availability,
        DateTimeOffset? challengeNext)
    {
        bool challengeBlocked = state.Active
            .Select(active =>
                BountyCatalog.For(
                    active.Number))
            .Any(definition =>
                definition.ChallengeConditional &&
                definition.HasWork);
        return challengeBlocked &&
            availability ==
                BountyChallengeAvailability.Cooldown
            ? challengeNext
            : null;
    }

    private static DateTimeOffset NextUtcDay()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;
        return BountyProgressState.UtcDay(now)
            .AddDays(1);
    }

    private static string Describe(
        BountyWorkRoute route) =>
        route.Kind switch
        {
            BountyObjectiveKind.RaidActOne =>
                "Spirit City Raid Act 1",
            BountyObjectiveKind.InfiniteWave =>
                $"{Label(route.Map!.Value)} Infinite through wave {route.TargetWave + 2}",
            BountyObjectiveKind.Challenge =>
                $"{route.ChallengeRuns} regular Challenge run(s)",
            _ => throw new ArgumentOutOfRangeException(
                nameof(route)),
        };

    private static string Label(
        ChallengeMapId map) =>
        map switch
        {
            ChallengeMapId.SchoolGrounds =>
                "School Grounds",
            ChallengeMapId.FlowerForest =>
                "Flower Forest",
            ChallengeMapId.RoseKingdom =>
                "Rose Kingdom",
            ChallengeMapId.FairyKingForest =>
                "Fairy King Forest",
            ChallengeMapId.KingsTomb =>
                "King's Tomb",
            _ => throw new ArgumentOutOfRangeException(
                nameof(map)),
        };
}
