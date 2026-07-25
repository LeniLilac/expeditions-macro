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
        Dictionary<ChallengeMapId, ChallengeMapRuntimeModels>
            result = [];
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            CameraModel? camera =
                preset.CameraPreparationMode ==
                    CameraPreparationMode.CameraModel
                    ? await _services.CameraModels.LoadAsync(
                        profile.CameraModelId,
                        cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException(
                            $"The {Label(profile.Map)} camera model could not be loaded.")
                    : null;
            PlacementModel? prestart =
                await LoadOptionalPlacementAsync(
                    profile.PrestartPlacementModelId,
                    cancellationToken).ConfigureAwait(false);
            PlacementModel? delayed =
                await LoadOptionalPlacementAsync(
                    profile.DelayedPlacementModelId,
                    cancellationToken).ConfigureAwait(false);
            result[profile.Map] =
                new ChallengeMapRuntimeModels(
                    camera,
                    prestart,
                    delayed);
        }
        return result;
    }

    private async Task<StageRuntimeModels>
        LoadStageModelsAsync(
            CameraPreparationMode cameraMode,
            string cameraId,
            string prestartId,
            string delayedId,
            CancellationToken cancellationToken)
    {
        CameraModel? camera =
            cameraMode == CameraPreparationMode.CameraModel
                ? await _services.CameraModels.LoadAsync(
                    cameraId,
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "The selected camera model could not be loaded.")
                : null;
        PlacementModel? prestart =
            await LoadOptionalPlacementAsync(
                prestartId,
                cancellationToken).ConfigureAwait(false);
        PlacementModel? delayed =
            await LoadOptionalPlacementAsync(
                delayedId,
                cancellationToken).ConfigureAwait(false);
        return new StageRuntimeModels(
            camera,
            prestart,
            delayed);
    }

    private Task<PlacementModel?>
        LoadOptionalPlacementAsync(
            string id,
            CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(id)
            ? Task.FromResult<PlacementModel?>(null)
            : LoadRequiredPlacementAsync(
                id,
                cancellationToken);

    private async Task<PlacementModel?>
        LoadRequiredPlacementAsync(
            string id,
            CancellationToken cancellationToken) =>
        await _services.PlacementModels.LoadAsync(
            id,
            cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            $"Placement model '{id}' could not be loaded.");
}
