using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class ChallengesPage
{
    internal void SetSnapshotFastMode() =>
        SelectCameraMode(
            CameraPreparationMode.FastNoAlign);

    private async Task RefreshCatalogsAsync()
    {
        IReadOnlyList<CameraModelManifest> cameras =
            await _services.CameraModels.ListAsync();
        IReadOnlyList<PlacementModel> placements =
            await _services.PlacementModels.ListAsync();
        IReadOnlyList<DetectorPackManifest> detectorPacks =
            await _services.DetectorPacks.ListAsync();

        CameraOptions.Clear();
        CameraOptions.Add(
            new CatalogOption(
                string.Empty,
                "Choose model"));
        foreach (CameraModelManifest camera in cameras)
        {
            CameraOptions.Add(
                new CatalogOption(
                    camera.Id,
                    camera.Name));
        }
        _allPlacementModels = placements;
        PlacementOptions.Clear();
        PlacementOptions.Add(
            new CatalogOption(
                string.Empty,
                "None"));
        foreach (PlacementModel placement in
                 placements.Where(
                     model =>
                         model.CameraPreparationMode ==
                         CameraPreparationMode.CameraModel))
        {
            PlacementOptions.Add(
                new CatalogOption(
                    placement.Id,
                    placement.Name));
        }
        PopulateFastPlacementOptions();
        _detectorPacks.Clear();
        foreach (DetectorPackManifest pack in detectorPacks)
        {
            _detectorPacks.Add(pack);
        }
    }

    private async Task<IReadOnlyDictionary<
        ChallengeMapId,
        ChallengeMapRuntimeModels>>
        LoadMapModelsAsync(ChallengePreset preset)
    {
        Dictionary<ChallengeMapId, ChallengeMapRuntimeModels>
            result = [];
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            CameraModel? camera =
                preset.CameraPreparationMode ==
                    CameraPreparationMode.CameraModel
                    ? await _services.CameraModels.LoadAsync(
                        profile.CameraModelId) ??
                        throw new InvalidOperationException(
                            $"The {Label(profile.Map)} camera model could not be loaded.")
                    : null;
            PlacementModel? prestart =
                string.IsNullOrWhiteSpace(
                    profile.PrestartPlacementModelId)
                    ? null
                    : await _services.PlacementModels.LoadAsync(
                        profile.PrestartPlacementModelId) ??
                        throw new InvalidOperationException(
                            $"The {Label(profile.Map)} before-start placement model could not be loaded.");
            PlacementModel? delayed =
                string.IsNullOrWhiteSpace(
                    profile.DelayedPlacementModelId)
                    ? null
                    : await _services.PlacementModels.LoadAsync(
                        profile.DelayedPlacementModelId) ??
                        throw new InvalidOperationException(
                            $"The {Label(profile.Map)} delayed placement model could not be loaded.");
            result[profile.Map] =
                new ChallengeMapRuntimeModels(
                    camera,
                    prestart,
                    delayed);
        }
        return result;
    }

    private void CameraMode_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (LegacyMapModelsPanel is null ||
            FastMapModelsPanel is null)
        {
            return;
        }
        bool fast =
            SelectedCameraMode() ==
            CameraPreparationMode.FastNoAlign;
        LegacyMapModelsPanel.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        FastMapModelsPanel.Visibility =
            fast ? Visibility.Visible : Visibility.Collapsed;
        if (fast)
        {
            foreach (ChallengeMapRow row in MapRows)
            {
                row.CameraModelId = string.Empty;
                row.DelayedPlacementModelId = string.Empty;
            }
        }
        else
        {
            ClearIncompatibleFastSelections();
        }
        PopulateFastPlacementOptions();
    }

    private void ClearIncompatibleFastSelections()
    {
        foreach (ChallengeMapRow row in MapRows)
        {
            if (!PlacementOptions.Any(
                    option =>
                        option.Id ==
                        row.PrestartPlacementModelId))
            {
                row.PrestartPlacementModelId =
                    string.Empty;
            }
        }
    }

    private void PopulateFastPlacementOptions()
    {
        bool selectFast =
            SelectedCameraMode() ==
            CameraPreparationMode.FastNoAlign;
        foreach (ChallengeMapRow row in MapRows)
        {
            string selected = row.PrestartPlacementModelId;
            row.FastPlacementOptions.Clear();
            PlacementTarget target =
                PlacementTarget.ForChallenge(row.Map);
            foreach (PlacementModel model in
                     _allPlacementModels.Where(
                         model => model.IsCompatibleWith(
                             CameraPreparationMode.FastNoAlign,
                             target)))
            {
                row.FastPlacementOptions.Add(
                    new CatalogOption(
                        model.Id,
                        model.Name));
            }
            if (selectFast &&
                !row.FastPlacementOptions.Any(
                    option => option.Id == selected))
            {
                row.PrestartPlacementModelId =
                    row.FastPlacementOptions
                        .FirstOrDefault()?.Id ??
                    string.Empty;
            }
        }
    }

    private CameraPreparationMode SelectedCameraMode() =>
        (CameraModeCombo.SelectedItem as
            NamedChoice<CameraPreparationMode>)?.Value ??
        CameraPreparationMode.CameraModel;

    private void SelectCameraMode(
        CameraPreparationMode mode) =>
        CameraModeCombo.SelectedItem =
            CameraModeCombo.Items
                .Cast<NamedChoice<CameraPreparationMode>>()
                .First(choice => choice.Value == mode);

    private static IReadOnlyList<
        NamedChoice<CameraPreparationMode>>
        CameraModeChoices() =>
        [
            new(
                CameraPreparationMode.FastNoAlign,
                "Fast no align"),
            new(
                CameraPreparationMode.CameraModel,
                "Camera model"),
        ];
}
