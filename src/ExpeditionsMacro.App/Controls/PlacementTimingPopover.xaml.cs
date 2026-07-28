using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public sealed class PlacementTimingApplyEventArgs(
    string placementIntervalText,
    string defaultAfterStartDelayText,
    string impossibilityThresholdText) : EventArgs
{
    public string PlacementIntervalText { get; } =
        placementIntervalText;

    public string DefaultAfterStartDelayText { get; } =
        defaultAfterStartDelayText;

    public string ImpossibilityThresholdText { get; } =
        impossibilityThresholdText;
}

public partial class PlacementTimingPopover : UserControl
{
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
        bool recordingMode)
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
        PlacementTimingFieldsPanel.Visibility =
            recordingMode
                ? Visibility.Collapsed
                : Visibility.Visible;
        ShowError(string.Empty);
        TextBox initialField =
            recordingMode
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
                ImpossibilityThresholdText.Text));
}
