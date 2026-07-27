using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public partial class PlacementRouteControls : UserControl
{
    private bool _updatingRecording;

    public PlacementRouteControls()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? UnitChanged;

    public event RoutedEventHandler? PhaseChanged;

    public event SelectionChangedEventHandler?
        RecordingChanged;

    internal ComboBox TeamSelector => TeamCombo;

    internal RadioButton BeforeStartSelector =>
        BeforeStartButton;

    internal RadioButton AfterStartSelector =>
        AfterStartButton;

    internal RadioButton Unit1Selector => Unit1Button;

    internal RadioButton Unit2Selector => Unit2Button;

    internal RadioButton Unit3Selector => Unit3Button;

    internal RadioButton Unit4Selector => Unit4Button;

    internal RadioButton Unit5Selector => Unit5Button;

    internal RadioButton Unit6Selector => Unit6Button;

    internal ComboBox RecordingSelector =>
        RecordingCombo;

    internal string? SelectedRecordingId =>
        (RecordingCombo.SelectedItem as
            ManualRecordingChoice)?.Id;

    internal void SetManualRecordingMode(
        bool enabled,
        string? selectedRecordingId,
        IReadOnlyList<ManualRecordingChoice>
            recordings)
    {
        PhasePanel.Visibility =
            enabled
                ? Visibility.Collapsed
                : Visibility.Visible;
        UnitPanel.Visibility =
            PhasePanel.Visibility;
        RecordingPanel.Visibility =
            enabled
                ? Visibility.Visible
                : Visibility.Collapsed;

        _updatingRecording = true;
        try
        {
            ManualRecordingChoice? selected =
                recordings.FirstOrDefault(
                    recording =>
                        recording.Id ==
                        selectedRecordingId);
            IReadOnlyList<ManualRecordingChoice> choices =
                selected is null &&
                !string.IsNullOrWhiteSpace(
                    selectedRecordingId)
                    ? recordings
                        .Prepend(
                            new ManualRecordingChoice(
                                selectedRecordingId,
                                "Unavailable recording"))
                        .ToArray()
                    : recordings;
            RecordingCombo.ItemsSource = choices;
            RecordingCombo.SelectedItem =
                choices.FirstOrDefault(
                    recording =>
                        recording.Id ==
                        selectedRecordingId);
        }
        finally
        {
            _updatingRecording = false;
        }
    }

    private void UnitButton_Checked(
        object sender,
        RoutedEventArgs e) =>
        UnitChanged?.Invoke(sender, e);

    private void PhaseButton_Checked(
        object sender,
        RoutedEventArgs e) =>
        PhaseChanged?.Invoke(sender, e);

    private void RecordingCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_updatingRecording)
        {
            RecordingChanged?.Invoke(sender, e);
        }
    }
}
