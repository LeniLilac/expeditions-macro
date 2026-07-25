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
            CancellationToken cancellationToken)
    {
        List<ChallengeMapProfile> profiles = [];
        Dictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> models = [];
        foreach (ChallengeMapId map in
                 Enum.GetValues<ChallengeMapId>())
        {
            PlacementTarget target = new()
            {
                Mode = PlacementTargetMode.Challenge,
                MapNumber = (int)map,
            };
            PlacementModel placement =
                await LoadPlacementSetupAsync(
                    target,
                    cancellationToken)
                    .ConfigureAwait(false);
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
                    null,
                    placement,
                    null);
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
                null,
                placement,
                null));
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
                null,
                placement,
                null));
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

            model.ValidateCompatibility(
                CameraPreparationMode.FastNoAlign,
                target);
            return model;
        }

        string names = string.Join(
            " or ",
            candidates.Select(route => route.Name));
        throw new InvalidOperationException(
            $"Configure {names} in Placement Setup before starting this plan.");
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
