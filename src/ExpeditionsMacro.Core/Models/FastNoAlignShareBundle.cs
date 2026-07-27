namespace ExpeditionsMacro.Core.Models;

public sealed record FastNoAlignShareBundle
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentFormat =
        "expeditions-macro-fast-no-align";

    public string Format { get; init; } = CurrentFormat;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public required MacroPlan Plan { get; init; }

    public required IReadOnlyList<PlacementModel>
        PlacementSetups
    { get; init; }

    public void Validate()
    {
        if (!string.Equals(
                Format,
                CurrentFormat,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "This is not an Expeditions Macro Fast no align share code.");
        }
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                "This Fast no align share code uses an unsupported version.");
        }

        Plan.Validate();
        if (Plan.Progress.Count != 0)
        {
            throw new InvalidDataException(
                "Shared plans cannot contain run history.");
        }
        if (!Plan.LoopProgress.IsEmpty ||
            Plan.LoopStates.Count != 0)
        {
            throw new InvalidDataException(
                "Shared plans cannot contain loop history.");
        }
        if (Plan.Tasks.Any(task =>
                !task.UsesPlacementSetup))
        {
            throw new InvalidDataException(
                "Legacy preset tasks cannot be shared as a Fast no align plan.");
        }

        Dictionary<string, PlacementModel> configured =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (PlacementModel setup in PlacementSetups)
        {
            setup.Validate();
            if (setup.CameraPreparationMode !=
                    CameraPreparationMode.FastNoAlign ||
                setup.Target is null)
            {
                throw new InvalidDataException(
                    "The share code contains an incompatible placement model.");
            }

            PlacementSetupRoute route =
                PlacementSetupCatalog.For(setup.Target);
            if (!string.Equals(
                    setup.Id,
                    route.ModelId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The share code contains a placement setup with a nonstandard identity.");
            }
            if (!configured.TryAdd(setup.Id, setup))
            {
                throw new InvalidDataException(
                    "The share code contains the same placement setup more than once.");
            }
        }

        PlacementTarget[] requiredTargets =
            RequiredSetupTargets(Plan).ToArray();
        bool everyRequiredCovered =
            requiredTargets.All(required =>
                configured.Values.Any(setup =>
                    setup.Target is not null &&
                    PlacementSetupCatalog.Covers(
                        setup.Target,
                        required)));
        bool everySuppliedUsed =
            configured.Values.All(setup =>
                setup.Target is not null &&
                requiredTargets.Any(required =>
                    PlacementSetupCatalog.Covers(
                        setup.Target,
                        required)));
        if (!everyRequiredCovered || !everySuppliedUsed)
        {
            throw new InvalidDataException(
                "The share code does not contain exactly the placement setups required by its plan.");
        }
    }

    public static IReadOnlySet<string> RequiredSetupIds(
        MacroPlan plan) =>
        RequiredSetupTargets(plan)
            .Select(PlacementSetupCatalog.IdFor)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PlacementTarget>
        RequiredSetupTargets(MacroPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        List<PlacementTarget> targets = [];
        foreach (MacroTaskDefinition task in plan.Tasks)
        {
            if (!task.UsesPlacementSetup)
            {
                throw new InvalidDataException(
                    "Legacy preset tasks cannot be exported as a Fast no align plan.");
            }

            if (task.Kind == MacroTaskKind.Challenge)
            {
                foreach (PlacementSetupRoute route in
                         PlacementSetupCatalog.All.Where(
                             route =>
                                 route.Target.Mode ==
                                 PlacementTargetMode.Challenge))
                {
                    AddTarget(
                        targets,
                        route.Target);
                }
                continue;
            }

            PlacementTarget target =
                task.PlacementTarget ??
                throw new InvalidDataException(
                    "A Fast no align task is missing its placement route.");
            AddTarget(targets, target);
        }
        return targets;
    }

    private static void AddTarget(
        ICollection<PlacementTarget> targets,
        PlacementTarget target)
    {
        if (!targets.Any(
                existing =>
                    existing.Matches(target)))
        {
            targets.Add(target);
        }
    }
}
