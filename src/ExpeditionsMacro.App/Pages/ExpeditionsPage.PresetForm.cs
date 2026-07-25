using System.Globalization;
using System.Windows;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class ExpeditionsPage
{
    private void ApplyPreset(ExpeditionPreset preset)
    {
        PresetNameText.Text = preset.Name;
        MapCombo.SelectedIndex =
            preset.MapNumber - 1;
        DifficultyCombo.SelectedIndex =
            preset.Difficulty - 1;
        SelectCameraMode(
            preset.CameraPreparationMode);
        UpdatePlacementOptions(
            preset.PlacementModelId);
        CameraPresetSelection.Apply(
            CameraCombo,
            PhaseText,
            _cameraModels,
            preset.CameraModelId);
        PlacementCombo.SelectedItem =
            _placementModels.FirstOrDefault(
                model =>
                    model.Id ==
                    preset.PlacementModelId);
        TeamCombo.SelectedItem =
            TeamChoices().First(
                value =>
                    value.Value ==
                    preset.TeamSlot);
        ExtractCheck.IsChecked =
            preset.ExtractAtCheckpoint;
        BossTargetText.Text =
            preset.BossesBeforeExtract.ToString(
                CultureInfo.InvariantCulture);
        AutoRecoverCheck.IsChecked =
            preset.AutoRecover;
        ZoomTicksText.Text =
            preset.ZoomTicks.ToString(
                CultureInfo.InvariantCulture);
        PitchPixelsText.Text =
            preset.PitchDragPixels.ToString(
                CultureInfo.InvariantCulture);
        PollText.Text =
            preset.PollMilliseconds.ToString(
                CultureInfo.InvariantCulture);
        StableText.Text =
            preset.StableDetections.ToString(
                CultureInfo.InvariantCulture);
        KeyHoldText.Text =
            preset.UnitKeyHoldMilliseconds.ToString(
                CultureInfo.InvariantCulture);
        KeyDelayText.Text =
            preset.UnitSelectDelayMilliseconds.ToString(
                CultureInfo.InvariantCulture);
        ExtractCheck_Changed(
            this,
            new RoutedEventArgs());
    }

    private void ApplyNewPreset()
    {
        PresetNameText.Text = "Expedition route";
        MapCombo.SelectedIndex = 0;
        DifficultyCombo.SelectedIndex = 0;
        ExtractCheck.IsChecked = true;
        BossTargetText.Text = "1";
        AutoRecoverCheck.IsChecked = true;
        TeamCombo.SelectedIndex = 0;
        SelectCameraMode(
            _services.Settings.FastNoAlignEnabled
                ? CameraPreparationMode.FastNoAlign
                : CameraPreparationMode.CameraModel);
        SelectCatalogDefaults();
    }

    private async Task RefreshCatalogsAsync()
    {
        IReadOnlyList<CameraModelManifest> cameras =
            await _services.CameraModels.ListAsync();
        IReadOnlyList<PlacementModel> placements =
            await _services.PlacementModels.ListAsync();
        IReadOnlyList<DetectorPackManifest> detectors =
            await _services.DetectorPacks.ListAsync();
        _cameraModels.Clear();
        foreach (CameraModelManifest model in cameras)
        {
            _cameraModels.Add(model);
        }
        _allPlacementModels = placements;
        UpdatePlacementOptions();
        _detectorPacks.Clear();
        foreach (DetectorPackManifest pack in detectors)
        {
            _detectorPacks.Add(pack);
        }
    }

    private void SelectCatalogDefaults()
    {
        CameraCombo.SelectedItem ??=
            _cameraModels.FirstOrDefault();
        PlacementCombo.SelectedItem ??=
            _placementModels.FirstOrDefault();
    }
}
