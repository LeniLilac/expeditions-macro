using System.Text.Json.Serialization;

namespace ExpeditionsMacro.Core.Models;

public sealed record FastNoAlignShareBundle
{
    public const int CurrentSchemaVersion = 2;
    public const int LegacySchemaVersion = 1;
    public const string CurrentFormat =
        "expeditions-macro-fast-no-align";

    public string Format { get; init; } = CurrentFormat;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public required MacroPlan Plan { get; init; }

    public required IReadOnlyList<PlacementModel>
        PlacementSetups
    { get; init; }

    public IReadOnlyList<ExpeditionPreset>
        ExpeditionPresets
    { get; init; } = [];

    public IReadOnlyList<ChallengePreset>
        ChallengePresets
    { get; init; } = [];

    public IReadOnlyList<StoryPreset>
        StoryPresets
    { get; init; } = [];

    public IReadOnlyList<RaidPreset>
        RaidPresets
    { get; init; } = [];

    [JsonPropertyName("manual_input_recordings")]
    [JsonIgnore(Condition =
        JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ManualInputRecording>?
        LegacyManualInputRecordings
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
        if (SchemaVersion is not
            (LegacySchemaVersion or
             CurrentSchemaVersion))
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
        ValidateNoRecordingPayload();
        ValidatePresetGraph();

        Dictionary<string, PlacementModel> configured =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (PlacementModel setup in PlacementSetups)
        {
            setup.Validate();
            if (!string.IsNullOrWhiteSpace(
                    setup.ManualInputRecordingId))
            {
                throw new InvalidDataException(
                    "Manual input recordings are device-local and cannot be included in a share code.");
            }
            if (setup.CameraPreparationMode !=
                    CameraPreparationMode.FastNoAlign ||
                setup.Target is null)
            {
                throw new InvalidDataException(
                    "The share code contains an incompatible placement model.");
            }

            if (!configured.TryAdd(setup.Id, setup))
            {
                throw new InvalidDataException(
                    "The share code contains the same placement setup more than once.");
            }
        }

        LegacySetupDependency[] legacyDependencies =
            RequiredLegacySetupDependencies()
                .ToArray();
        HashSet<string> legacySetupIds =
            legacyDependencies
                .Select(dependency => dependency.Id)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
        PlacementTarget[] requiredTargets =
            RequiredSetupTargets(Plan).ToArray();
        HashSet<string> directSetupIds =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (PlacementTarget required in
                 requiredTargets)
        {
            PlacementSetupRoute? route =
                PlacementSetupCatalog
                    .CandidatesFor(required)
                    .FirstOrDefault(candidate =>
                        configured.ContainsKey(
                            candidate.ModelId));
            if (route is null)
            {
                throw new InvalidDataException(
                    "The share code does not contain the placement setup required by its plan.");
            }
            configured[route.ModelId]
                .ValidateCompatibility(
                    CameraPreparationMode
                        .FastNoAlign,
                    required);
            directSetupIds.Add(route.ModelId);
        }
        foreach (LegacySetupDependency dependency in
                 legacyDependencies)
        {
            if (!configured.TryGetValue(
                    dependency.Id,
                    out PlacementModel? setup))
            {
                throw new InvalidDataException(
                    $"The share code is missing placement model '{dependency.Id}' referenced by {dependency.Label}.");
            }
            setup.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                dependency.Target);
        }
        bool everySuppliedUsed =
            configured.Values.All(setup =>
                legacySetupIds.Contains(setup.Id) ||
                directSetupIds.Contains(setup.Id));
        if (!everySuppliedUsed)
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
                continue;
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

    private void ValidateNoRecordingPayload()
    {
        if (LegacyManualInputRecordings?.Count > 0)
        {
            throw new InvalidDataException(
                "Share codes cannot contain manual input recording payloads.");
        }
        if (SchemaVersion == LegacySchemaVersion &&
            (Plan.Tasks.Any(task =>
                 !task.UsesPlacementSetup) ||
             ExpeditionPresets.Count != 0 ||
             ChallengePresets.Count != 0 ||
             StoryPresets.Count != 0 ||
             RaidPresets.Count != 0))
        {
            throw new InvalidDataException(
                "Legacy share schema 1 cannot contain referenced presets.");
        }
    }

    private void ValidatePresetGraph()
    {
        ValidateExactPresets(
            RequiredPresetIds(
                MacroTaskKind.Expedition),
            ExpeditionPresets,
            preset => preset.Id,
            preset =>
            {
                preset.Validate();
                RequireFastNoAlign(
                    preset.CameraPreparationMode,
                    "Expedition");
            },
            "Expedition");
        ValidateExactPresets(
            RequiredPresetIds(
                MacroTaskKind.Challenge),
            ChallengePresets,
            preset => preset.Id,
            preset =>
            {
                preset.ValidateReady();
                RequireFastNoAlign(
                    preset.CameraPreparationMode,
                    "Challenge");
            },
            "Challenge");
        ValidateExactPresets(
            RequiredPresetIds(
                MacroTaskKind.Story),
            StoryPresets,
            preset => preset.Id,
            preset =>
            {
                preset.Validate(
                    requireModels: true);
                RequireFastNoAlign(
                    preset.CameraPreparationMode,
                    "Story");
            },
            "Story");
        ValidateExactPresets(
            RequiredPresetIds(
                MacroTaskKind.Raid),
            RaidPresets,
            preset => preset.Id,
            preset =>
            {
                preset.Validate(
                    requireModels: true);
                RequireFastNoAlign(
                    preset.CameraPreparationMode,
                    "Raid");
            },
            "Raid");
        if (Plan.Tasks.Any(task =>
                !task.UsesPlacementSetup &&
                task.Kind == MacroTaskKind.Event))
        {
            throw new InvalidDataException(
                "Event tasks must use a placement setup rather than a legacy preset.");
        }
    }

    private IEnumerable<LegacySetupDependency>
        RequiredLegacySetupDependencies()
    {
        foreach (ExpeditionPreset preset in
                 ExpeditionPresets)
        {
            yield return new LegacySetupDependency(
                preset.PlacementModelId,
                PlacementTarget.ForExpedition(
                    preset),
                $"Expedition preset '{preset.Name}'");
        }
        foreach (ChallengePreset preset in
                 ChallengePresets)
        {
            foreach (ChallengeMapProfile map in
                     preset.Maps)
            {
                if (!string.IsNullOrWhiteSpace(
                        map.PrestartPlacementModelId))
                {
                    yield return
                        new LegacySetupDependency(
                            map
                                .PrestartPlacementModelId,
                            PlacementTarget.ForChallenge(
                                map.Map),
                            $"Challenge preset '{preset.Name}'");
                }
                if (!string.IsNullOrWhiteSpace(
                        map.DelayedPlacementModelId))
                {
                    yield return
                        new LegacySetupDependency(
                            map
                                .DelayedPlacementModelId,
                            PlacementTarget.ForChallenge(
                                map.Map),
                            $"Challenge preset '{preset.Name}'");
                }
            }
        }
        foreach (StoryPreset preset in StoryPresets)
        {
            if (!string.IsNullOrWhiteSpace(
                    preset.PrestartPlacementModelId))
            {
                yield return
                    new LegacySetupDependency(
                        preset.PrestartPlacementModelId,
                        PlacementTarget.ForStory(
                            preset),
                        $"Story preset '{preset.Name}'");
            }
            if (!string.IsNullOrWhiteSpace(
                    preset.DelayedPlacementModelId))
            {
                yield return
                    new LegacySetupDependency(
                        preset.DelayedPlacementModelId,
                        PlacementTarget.ForStory(
                            preset),
                        $"Story preset '{preset.Name}'");
            }
        }
        foreach (RaidPreset preset in RaidPresets)
        {
            if (!string.IsNullOrWhiteSpace(
                    preset.PrestartPlacementModelId))
            {
                yield return
                    new LegacySetupDependency(
                        preset.PrestartPlacementModelId,
                        PlacementTarget.ForRaid(
                            preset),
                        $"Raid preset '{preset.Name}'");
            }
            if (!string.IsNullOrWhiteSpace(
                    preset.DelayedPlacementModelId))
            {
                yield return
                    new LegacySetupDependency(
                        preset.DelayedPlacementModelId,
                        PlacementTarget.ForRaid(
                            preset),
                        $"Raid preset '{preset.Name}'");
            }
        }
    }

    private HashSet<string> RequiredPresetIds(
        MacroTaskKind kind) =>
        Plan.Tasks
            .Where(task =>
                !task.UsesPlacementSetup &&
                task.Kind == kind)
            .Select(task => task.PresetId)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

    private static void ValidateExactPresets<T>(
        IReadOnlySet<string> requiredIds,
        IReadOnlyList<T> presets,
        Func<T, string> id,
        Action<T> validate,
        string label)
    {
        Dictionary<string, T> supplied =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (T preset in presets)
        {
            validate(preset);
            if (!supplied.TryAdd(
                    id(preset),
                    preset))
            {
                throw new InvalidDataException(
                    $"The share code contains the same {label} preset more than once.");
            }
        }
        if (!requiredIds.SetEquals(supplied.Keys))
        {
            throw new InvalidDataException(
                $"The share code does not contain exactly the {label} presets referenced by its plan.");
        }
    }

    private static void RequireFastNoAlign(
        CameraPreparationMode mode,
        string label)
    {
        if (mode != CameraPreparationMode.FastNoAlign)
        {
            throw new InvalidDataException(
                $"{label} camera-model presets cannot be shared without their camera models. Switch the preset to Fast no align first.");
        }
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

    private readonly record struct
        LegacySetupDependency(
            string Id,
            PlacementTarget Target,
            string Label);
}
