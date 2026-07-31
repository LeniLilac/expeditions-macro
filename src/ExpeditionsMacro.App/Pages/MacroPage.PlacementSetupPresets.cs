using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<(
        ChallengePreset Preset,
        IReadOnlyDictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> Models)>
        BuildChallengeSetupAsync(
            MacroTaskDefinition task,
            CancellationToken cancellationToken,
            bool useStoryInfinitePlacements = false)
    {
        List<ChallengeMapProfile> profiles = [];
        Dictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> models = [];
        foreach (ChallengeMapId map in
                 Enum.GetValues<ChallengeMapId>())
        {
            PlacementTarget target =
                useStoryInfinitePlacements
                    ? new PlacementTarget
                    {
                        Mode = PlacementTargetMode.Story,
                        MapNumber = (int)map,
                        StoryRunKind =
                            StoryRunKind.Infinite,
                        ActNumber = 1,
                    }
                    : new PlacementTarget
                    {
                        Mode =
                            PlacementTargetMode.Challenge,
                        MapNumber = (int)map,
                    };
            PlacementModel placement =
                await LoadPlacementSetupAsync(
                    target,
                    cancellationToken)
                    .ConfigureAwait(false);
            if (useStoryInfinitePlacements)
            {
                placement =
                    BountyChallengePlacementPolicy
                        .ForChallengeRuntime(
                            placement,
                            map);
            }
            profiles.Add(
                new ChallengeMapProfile
                {
                    Map = map,
                    PrestartPlacementModelId =
                        placement.Id,
                    TeamSlot = placement.TeamSlot,
                    DelayedPlacementSeconds = 0,
                });
            models[map] =
                new ChallengeMapRuntimeModels(
                    placement);
        }

        ChallengePreset preset = new()
        {
            Id = $"task-{task.Id}",
            Name = task.Name,
            RunTraitChallenge =
                task.RunTraitChallenge,
            RunStatChallenge =
                task.RunStatChallenge,
            RunSpriteChallenge =
                task.RunSpriteChallenge,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Maps = profiles,
            DefeatRetries = task.DefeatRetries,
        };
        preset.ValidateReady();
        return (preset, models);
    }

    private async Task<(
        ExpeditionPreset Preset,
        PlacementModel Placement)>
        BuildExpeditionSetupAsync(
            MacroTaskDefinition task,
            CancellationToken cancellationToken)
    {
        PlacementTarget target =
            RequireTarget(
                task,
                PlacementTargetMode.Expedition);
        PlacementModel placement =
            await LoadPlacementSetupAsync(
                target,
                cancellationToken)
                .ConfigureAwait(false);
        ExpeditionPreset preset = new()
        {
            Id = $"task-{task.Id}",
            Name = task.Name,
            MapNumber = target.MapNumber,
            Difficulty = task.Difficulty,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PlacementModelId = placement.Id,
            TeamSlot = placement.TeamSlot,
            ExtractAtCheckpoint =
                task.ExtractAtCheckpoint,
            BossesBeforeExtract =
                task.BossesBeforeExtract,
        };
        preset.Validate();
        return (preset, placement);
    }

    private async Task<(
        StoryPreset Preset,
        StageRuntimeModels Models)>
        BuildStorySetupAsync(
            MacroTaskDefinition task,
            CancellationToken cancellationToken)
    {
        PlacementTarget target =
            RequireTarget(
                task,
                PlacementTargetMode.Story);
        PlacementModel placement =
            await LoadPlacementSetupAsync(
                target,
                cancellationToken)
                .ConfigureAwait(false);
        StoryPreset preset = new()
        {
            Id = $"task-{task.Id}",
            Name = task.Name,
            Map = (ChallengeMapId)
                target.MapNumber,
            RunKind = target.StoryRunKind,
            ActNumber = target.ActNumber,
            HardMode = task.HardMode,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PrestartPlacementModelId =
                placement.Id,
            DelayedPlacementSeconds = 0,
            TeamSlot = placement.TeamSlot,
            DefeatRetries = task.DefeatRetries,
        };
        preset.Validate(requireModels: true);
        return (
            preset,
            new StageRuntimeModels(
                placement));
    }

    private async Task<(
        RaidPreset Preset,
        StageRuntimeModels Models)>
        BuildRaidSetupAsync(
            MacroTaskDefinition task,
            CancellationToken cancellationToken)
    {
        PlacementTarget target =
            RequireTarget(
                task,
                PlacementTargetMode.Raid);
        PlacementModel placement =
            await LoadPlacementSetupAsync(
                target,
                cancellationToken)
                .ConfigureAwait(false);
        RaidPreset preset = new()
        {
            Id = $"task-{task.Id}",
            Name = task.Name,
            Act = (RaidAct)target.ActNumber,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PrestartPlacementModelId =
                placement.Id,
            DelayedPlacementSeconds = 0,
            TeamSlot = placement.TeamSlot,
            DefeatRetries = task.DefeatRetries,
        };
        preset.Validate(requireModels: true);
        return (
            preset,
            new StageRuntimeModels(
                placement));
    }

    private async Task<(
        EventPreset Preset,
        PlacementModel Placement)>
        BuildEventSetupAsync(
            MacroTaskDefinition task,
            CancellationToken cancellationToken)
    {
        PlacementTarget target =
            RequireTarget(
                task,
                PlacementTargetMode.Event);
        PlacementModel placement =
            await LoadPlacementSetupAsync(
                target,
                cancellationToken)
                .ConfigureAwait(false);
        EventPreset preset = new()
        {
            Id = $"task-{task.Id}",
            Name = task.Name,
            Mode = (EventModeId)
                target.MapNumber,
            Act = (EventAct)
                target.ActNumber,
            SpawnRoute = target.SpawnRoute,
            PlacementModelId = placement.Id,
            TeamSlot = placement.TeamSlot,
            DefeatRetries = task.DefeatRetries,
        };
        preset.Validate();
        return (preset, placement);
    }

    private async Task<PlacementModel>
        LoadPlacementSetupAsync(
            PlacementTarget target,
            CancellationToken cancellationToken)
    {
        List<PlacementSetupRoute> candidates =
            PlacementSetupCatalog.CandidatesFor(target)
                .ToList();
        foreach (PlacementSetupRoute candidate in
                 candidates)
        {
            PlacementModel? model =
                await _services.PlacementModels
                    .LoadAsync(
                        candidate.ModelId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (model is null)
            {
                continue;
            }
            if (PlacementSetupCatalog
                    .IsEmptyRouteOverride(model))
            {
                continue;
            }

            model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                target);
            await ValidateManualRecordingDependencyAsync(
                    model,
                    cancellationToken)
                .ConfigureAwait(false);
            return model;
        }

        string names = string.Join(
            " or ",
            candidates.Select(route => route.Name));
        throw new InvalidOperationException(
            $"Configure {names} in Placement Setup before starting this plan.");
    }

    private async Task
        ValidateManualRecordingDependencyAsync(
            PlacementModel model,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                model.ManualInputRecordingId))
        {
            return;
        }

        if (!_services.Settings
                .ManualInputRecordingEnabled)
        {
            throw new InvalidOperationException(
                $"{model.Name} uses a manual recording, but manual recordings are disabled under Experimental in Settings. Re-enable them there or switch this Placement Setup route to Step Mode.");
        }

        ManualInputRecording? recording =
            await _services.ManualRecordings.LoadAsync(
                    model.ManualInputRecordingId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (recording is null)
        {
            throw new InvalidOperationException(
                $"{model.Name} references a manual recording that no longer exists. Select an available recording or switch this Placement Setup route to Step Mode.");
        }
    }

    private static PlacementTarget RequireTarget(
        MacroTaskDefinition task,
        PlacementTargetMode mode)
    {
        PlacementTarget target =
            task.PlacementTarget ??
            throw new InvalidOperationException(
                "This task does not have a placement route.");
        if (target.Mode != mode)
        {
            throw new InvalidOperationException(
                "This task's placement route does not match its mode.");
        }
        target.Validate();
        return target;
    }
}
