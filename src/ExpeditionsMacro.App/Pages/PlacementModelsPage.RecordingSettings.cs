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

    private void UpdateFastManualRecordingEditor() =>
        FastEditorPanel.SetManualRecordingMode(
            !string.IsNullOrWhiteSpace(
                _fastManualRecordingId),
            _fastManualRecordingId,
            _manualRecordingChoices);

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
        FastStatusText.Text =
            "Recording updated. Save setup to keep it.";
    }
}
