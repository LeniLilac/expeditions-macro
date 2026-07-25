using System.Windows;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    internal void SetSnapshotState()
    {
        if (!FastWorkflow) return;
        _selectedModel = null;
        _selectedSetupTarget =
            PlacementSetupCatalog.All[0].Target;
        ApplyFastTarget(_selectedSetupTarget);
        FastTeamCombo.SelectedIndex = 2;
        _steps.Clear();
        _steps.Add(new PlacementStepRow
        {
            UnitKey = 1,
            X = 390,
            Y = 352,
            Phase = PlacementPhase.BeforeStart,
            DelayAfterMilliseconds = 900,
        });
        _steps.Add(new PlacementStepRow
        {
            UnitKey = 2,
            X = 445,
            Y = 394,
            Phase = PlacementPhase.AfterStart,
            DelayAfterStartMilliseconds = 5000,
            DelayAfterMilliseconds = 900,
        });
        _steps.Add(new PlacementStepRow
        {
            UnitKey = 3,
            X = 505,
            Y = 332,
            Phase = PlacementPhase.BeforeStart,
            DelayAfterMilliseconds = 900,
        });
        FastAfterStartButton.IsChecked = true;
        FastAfterStartDelayText.Text = "5";
        FastStepsList.SelectedIndex = 1;
        FastStatusText.Text = string.Empty;
        UpdateFastPlacementCount();
    }

    private void ApplyWorkflowMode()
    {
        bool fast = FastWorkflow;
        ModelsList.ItemsSource =
            fast ? _setupRows : _models;
        ModelsList.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        FastSetupList.Visibility =
            fast ? Visibility.Visible : Visibility.Collapsed;
        ModelsHeaderText.Text =
            fast
                ? "PLACEMENT SETUP"
                : "PLACEMENT MODELS";
        NewModelButton.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        DeleteModelButton.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        LegacyEditorPanel.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        FastEditorPanel.Visibility =
            fast ? Visibility.Visible : Visibility.Collapsed;
        PageHeading.Visibility =
            fast ? Visibility.Collapsed : Visibility.Visible;
        if (_selectedModel is not null &&
            _selectedModel.CameraPreparationMode ==
                (fast
                    ? CameraPreparationMode.FastNoAlign
                    : CameraPreparationMode.CameraModel))
        {
            ApplyModel(_selectedModel);
        }
        else
        {
            ApplyNewModelDefaults();
        }
        UpdateBusyState();
    }
}
