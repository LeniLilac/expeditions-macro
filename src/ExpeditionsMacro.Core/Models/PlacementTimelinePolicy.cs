namespace ExpeditionsMacro.Core.Models;

public static class PlacementTimelinePolicy
{
    public static PlacementStep CreateStartGameStep() =>
        new()
        {
            Kind = MatchStepKind.StartGame,
            UnitKey = 0,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            Phase = PlacementPhase.BeforeStart,
        };

    public static PlacementModel Normalize(
        PlacementModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model with
        {
            Steps = NormalizeSteps(model.Steps),
        };
    }

    public static IReadOnlyList<PlacementStep>
        NormalizeSteps(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        int startCount = steps.Count(
            step =>
                step.Kind == MatchStepKind.StartGame);
        if (startCount > 1)
        {
            throw new InvalidDataException(
                "A placement setup can contain only one Start Game step.");
        }
        IReadOnlyList<PlacementStep> timeline =
            startCount == 1
                ? DeriveLegacyPhases(steps)
                : MigrateLegacyPhases(steps);
        return PlacementReferencePolicy.Normalize(
            timeline);
    }

    public static int StartGameIndex(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        for (int index = 0;
             index < steps.Count;
             index++)
        {
            if (steps[index].Kind ==
                MatchStepKind.StartGame)
            {
                return index;
            }
        }
        return -1;
    }

    public static int NewActionInsertionIndex(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (StartGameIndex(steps) < 0)
        {
            throw new InvalidOperationException(
                "The required Start Game step is missing.");
        }
        return steps.Count;
    }

    public static bool IsBeforeStart(
        IReadOnlyList<PlacementStep> steps,
        int index)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (index < 0 || index >= steps.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index));
        }
        int start = StartGameIndex(steps);
        return start < 0
            ? steps[index].Phase ==
                PlacementPhase.BeforeStart
            : index < start;
    }

    public static void ValidateStructure(
        IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        int count = steps.Count(
            step =>
                step.Kind == MatchStepKind.StartGame);
        if (count > 1)
        {
            throw new InvalidDataException(
                "A placement setup can contain only one Start Game step.");
        }
    }

    private static IReadOnlyList<PlacementStep>
        DeriveLegacyPhases(
        IReadOnlyList<PlacementStep> steps)
    {
        List<PlacementStep> normalized =
            new(steps.Count);
        bool afterStart = false;
        foreach (PlacementStep step in steps)
        {
            if (step.Kind ==
                MatchStepKind.StartGame)
            {
                normalized.Add(
                    CreateStartGameStep());
                afterStart = true;
                continue;
            }
            normalized.Add(step with
            {
                Phase = afterStart
                    ? PlacementPhase.AfterStart
                    : PlacementPhase.BeforeStart,
                DelayAfterStartMilliseconds = 0,
            });
        }
        return normalized;
    }

    private static IReadOnlyList<PlacementStep>
        MigrateLegacyPhases(
        IReadOnlyList<PlacementStep> steps)
    {
        PlacementStep[] before = steps
            .Where(step =>
                step.Phase ==
                PlacementPhase.BeforeStart)
            .Select(step => step with
            {
                Phase = PlacementPhase.BeforeStart,
                DelayAfterStartMilliseconds = 0,
            })
            .ToArray();
        PlacementStep[] after = steps
            .Select((step, index) =>
                new
                {
                    Step = step,
                    Index = index,
                })
            .Where(item =>
                item.Step.Phase ==
                PlacementPhase.AfterStart)
            .OrderBy(item =>
                item.Step
                    .DelayAfterStartMilliseconds)
            .ThenBy(item => item.Index)
            .Select(item => item.Step)
            .ToArray();

        List<PlacementStep> migrated =
            [.. before, CreateStartGameStep()];
        int previousDue = 0;
        foreach (IGrouping<int, PlacementStep> group in
                 after.GroupBy(step =>
                     step.DelayAfterStartMilliseconds))
        {
            int due = group.Key;
            int wait = due - previousDue;
            if (wait > 0)
            {
                migrated.Add(CreateDelayStep(wait));
            }
            migrated.AddRange(group.Select(step =>
                step with
                {
                    Phase =
                        PlacementPhase.AfterStart,
                    DelayAfterStartMilliseconds = 0,
                }));
            previousDue = due;
        }
        return migrated;
    }

    private static PlacementStep CreateDelayStep(
        int milliseconds) =>
        new()
        {
            Kind = MatchStepKind.Delay,
            UnitKey = 0,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            Phase = PlacementPhase.AfterStart,
            DelayDurationMilliseconds =
                milliseconds,
        };
}
