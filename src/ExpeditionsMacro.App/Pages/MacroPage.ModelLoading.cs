using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<IReadOnlyDictionary<
        ChallengeMapId,
        ChallengeMapRuntimeModels>>
        LoadChallengeModelsAsync(
        ChallengePreset preset,
        CancellationToken cancellationToken)
    {
        CameraPreparationExecutionPolicy.ValidateForExecution(
            preset.CameraPreparationMode,
            "The selected Challenge preset");
        Dictionary<ChallengeMapId, ChallengeMapRuntimeModels>
            result = [];
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            PlacementModel placement =
                await LoadRequiredPlacementAsync(
                    profile.PrestartPlacementModelId,
                    cancellationToken).ConfigureAwait(false);
            result[profile.Map] =
                new ChallengeMapRuntimeModels(
                    placement);
        }
        return result;
    }

    private async Task<StageRuntimeModels>
        LoadStageModelsAsync(
            CameraPreparationMode cameraMode,
            string prestartId,
            CancellationToken cancellationToken)
    {
        CameraPreparationExecutionPolicy.ValidateForExecution(
            cameraMode,
            "The selected Stage preset");
        PlacementModel placement =
            await LoadRequiredPlacementAsync(
                prestartId,
                cancellationToken).ConfigureAwait(false);
        return new StageRuntimeModels(
            placement);
    }

    private async Task<PlacementModel>
        LoadRequiredPlacementAsync(
            string id,
            CancellationToken cancellationToken) =>
        await _services.PlacementModels.LoadAsync(
            id,
            cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            $"Placement model '{id}' could not be loaded.");
}
