using System.Globalization;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private const int MaximumPlacementIntervalMilliseconds =
        60_000;
    private const double MaximumAfterStartDelaySeconds =
        3_600;

    private void ResetFastTimingDefaults()
    {
        _fastPlacementIntervalMilliseconds =
            PlacementAuthoringRules
                .DefaultStepDelayMilliseconds;
        _fastPlacementAttempts =
            PlacementModel.DefaultPlacementAttempts;
        _fastDefaultAfterStartDelayMilliseconds =
            PlacementAuthoringRules
                .DefaultAfterStartDelayMilliseconds;
    }

    private void FastTimingSettingsOpening(
        object? sender,
        EventArgs e) =>
        FastEditorPanel.SetTimingSettings(
            _fastPlacementIntervalMilliseconds,
            _fastPlacementAttempts,
            _fastDefaultAfterStartDelayMilliseconds,
            _fastImpossibilityThresholdMinutes,
            !string.IsNullOrWhiteSpace(
                _fastManualRecordingId));

    private void FastTimingSettingsApplied(
        object? sender,
        PlacementTimingApplyEventArgs e)
    {
        if (!int.TryParse(
                e.PlacementIntervalText,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out int interval) ||
            interval < 0 ||
            interval >
                MaximumPlacementIntervalMilliseconds)
        {
            FastEditorPanel.ShowTimingError(
                $"Enter a placement interval from 0 to {MaximumPlacementIntervalMilliseconds:N0} ms.");
            return;
        }

        if (!double.TryParse(
                e.DefaultAfterStartDelayText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out double afterStartSeconds) ||
            afterStartSeconds < 0 ||
            afterStartSeconds >
                MaximumAfterStartDelaySeconds)
        {
            FastEditorPanel.ShowTimingError(
                $"Enter a default After Start delay from 0 to {MaximumAfterStartDelaySeconds:N0} seconds.");
            return;
        }

        if (!int.TryParse(
                e.PlacementAttemptsText,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out int placementAttempts) ||
            placementAttempts is < 1 or
            > PlacementModel.MaximumPlacementAttempts)
        {
            FastEditorPanel.ShowTimingError(
                $"Enter placement attempts from 1 to {PlacementModel.MaximumPlacementAttempts}.");
            return;
        }

        if (!int.TryParse(
                e.ImpossibilityThresholdText,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out int impossibilityThreshold) ||
            impossibilityThreshold is < 0 or
            > PlacementModel
                .MaximumImpossibilityThresholdMinutes)
        {
            FastEditorPanel.ShowTimingError(
                $"Enter an impossibility threshold from 0 to {PlacementModel.MaximumImpossibilityThresholdMinutes} minutes.");
            return;
        }
        int previousDefault =
            _fastDefaultAfterStartDelayMilliseconds;
        int afterStartMilliseconds =
            (int)Math.Round(
                afterStartSeconds * 1000,
                MidpointRounding.AwayFromZero);
        using (SuspendPlacementAutoSave())
        {
            foreach (PlacementStepRow step in _steps)
            {
                step.DelayAfterMilliseconds = interval;
                if (step.Phase ==
                        PlacementPhase.AfterStart &&
                    step.DelayAfterStartMilliseconds ==
                        previousDefault)
                {
                    step.DelayAfterStartMilliseconds =
                        afterStartMilliseconds;
                }
            }

            _fastPlacementIntervalMilliseconds = interval;
            _fastPlacementAttempts =
                placementAttempts;
            _fastDefaultAfterStartDelayMilliseconds =
                afterStartMilliseconds;
            _fastImpossibilityThresholdMinutes =
                impossibilityThreshold;
        }
        FastEditorPanel.CloseTimingSettings();
        SchedulePlacementAutoSave();
    }
}
