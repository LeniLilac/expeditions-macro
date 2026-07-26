using System.Windows;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private async void DeleteModel_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ModelsList.SelectedItem is not PlacementModel model)
        {
            return;
        }
        if (MessageBox.Show(
            Window.GetWindow(this),
            $"Delete placement model '{model.Name}'?",
            "Delete model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        _services.PlacementModels.Delete(model.Id);
        NewModel_Click(sender, e);
        await RefreshModelsAsync();
    }

    private PlacementModel BuildModel()
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException(
                "Add at least one placement.");
        }

        CameraPreparationMode mode = FastWorkflow
            ? CameraPreparationMode.FastNoAlign
            : CameraPreparationMode.CameraModel;
        if (_selectedModel is not null &&
            _selectedModel.CameraPreparationMode != mode)
        {
            throw new InvalidOperationException(
                "Create a new model instead of converting an incompatible placement model.");
        }

        PlacementTarget? target =
            FastWorkflow
                ? CurrentFastTarget()
                : null;
        string name = (FastWorkflow
            ? PlacementSetupCatalog.NameFor(
                target!)
            : ModelNameText.Text).Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException(
                "Enter a model name.");
        }
        PlacementModel model = new()
        {
            Id = FastWorkflow
                ? PlacementSetupCatalog.IdFor(
                    target!)
                : _selectedModel?.Id ??
                    ModelId.FromName(name),
            Name = name,
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode = mode,
            Target = target,
            TeamSlot = FastWorkflow &&
                FastTeamCombo.SelectedItem is
                    TeamChoice team
                ? team.Value
                : 0,
            PlacementIntervalMilliseconds =
                FastWorkflow
                    ? _fastPlacementIntervalMilliseconds
                    : PlacementAuthoringRules
                        .DefaultStepDelayMilliseconds,
            DefaultAfterStartDelayMilliseconds =
                FastWorkflow
                    ? _fastDefaultAfterStartDelayMilliseconds
                    : PlacementAuthoringRules
                        .DefaultAfterStartDelayMilliseconds,
            Steps =
                PlacementAuthoringRules
                    .OrderForAuthoring(
                        _steps
                            .Select(row =>
                                row.ToModel())
                            .ToArray()),
            CreatedAt =
                _selectedModel?.CreatedAt ??
                DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        model.Validate();
        return model;
    }
}
