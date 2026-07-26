using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public sealed class PlacementTimingApplyEventArgs(
    string placementIntervalText,
    string defaultAfterStartDelayText) : EventArgs
{
    public string PlacementIntervalText { get; } =
        placementIntervalText;

    public string DefaultAfterStartDelayText { get; } =
        defaultAfterStartDelayText;
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
        int defaultAfterStartDelayMilliseconds)
    {
        PlacementIntervalText.Text =
            placementIntervalMilliseconds.ToString(
                CultureInfo.CurrentCulture);
        DefaultAfterStartDelayText.Text =
            (defaultAfterStartDelayMilliseconds / 1000d)
                .ToString(
                    "0.###",
                    CultureInfo.CurrentCulture);
        ShowError(string.Empty);
        PlacementIntervalText.Focus();
        PlacementIntervalText.SelectAll();
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
                DefaultAfterStartDelayText.Text));
}
