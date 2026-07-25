using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class ExpeditionsPage
{
    internal void SetSnapshotFastMode() =>
        SelectCameraMode(
            CameraPreparationMode.FastNoAlign);

    private void CameraMode_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (CameraCombo is null ||
            PlacementCombo is null)
        {
            return;
        }
        ApplyCameraModeLayout();
        UpdatePlacementOptions();
    }

    private void PlacementRoute_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_loading &&
            PlacementCombo is not null)
        {
            UpdatePlacementOptions();
        }
    }

    private void ApplyCameraModeLayout()
    {
        bool fast =
            SelectedCameraMode() ==
            CameraPreparationMode.FastNoAlign;
        ExpeditionCameraRow.Height =
            fast ? new GridLength(0) : new GridLength(54);
        CameraLabel.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        CameraCombo.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdatePlacementOptions(
        string? selectedId = null)
    {
        if (MapCombo.SelectedItem is null) return;
        selectedId ??=
            (PlacementCombo.SelectedItem as
                PlacementModel)?.Id;
        CameraPreparationMode mode =
            SelectedCameraMode();
        PlacementTarget target = new()
        {
            Mode = PlacementTargetMode.Expedition,
            MapNumber = SelectedTag(MapCombo),
            ActNumber = 0,
        };
        _placementModels.Clear();
        foreach (PlacementModel model in
                 _allPlacementModels.Where(
                     model => model.IsCompatibleWith(
                         mode,
                         target)))
        {
            _placementModels.Add(model);
        }
        PlacementCombo.SelectedItem =
            _placementModels.FirstOrDefault(
                model => model.Id == selectedId) ??
            _placementModels.FirstOrDefault();
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
