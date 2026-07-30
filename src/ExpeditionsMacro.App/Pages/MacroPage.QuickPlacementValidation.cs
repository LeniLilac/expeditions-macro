using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<bool>
        PlanRequiresQuickPlacementKeyAsync(
        MacroPlan plan,
        CancellationToken cancellationToken) =>
        await PlacementControlRequirements
            .PlanRequiresQuickPlacementKeyAsync(
                plan,
                ResolveTaskPlacementsAsync,
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<IReadOnlyList<PlacementModel>>
        ResolveTaskPlacementsAsync(
        MacroTaskDefinition task,
        CancellationToken cancellationToken)
    {
        if (task.UsesPlacementSetup)
        {
            return await ResolvePlacementSetupTaskAsync(
                    task,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return task.Kind switch
        {
            MacroTaskKind.Challenge =>
                await ResolveLegacyChallengeAsync(
                        task,
                        cancellationToken)
                    .ConfigureAwait(false),
            MacroTaskKind.Expedition =>
                [
                    await ResolveLegacyExpeditionAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false),
                ],
            MacroTaskKind.Story =>
                [
                    await ResolveLegacyStoryAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false),
                ],
            MacroTaskKind.Raid =>
                [
                    await ResolveLegacyRaidAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false),
                ],
            MacroTaskKind.Event =>
                throw new InvalidOperationException(
                    "Event routes require Fast no align Placement Setup."),
            MacroTaskKind.Utility => [],
            MacroTaskKind.Bounty =>
                await ResolveBountyPlacementsAsync(
                        cancellationToken)
                    .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(
                nameof(task),
                task.Kind,
                "Unknown macro task type."),
        };
    }

    private async Task<IReadOnlyList<PlacementModel>>
        ResolvePlacementSetupTaskAsync(
        MacroTaskDefinition task,
        CancellationToken cancellationToken) =>
        task.Kind switch
        {
            MacroTaskKind.Challenge =>
                (await BuildChallengeSetupAsync(
                        task,
                        cancellationToken)
                    .ConfigureAwait(false))
                .Models.Values
                .Select(model => model.Placement)
                .OfType<PlacementModel>()
                .ToArray(),
            MacroTaskKind.Expedition =>
                [
                    (await BuildExpeditionSetupAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Placement,
                ],
            MacroTaskKind.Story =>
                [
                    (await BuildStorySetupAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Models.Placement!,
                ],
            MacroTaskKind.Raid =>
                [
                    (await BuildRaidSetupAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Models.Placement!,
                ],
            MacroTaskKind.Event =>
                [
                    (await BuildEventSetupAsync(
                            task,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Placement,
                ],
            MacroTaskKind.Utility => [],
            MacroTaskKind.Bounty =>
                await ResolveBountyPlacementsAsync(
                        cancellationToken)
                    .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(
                nameof(task),
                task.Kind,
                "Unknown macro task type."),
        };

    private async Task<IReadOnlyList<PlacementModel>>
        ResolveLegacyChallengeAsync(
        MacroTaskDefinition task,
        CancellationToken cancellationToken)
    {
        ChallengePreset preset =
            await _services.ChallengePresets.LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                $"Challenge preset '{task.PresetId}' could not be loaded.");
        IReadOnlyDictionary<
            ChallengeMapId,
            ChallengeMapRuntimeModels> models =
            await LoadChallengeModelsAsync(
                    preset,
                    cancellationToken)
                .ConfigureAwait(false);
        return models.Values
            .Select(model => model.Placement)
            .OfType<PlacementModel>()
            .ToArray();
    }

    private async Task<PlacementModel>
        ResolveLegacyExpeditionAsync(
        MacroTaskDefinition task,
        CancellationToken cancellationToken)
    {
        ExpeditionPreset preset =
            await _services.Presets.LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                $"Expedition preset '{task.PresetId}' could not be loaded.");
        return await LoadRequiredPlacementAsync(
                preset.PlacementModelId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PlacementModel>
        ResolveLegacyStoryAsync(
        MacroTaskDefinition task,
        CancellationToken cancellationToken)
    {
        StoryPreset preset =
            await _services.StoryPresets.LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                $"Story preset '{task.PresetId}' could not be loaded.");
        return (await LoadStageModelsAsync(
                    preset.CameraPreparationMode,
                    preset.PrestartPlacementModelId,
                    cancellationToken)
                .ConfigureAwait(false))
            .Placement!;
    }

    private async Task<PlacementModel>
        ResolveLegacyRaidAsync(
        MacroTaskDefinition task,
        CancellationToken cancellationToken)
    {
        RaidPreset preset =
            await _services.RaidPresets.LoadAsync(
                    task.PresetId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                $"Raid preset '{task.PresetId}' could not be loaded.");
        return (await LoadStageModelsAsync(
                    preset.CameraPreparationMode,
                    preset.PrestartPlacementModelId,
                    cancellationToken)
                .ConfigureAwait(false))
            .Placement!;
    }
}
