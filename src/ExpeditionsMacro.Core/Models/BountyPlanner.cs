namespace ExpeditionsMacro.Core.Models;

public enum BountyChallengeAvailability
{
    Available,
    Cooldown,
    DailyLimit,
}

public static class BountyPlanner
{
    public static bool HasEveryRetainableBounty(
        IReadOnlySet<int> observed,
        IReadOnlySet<int> unavailableToday,
        int parkedNonViable,
        int parkedLimit,
        BountyChallengeAvailability challengeAvailability)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(unavailableToday);
        bool hasEveryViable =
            BountyCatalog.All
                .Where(definition =>
                    !unavailableToday.Contains(
                        definition.Number) &&
                    !definition.AlwaysReroll &&
                    !(definition.ChallengeConditional &&
                        challengeAvailability ==
                            BountyChallengeAvailability
                                .DailyLimit))
                .All(definition =>
                    observed.Contains(
                        definition.Number));
        int availableNonViable =
            BountyCatalog.All.Count(definition =>
                definition.AlwaysReroll &&
                !unavailableToday.Contains(
                    definition.Number));
        int requiredParked =
            Math.Min(
                parkedLimit,
                availableNonViable);
        return hasEveryViable &&
            parkedNonViable >= requiredParked;
    }

    public static bool ShouldReroll(
        int bountyNumber,
        IReadOnlySet<int> unavailableToday,
        int parkedNonViable,
        int parkedLimit,
        BountyChallengeAvailability challengeAvailability)
    {
        ArgumentNullException.ThrowIfNull(unavailableToday);
        if (unavailableToday.Contains(bountyNumber))
        {
            return true;
        }
        BountyDefinition definition =
            BountyCatalog.For(bountyNumber);
        if (definition.ChallengeConditional)
        {
            return challengeAvailability switch
            {
                BountyChallengeAvailability.Cooldown => false,
                BountyChallengeAvailability.DailyLimit => true,
                _ => false,
            };
        }
        return definition.AlwaysReroll &&
            parkedNonViable >= parkedLimit;
    }

    public static IReadOnlyList<BountyWorkRoute> BuildRoutes(
        IReadOnlyList<BountyActiveProgress> active,
        BountyChallengeAvailability challengeAvailability)
    {
        ArgumentNullException.ThrowIfNull(active);
        List<(BountyWorkRoute Route, int Coverage)> routes = [];

        AddRaidRoute();
        AddChallengeRoute();
        foreach (IGrouping<ChallengeMapId, (
                     BountyActiveProgress Active,
                     BountyObjective Objective)> group in
                 IncompleteObjectives()
                     .Where(value =>
                         value.Objective.Kind ==
                         BountyObjectiveKind.InfiniteWave)
                     .GroupBy(value =>
                         value.Objective.Map!.Value))
        {
            int target = group.Max(value =>
                value.Objective.TargetWave);
            int[] covered = group
                .Where(value =>
                    value.Objective.TargetWave <= target)
                .Select(value => value.Active.Number)
                .Distinct()
                .Order()
                .ToArray();
            routes.Add((
                new BountyWorkRoute
                {
                    Kind = BountyObjectiveKind.InfiniteWave,
                    Map = group.Key,
                    TargetWave = target,
                    CoveredBounties = covered,
                },
                covered.Length));
        }

        return routes
            .OrderByDescending(value => value.Coverage)
            .ThenBy(value => Cost(value.Route))
            .Select(value => value.Route)
            .ToArray();

        IEnumerable<(
            BountyActiveProgress Active,
            BountyObjective Objective)> IncompleteObjectives()
        {
            foreach (BountyActiveProgress progress in active)
            {
                BountyDefinition definition =
                    BountyCatalog.For(progress.Number);
                if (definition.AlwaysReroll ||
                    definition.ChallengeConditional &&
                    challengeAvailability ==
                    BountyChallengeAvailability.DailyLimit)
                {
                    continue;
                }
                foreach (BountyObjective objective in
                         definition.Objectives)
                {
                    if (progress.ProgressFor(objective.Key) <
                        objective.RequiredCount)
                    {
                        yield return (progress, objective);
                    }
                }
            }
        }

        void AddRaidRoute()
        {
            int[] covered = IncompleteObjectives()
                .Where(value =>
                    value.Objective.Kind ==
                    BountyObjectiveKind.RaidActOne)
                .Select(value => value.Active.Number)
                .Distinct()
                .Order()
                .ToArray();
            if (covered.Length == 0)
            {
                return;
            }
            routes.Add((
                new BountyWorkRoute
                {
                    Kind = BountyObjectiveKind.RaidActOne,
                    CoveredBounties = covered,
                },
                covered.Length));
        }

        void AddChallengeRoute()
        {
            if (challengeAvailability !=
                BountyChallengeAvailability.Available)
            {
                return;
            }
            (BountyActiveProgress Active, BountyObjective Objective)[]
                objectives = IncompleteObjectives()
                    .Where(value =>
                        value.Objective.Kind ==
                        BountyObjectiveKind.Challenge)
                    .ToArray();
            if (objectives.Length == 0)
            {
                return;
            }
            int remaining = objectives.Max(value =>
                value.Objective.RequiredCount -
                value.Active.ProgressFor(
                    value.Objective.Key));
            int[] covered = objectives
                .Select(value => value.Active.Number)
                .Distinct()
                .Order()
                .ToArray();
            routes.Add((
                new BountyWorkRoute
                {
                    Kind = BountyObjectiveKind.Challenge,
                    ChallengeRuns = remaining,
                    CoveredBounties = covered,
                },
                covered.Length));
        }
    }

    public static IReadOnlyList<BountyActiveProgress>
        ApplyCompletedRoute(
        IReadOnlyList<BountyActiveProgress> active,
        BountyWorkRoute route,
        int completedChallengeRuns = 0)
    {
        return active
            .Select(progress =>
                Apply(progress, route, completedChallengeRuns))
            .ToArray();
    }

    public static bool IsComplete(
        BountyActiveProgress progress)
    {
        BountyDefinition definition =
            BountyCatalog.For(progress.Number);
        return definition.HasWork &&
            definition.Objectives.All(objective =>
                progress.ProgressFor(objective.Key) >=
                objective.RequiredCount);
    }

    public static bool HasClaimableBounty(
        IReadOnlyList<BountyActiveProgress> active)
    {
        ArgumentNullException.ThrowIfNull(active);
        return active.Any(IsComplete);
    }

    public static bool HasExecutableWork(
        IReadOnlyList<BountyActiveProgress> active,
        BountyChallengeAvailability
            challengeAvailability) =>
        BuildRoutes(
            active,
            challengeAvailability).Count > 0;

    private static BountyActiveProgress Apply(
        BountyActiveProgress progress,
        BountyWorkRoute route,
        int completedChallengeRuns)
    {
        if (!route.CoveredBounties.Contains(
                progress.Number))
        {
            return progress;
        }
        BountyDefinition definition =
            BountyCatalog.For(progress.Number);
        Dictionary<string, int> updated =
            new(progress.ObjectiveProgress);
        foreach (BountyObjective objective in
                 definition.Objectives)
        {
            bool covered = route.Kind switch
            {
                BountyObjectiveKind.RaidActOne =>
                    objective.Kind ==
                    BountyObjectiveKind.RaidActOne,
                BountyObjectiveKind.InfiniteWave =>
                    objective.Kind ==
                        BountyObjectiveKind.InfiniteWave &&
                    objective.Map == route.Map &&
                    objective.TargetWave <=
                        route.TargetWave,
                BountyObjectiveKind.Challenge =>
                    objective.Kind ==
                    BountyObjectiveKind.Challenge,
                _ => false,
            };
            if (!covered)
            {
                continue;
            }
            int value = route.Kind ==
                    BountyObjectiveKind.Challenge
                ? Math.Min(
                    objective.RequiredCount,
                    progress.ProgressFor(
                        objective.Key) +
                    completedChallengeRuns)
                : objective.RequiredCount;
            updated[objective.Key] = value;
        }
        return progress with
        {
            ObjectiveProgress = updated,
        };
    }

    private static int Cost(BountyWorkRoute route) =>
        route.Kind switch
        {
            BountyObjectiveKind.Challenge =>
                route.ChallengeRuns * 20,
            BountyObjectiveKind.RaidActOne => 35,
            BountyObjectiveKind.InfiniteWave =>
                route.TargetWave,
            _ => int.MaxValue,
        };
}
