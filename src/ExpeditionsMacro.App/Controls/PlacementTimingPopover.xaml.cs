using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public sealed class PlacementTimingApplyEventArgs(
    string placementIntervalText,
    string defaultAfterStartDelayText,
    string impossibilityThresholdText,
    bool useManualRecording,
    string? manualRecordingId) : EventArgs
{
    public string PlacementIntervalText { get; } =
        placementIntervalText;

    public string DefaultAfterStartDelayText { get; } =
        defaultAfterStartDelayText;

    public string ImpossibilityThresholdText { get; } =
        impossibilityThresholdText;

    public bool UseManualRecording { get; } =
        useManualRecording;

    public string? ManualRecordingId { get; } =
        manualRecordingId;
}

public sealed record ManualRecordingChoice(
    string Id,
    string Name);

public partial class PlacementTimingPopover : UserControl
{
    private string? _manualRecordingId;

    public PlacementTimingPopover()
    {
        InitializeComponent();
    }

    public event EventHandler<PlacementTimingApplyEventArgs>?
        ApplyRequested;

    public void SetValues(
        int placementIntervalMilliseconds,
        int defaultAfterStartDelayMilliseconds,
        int impossibilityThresholdMinutes,
        bool manualRecordingEnabled,
        string? selectedRecordingId,
        IReadOnlyList<ManualRecordingChoice>
            recordings)
    {
        PlacementIntervalText.Text =
            placementIntervalMilliseconds.ToString(
                CultureInfo.CurrentCulture);
        DefaultAfterStartDelayText.Text =
            (defaultAfterStartDelayMilliseconds / 1000d)
                .ToString(
                    "0.###",
                    CultureInfo.CurrentCulture);
        ImpossibilityThresholdText.Text =
            impossibilityThresholdMinutes.ToString(
                CultureInfo.CurrentCulture);
        ManualRecordingPanel.Visibility =
            manualRecordingEnabled ||
            !string.IsNullOrWhiteSpace(
                selectedRecordingId)
                ? Visibility.Visible
                : Visibility.Collapsed;
        _manualRecordingId =
            !string.IsNullOrWhiteSpace(
                selectedRecordingId)
                ? selectedRecordingId
                : recordings.FirstOrDefault()?.Id;
        UseManualRecordingCheck.IsChecked =
            !string.IsNullOrWhiteSpace(
                selectedRecordingId);
        UpdateManualRecordingState();
        ShowError(string.Empty);
        TextBox initialField =
            UseManualRecordingCheck.IsChecked == true
                ? ImpossibilityThresholdText
                : PlacementIntervalText;
        initialField.Focus();
        initialField.SelectAll();
    }

    public void ShowError(string message)
    {
        TimingErrorText.Text = message;
        TimingErrorText.Visibility =
            string.IsNullOrEmpty(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void Apply_Click(
        object sender,
        RoutedEventArgs e) =>
        ApplyRequested?.Invoke(
            this,
            new PlacementTimingApplyEventArgs(
                PlacementIntervalText.Text,
                DefaultAfterStartDelayText.Text,
                ImpossibilityThresholdText.Text,
                UseManualRecordingCheck
                    .IsChecked == true,
                _manualRecordingId));

    private void UseManualRecordingCheck_Changed(
        object sender,
        RoutedEventArgs e) =>
        UpdateManualRecordingState();

    private void UpdateManualRecordingState()
    {
        PlacementTimingFieldsPanel.Visibility =
            UseManualRecordingCheck.IsChecked ==
            true
                ? Visibility.Collapsed
                : Visibility.Visible;
    }
}
