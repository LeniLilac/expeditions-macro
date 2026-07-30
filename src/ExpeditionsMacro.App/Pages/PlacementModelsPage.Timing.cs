using System.Globalization;
using System.Windows;
using System.Windows.Input;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private const int MaximumPlacementIntervalMilliseconds =
        60_000;
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
        _fastAdvancedSettings = new();
    }

    private void FastTimingSettingsOpening(
        object? sender,
        EventArgs e)
    {
        MatchSettingsOverlay.Visibility =
            Visibility.Visible;
        MatchSettingsDialog.SetValues(
            _fastPlacementIntervalMilliseconds,
            _fastPlacementAttempts,
            _fastDefaultAfterStartDelayMilliseconds,
            _fastImpossibilityThresholdMinutes,
            !string.IsNullOrWhiteSpace(
                _fastManualRecordingId),
            _fastAdvancedSettings);
    }

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
            MatchSettingsDialog.ShowError(
                $"Enter a placement interval from 0 to {MaximumPlacementIntervalMilliseconds:N0} ms.");
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
            MatchSettingsDialog.ShowError(
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
            MatchSettingsDialog.ShowError(
                $"Enter an impossibility threshold from 0 to {PlacementModel.MaximumImpossibilityThresholdMinutes} minutes.");
            return;
        }
        PlacementAdvancedSettings advanced;
        try
        {
            advanced =
                BuildAdvancedSettings(e.Advanced);
        }
        catch (FormatException error)
        {
            MatchSettingsDialog.ShowError(
                error.Message);
            return;
        }
        using (SuspendPlacementAutoSave())
        {
            foreach (PlacementStepRow step in _steps)
            {
                if (!step.IsStartGame)
                {
                    step.DelayAfterMilliseconds =
                        interval;
                }
            }

            _fastPlacementIntervalMilliseconds = interval;
            _fastPlacementAttempts =
                placementAttempts;
            _fastImpossibilityThresholdMinutes =
                impossibilityThreshold;
            _fastAdvancedSettings = advanced;
        }
        CloseMatchSettingsDialog();
        SchedulePlacementAutoSave();
    }

    private void MatchSettingsDialog_CancelRequested(
        object? sender,
        EventArgs e) =>
        CloseMatchSettingsDialog();

    private void MatchSettingsOverlay_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }
        CloseMatchSettingsDialog();
        e.Handled = true;
    }

    private void CloseMatchSettingsDialog()
    {
        MatchSettingsOverlay.Visibility =
            Visibility.Collapsed;
        FastTimingButton.Focus();
    }

    internal void SetSnapshotMatchSettings(
        int placementIntervalMilliseconds,
        int placementAttempts,
        int defaultAfterStartDelayMilliseconds,
        int impossibilityThresholdMinutes,
        bool recordingMode,
        PlacementAdvancedSettings advanced)
    {
        MatchSettingsOverlay.Visibility =
            Visibility.Visible;
        MatchSettingsDialog.SetValues(
            placementIntervalMilliseconds,
            placementAttempts,
            defaultAfterStartDelayMilliseconds,
            impossibilityThresholdMinutes,
            recordingMode,
            advanced);
    }

    internal void ClearSnapshotMatchSettings()
    {
        MatchSettingsOverlay.Visibility =
            Visibility.Collapsed;
        MatchStepEditorOverlay.Visibility =
            Visibility.Collapsed;
    }

    internal void ScrollSnapshotAdvancedSettingsIntoView() =>
        MatchSettingsDialog
            .ScrollAdvancedSettingsIntoView();

    private static PlacementAdvancedSettings
        BuildAdvancedSettings(
            PlacementAdvancedEditorValues values)
    {
        PlacementAdvancedSettings settings = new()
        {
            Enabled = values.Enabled,
            UnitSelectionDelayMilliseconds =
                ParseInteger(
                    values.UnitSelectionDelayText,
                    0,
                    PlacementAdvancedSettings
                        .MaximumActionDelayMilliseconds,
                    "Unit selection delay"),
            PlacementBurstDurationMilliseconds =
                ParseInteger(
                    values.PlacementBurstDurationText,
                    0,
                    PlacementAdvancedSettings
                        .MaximumActionDelayMilliseconds,
                    "Placement click burst"),
            BeforeSelectionClickMilliseconds =
                ParseInteger(
                    values.BeforeSelectionClickText,
                    0,
                    PlacementAdvancedSettings
                        .MaximumActionDelayMilliseconds,
                    "Before-selection delay"),
            BeforeSelectedUnitProofMilliseconds =
                ParseInteger(
                    values.BeforeSelectedUnitProofText,
                    0,
                    PlacementAdvancedSettings
                        .MaximumActionDelayMilliseconds,
                    "Selected-unit check delay"),
            ActionKeyIntervalMilliseconds =
                ParseInteger(
                    values.ActionKeyIntervalText,
                    0,
                    PlacementAdvancedSettings
                        .MaximumActionDelayMilliseconds,
                    "Action key interval"),
            VerifySelectedUnitPanelBeforeActions =
                values.VerifyPlacementActionProof,
            VerifySelectedUnitPanelBeforeReconfigureActions =
                values.VerifyReconfigureActionProof,
            VerifyUpgradeUnitReadiness =
                values.VerifyUpgradeUnitReadiness,
            VerifyPrestartBeforeManualPlayback =
                values.VerifyPrestart,
            ManualPlaybackStartDelayMilliseconds =
                ParseInteger(
                    values.ManualPlaybackStartDelayText,
                    0,
                    PlacementAdvancedSettings
                        .MaximumPlaybackStartDelayMilliseconds,
                    "Playback start delay"),
        };
        settings.Validate();
        return settings;
    }
}
