using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private PlacementModel BuildModel()
    {
        if (_steps.Count == 0 &&
            string.IsNullOrWhiteSpace(
                _fastManualRecordingId))
        {
            throw new InvalidOperationException(
                "Add at least one match step.");
        }

        const CameraPreparationMode mode =
            CameraPreparationMode.FastNoAlign;
        if (_selectedModel is not null &&
            _selectedModel.CameraPreparationMode != mode)
        {
            throw new InvalidOperationException(
                "Create a new model instead of converting an incompatible placement model.");
        }

        PlacementTarget target = CurrentFastTarget();
        string name = PlacementSetupCatalog
            .NameFor(target)
            .Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException(
                "Enter a model name.");
        }
        PlacementModel model = new()
        {
            Id = PlacementSetupCatalog.IdFor(
                target),
            Name = name,
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode = mode,
            Target = target,
            TeamSlot =
                FastTeamCombo.SelectedItem is
                    TeamChoice team
                ? team.Value
                : 0,
            PlacementIntervalMilliseconds =
                _fastPlacementIntervalMilliseconds,
            PlacementAttempts =
                _fastPlacementAttempts,
            AdvancedSettings =
                _fastAdvancedSettings,
            DefaultAfterStartDelayMilliseconds =
                _fastDefaultAfterStartDelayMilliseconds,
            ManualInputRecordingId =
                _fastManualRecordingId,
            ImpossibilityThresholdMinutes =
                _fastImpossibilityThresholdMinutes,
            Steps =
                PlacementTimelinePolicy
                    .NormalizeSteps(
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
