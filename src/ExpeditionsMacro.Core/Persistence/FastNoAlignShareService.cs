using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class FastNoAlignShareService
{
    private readonly MacroPlanRepository _plans;
    private readonly PlacementModelRepository _placements;
    private readonly PresetRepository _expeditionPresets;
    private readonly ChallengePresetRepository
        _challengePresets;
    private readonly StoryPresetRepository _storyPresets;
    private readonly RaidPresetRepository _raidPresets;

    public FastNoAlignShareService(
        MacroPlanRepository plans,
        PlacementModelRepository placements,
        PresetRepository expeditionPresets,
        ChallengePresetRepository challengePresets,
        StoryPresetRepository storyPresets,
        RaidPresetRepository raidPresets)
    {
        _plans = plans;
        _placements = placements;
        _expeditionPresets = expeditionPresets;
        _challengePresets = challengePresets;
        _storyPresets = storyPresets;
        _raidPresets = raidPresets;
    }

    public async Task<string> ExportAsync(
        MacroPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        MacroPlan portable = plan with
        {
            Progress = [],
            LoopStates = [],
            LoopProgress = new(),
            ChallengeRotation = null,
        };
        portable.Validate();

        IReadOnlyList<ExpeditionPreset>
            expeditionPresets =
                await LoadReferencedPresetsAsync(
                        portable,
                        MacroTaskKind.Expedition,
                        _expeditionPresets.LoadAsync,
                        preset => preset.Id,
                        "Expedition",
                        cancellationToken)
                    .ConfigureAwait(false);
        IReadOnlyList<ChallengePreset>
            challengePresets =
                await LoadReferencedPresetsAsync(
                        portable,
                        MacroTaskKind.Challenge,
                        _challengePresets.LoadAsync,
                        preset => preset.Id,
                        "Challenge",
                        cancellationToken)
                    .ConfigureAwait(false);
        IReadOnlyList<StoryPreset> storyPresets =
            await LoadReferencedPresetsAsync(
                    portable,
                    MacroTaskKind.Story,
                    _storyPresets.LoadAsync,
                    preset => preset.Id,
                    "Story",
                    cancellationToken)
                .ConfigureAwait(false);
        IReadOnlyList<RaidPreset> raidPresets =
            await LoadReferencedPresetsAsync(
                    portable,
                    MacroTaskKind.Raid,
                    _raidPresets.LoadAsync,
                    preset => preset.Id,
                    "Raid",
                    cancellationToken)
                .ConfigureAwait(false);

        List<PlacementModel> setups = [];
        HashSet<string> exportedIds =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (PlacementTarget target in
                 FastNoAlignShareBundle
                     .RequiredSetupTargets(portable))
        {
            PlacementModel setup =
                await LoadSetupAsync(
                    target,
                    cancellationToken)
                    .ConfigureAwait(false);
            if (exportedIds.Add(setup.Id))
            {
                setups.Add(setup);
            }
        }

        foreach (string modelId in
                 RequiredPresetPlacementIds(
                     expeditionPresets,
                     challengePresets,
                     storyPresets,
                     raidPresets))
        {
            if (!exportedIds.Add(modelId))
            {
                continue;
            }
            PlacementModel? setup =
                await _placements
                    .LoadAsync(
                        modelId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (setup is null)
            {
                throw new InvalidOperationException(
                    $"The placement model '{modelId}' referenced by a preset no longer exists.");
            }
            RequireShareableSetup(setup);
            setups.Add(setup);
        }

        FastNoAlignShareBundle bundle = new()
        {
            Plan = portable,
            PlacementSetups = setups,
            ExpeditionPresets = expeditionPresets,
            ChallengePresets = challengePresets,
            StoryPresets = storyPresets,
            RaidPresets = raidPresets,
        };
        return FastNoAlignShareCodec.Encode(bundle);
    }

    public FastNoAlignShareBundle Read(
        string code) =>
        FastNoAlignShareCodec.Decode(code);

    private async Task<PlacementModel> LoadSetupAsync(
        PlacementTarget target,
        CancellationToken cancellationToken)
    {
        foreach (PlacementSetupRoute route in
                 PlacementSetupCatalog.CandidatesFor(target))
        {
            PlacementModel? setup =
                await _placements
                    .LoadAsync(
                        route.ModelId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (setup is null)
            {
                continue;
            }
            if (PlacementSetupCatalog
                    .IsEmptyRouteOverride(setup))
            {
                continue;
            }
            setup.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                target);
            RequireShareableSetup(setup);
            return setup;
        }

        throw new InvalidOperationException(
            $"Configure '{PlacementSetupCatalog.NameFor(target)}' in Placement Setup before exporting this plan.");
    }

    public async Task ImportAsync(
        FastNoAlignShareBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        bundle = StoryHardModePolicy.Normalize(bundle);
        bundle.Validate();

        foreach (PlacementModel setup in
                 bundle.PlacementSetups)
        {
            await _placements
                .SaveAsync(
                    setup,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        foreach (ExpeditionPreset preset in
                 bundle.ExpeditionPresets)
        {
            await _expeditionPresets
                .SaveAsync(
                    preset,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        foreach (ChallengePreset preset in
                 bundle.ChallengePresets)
        {
            await _challengePresets
                .SaveAsync(
                    preset,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        foreach (StoryPreset preset in
                 bundle.StoryPresets)
        {
            await _storyPresets
                .SaveAsync(
                    preset,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        foreach (RaidPreset preset in
                 bundle.RaidPresets)
        {
            await _raidPresets
                .SaveAsync(
                    preset,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await _plans
            .SaveAsync(
                bundle.Plan with
                {
                    Progress = [],
                    LoopStates = [],
                    LoopProgress = new(),
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<T>>
        LoadReferencedPresetsAsync<T>(
            MacroPlan plan,
            MacroTaskKind kind,
            Func<string, CancellationToken, Task<T?>>
                load,
            Func<T, string> id,
            string label,
            CancellationToken cancellationToken)
        where T : class
    {
        List<T> presets = [];
        HashSet<string> loaded =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string presetId in plan.Tasks
                     .Where(task =>
                         !task.UsesPlacementSetup &&
                         task.Kind == kind)
                     .Select(task => task.PresetId))
        {
            if (!loaded.Add(presetId))
            {
                continue;
            }
            T? preset =
                await load(
                        presetId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (preset is null)
            {
                throw new InvalidOperationException(
                    $"The {label} preset '{presetId}' referenced by the plan no longer exists.");
            }
            if (!string.Equals(
                    id(preset),
                    presetId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"The loaded {label} preset identity does not match the plan.");
            }
            presets.Add(preset);
        }
        return presets;
    }

    private static IEnumerable<string>
        RequiredPresetPlacementIds(
            IReadOnlyList<ExpeditionPreset>
                expeditionPresets,
            IReadOnlyList<ChallengePreset>
                challengePresets,
            IReadOnlyList<StoryPreset> storyPresets,
            IReadOnlyList<RaidPreset> raidPresets)
    {
        foreach (ExpeditionPreset preset in
                 expeditionPresets)
        {
            yield return preset.PlacementModelId;
        }
        foreach (ChallengePreset preset in
                 challengePresets)
        {
            foreach (ChallengeMapProfile map in
                     preset.Maps)
            {
                if (!string.IsNullOrWhiteSpace(
                        map.PrestartPlacementModelId))
                {
                    yield return
                        map.PrestartPlacementModelId;
                }
                if (!string.IsNullOrWhiteSpace(
                        map.DelayedPlacementModelId))
                {
                    yield return
                        map.DelayedPlacementModelId;
                }
            }
        }
        foreach (StoryPreset preset in storyPresets)
        {
            if (!string.IsNullOrWhiteSpace(
                    preset.PrestartPlacementModelId))
            {
                yield return
                    preset.PrestartPlacementModelId;
            }
            if (!string.IsNullOrWhiteSpace(
                    preset.DelayedPlacementModelId))
            {
                yield return
                    preset.DelayedPlacementModelId;
            }
        }
        foreach (RaidPreset preset in raidPresets)
        {
            if (!string.IsNullOrWhiteSpace(
                    preset.PrestartPlacementModelId))
            {
                yield return
                    preset.PrestartPlacementModelId;
            }
            if (!string.IsNullOrWhiteSpace(
                    preset.DelayedPlacementModelId))
            {
                yield return
                    preset.DelayedPlacementModelId;
            }
        }
    }

    private static void RequireShareableSetup(
        PlacementModel setup)
    {
        if (!string.IsNullOrWhiteSpace(
                setup.ManualInputRecordingId))
        {
            throw new InvalidOperationException(
                $"Placement setup '{setup.Name}' uses a device-local manual recording. Choose Step Mode before exporting this plan.");
        }
    }
}
