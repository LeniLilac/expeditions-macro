using System.Windows.Controls;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private int _fastImpossibilityThresholdMinutes;
    private string? _fastManualRecordingId;
    private IReadOnlyList<ManualRecordingChoice>
        _manualRecordingChoices = [];

    private void ResetFastRecordingSettings()
    {
        _fastImpossibilityThresholdMinutes = 0;
        _fastManualRecordingId = null;
        UpdateFastManualRecordingEditor();
    }

    private async Task RefreshManualRecordingChoicesAsync()
    {
        IReadOnlyList<ManualInputRecording> recordings =
            await _services.ManualRecordings
                .ListAsync()
                .ConfigureAwait(false);
        _manualRecordingChoices = recordings
            .Select(
                recording =>
                    new ManualRecordingChoice(
                        recording.Id,
                        recording.Name))
            .ToArray();
    }

    private void UpdateFastManualRecordingEditor(
        bool? featureEnabledOverride = null)
    {
        bool recordingMode =
            !string.IsNullOrWhiteSpace(
                _fastManualRecordingId);
        FastEditorPanel.SetManualRecordingMode(
            featureEnabledOverride ??
            _services.Settings
                .ManualInputRecordingEnabled,
            recordingMode,
            _fastManualRecordingId,
            _manualRecordingChoices);
        UpdateFastPositionButtonVisibility(
            CurrentFastTarget());
    }

    private void FastPlaybackMode_Changed(
        object? sender,
        PlacementPlaybackModeChangedEventArgs e)
    {
        string? nextRecordingId =
            e.RecordingMode
                ? _fastManualRecordingId ??
                  _manualRecordingChoices
                      .FirstOrDefault()?.Id
                : null;
        if (e.RecordingMode &&
            string.IsNullOrWhiteSpace(
                nextRecordingId))
        {
            FastStatusText.Text =
                "Create a recording on the Recordings page before choosing Recording Mode.";
            UpdateFastManualRecordingEditor();
            return;
        }
        if (string.Equals(
                nextRecordingId,
                _fastManualRecordingId,
                StringComparison.Ordinal))
        {
            return;
        }

        _fastManualRecordingId =
            nextRecordingId;
        UpdateFastManualRecordingEditor();
        SchedulePlacementAutoSave();
    }

    private void FastManualRecording_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        string? recordingId =
            FastEditorPanel.SelectedManualRecordingId;
        if (string.IsNullOrWhiteSpace(recordingId) ||
            recordingId == _fastManualRecordingId)
        {
            return;
        }

        _fastManualRecordingId = recordingId;
        SchedulePlacementAutoSave();
    }
}
