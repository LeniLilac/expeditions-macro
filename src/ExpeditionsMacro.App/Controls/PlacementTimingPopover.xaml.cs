using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public sealed record PlacementAdvancedEditorValues(
    bool Enabled,
    string UnitSelectionDelayText,
    string PlacementBurstDurationText,
    string BeforeSelectionClickText,
    string BeforeSelectedUnitProofText,
    string ActionKeyIntervalText,
    bool VerifySelectedUnitPanel,
    bool VerifyPrestart,
    string ManualPlaybackStartDelayText);

public sealed class PlacementTimingApplyEventArgs(
    string placementIntervalText,
    string placementAttemptsText,
    string impossibilityThresholdText,
    PlacementAdvancedEditorValues advanced) : EventArgs
{
    public string PlacementIntervalText { get; } =
        placementIntervalText;

    public string PlacementAttemptsText { get; } =
        placementAttemptsText;

    public string ImpossibilityThresholdText { get; } =
        impossibilityThresholdText;

    public PlacementAdvancedEditorValues Advanced { get; } =
        advanced;
}

public partial class PlacementTimingPopover : UserControl
{
    public PlacementTimingPopover()
    {
        InitializeComponent();
    }

    public event EventHandler<PlacementTimingApplyEventArgs>?
        ApplyRequested;

    public event EventHandler? CancelRequested;

    public void SetValues(
        int placementIntervalMilliseconds,
        int placementAttempts,
        int defaultAfterStartDelayMilliseconds,
        int impossibilityThresholdMinutes,
        bool recordingMode,
        PlacementAdvancedSettings advanced)
    {
        PlacementIntervalText.Text =
            placementIntervalMilliseconds.ToString(
                CultureInfo.CurrentCulture);
        PlacementAttemptsText.Text =
            placementAttempts.ToString(
                CultureInfo.CurrentCulture);
        ImpossibilityThresholdText.Text =
            impossibilityThresholdMinutes.ToString(
                CultureInfo.CurrentCulture);
        AdvancedModeCheck.IsChecked = advanced.Enabled;
        UnitSelectionDelayText.Text =
            Format(advanced.UnitSelectionDelayMilliseconds);
        PlacementBurstDurationText.Text =
            Format(
                advanced.PlacementBurstDurationMilliseconds);
        BeforeSelectionClickText.Text =
            Format(
                advanced.BeforeSelectionClickMilliseconds);
        BeforeSelectedUnitProofText.Text =
            Format(
                advanced
                    .BeforeSelectedUnitProofMilliseconds);
        ActionKeyIntervalText.Text =
            Format(
                advanced.ActionKeyIntervalMilliseconds);
        VerifySelectedUnitPanelCheck.IsChecked =
            advanced.VerifySelectedUnitPanelBeforeActions;
        VerifyPrestartCheck.IsChecked =
            advanced.VerifyPrestartBeforeManualPlayback;
        ManualPlaybackStartDelayText.Text =
            Format(
                advanced
                    .ManualPlaybackStartDelayMilliseconds);
        PlacementTimingFieldsPanel.Visibility =
            recordingMode
                ? Visibility.Collapsed
                : Visibility.Visible;
        AdvancedStepFieldsPanel.Visibility =
            recordingMode
                ? Visibility.Collapsed
                : Visibility.Visible;
        AdvancedRecordingFieldsPanel.Visibility =
            recordingMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        UpdateAdvancedVisibility();
        ShowError(string.Empty);
        TextBox initialField =
            recordingMode
                ? ImpossibilityThresholdText
                : PlacementIntervalText;
        initialField.Focus();
        initialField.SelectAll();
        TimingScrollViewer.ScrollToTop();
    }

    public void ShowError(string message)
    {
        TimingErrorText.Text = message;
        TimingErrorText.Visibility =
            string.IsNullOrEmpty(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    internal void ScrollAdvancedSettingsIntoView()
    {
        UpdateLayout();
        AdvancedModePanel.BringIntoView();
    }

    private void Apply_Click(
        object sender,
        RoutedEventArgs e) =>
        ApplyRequested?.Invoke(
            this,
            new PlacementTimingApplyEventArgs(
                PlacementIntervalText.Text,
                PlacementAttemptsText.Text,
                ImpossibilityThresholdText.Text,
                new PlacementAdvancedEditorValues(
                    AdvancedModeCheck.IsChecked == true,
                    UnitSelectionDelayText.Text,
                    PlacementBurstDurationText.Text,
                    BeforeSelectionClickText.Text,
                    BeforeSelectedUnitProofText.Text,
                    ActionKeyIntervalText.Text,
                    VerifySelectedUnitPanelCheck.IsChecked ==
                        true,
                    VerifyPrestartCheck.IsChecked == true,
                    ManualPlaybackStartDelayText.Text)));

    private void Cancel_Click(
        object sender,
        RoutedEventArgs e) =>
        CancelRequested?.Invoke(
            this,
            EventArgs.Empty);

    private void AdvancedModeCheck_Changed(
        object sender,
        RoutedEventArgs e) =>
        UpdateAdvancedVisibility();

    private void UpdateAdvancedVisibility()
    {
        if (AdvancedFieldsPanel is null)
        {
            return;
        }
        AdvancedFieldsPanel.Visibility =
            AdvancedModeCheck.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static string Format(int value) =>
        value.ToString(CultureInfo.CurrentCulture);
}
