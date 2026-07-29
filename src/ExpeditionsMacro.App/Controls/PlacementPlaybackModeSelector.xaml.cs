using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public sealed class PlacementPlaybackModeChangedEventArgs(
    bool recordingMode) : EventArgs
{
    public bool RecordingMode { get; } = recordingMode;
}

public partial class PlacementPlaybackModeSelector :
    UserControl
{
    private bool _updating;
    private bool _featureEnabled;
    private bool _hasAvailableRecording;
    private bool _interactionEnabled = true;

    public PlacementPlaybackModeSelector()
    {
        InitializeComponent();
    }

    public event EventHandler<
        PlacementPlaybackModeChangedEventArgs>?
        SelectionChanged;

    public void SetState(
        bool featureEnabled,
        bool recordingMode,
        bool hasAvailableRecording)
    {
        _featureEnabled = featureEnabled;
        _hasAvailableRecording =
            hasAvailableRecording;
        _updating = true;
        try
        {
            Visibility =
                featureEnabled ||
                recordingMode
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            RecordingModeButton.IsChecked =
                recordingMode;
            StepModeButton.IsChecked =
                !recordingMode;
        }
        finally
        {
            _updating = false;
        }
        UpdateAvailability();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;
        UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        bool recordingMode =
            RecordingModeButton.IsChecked == true;
        RecordingModeButton.IsEnabled =
            _interactionEnabled &&
            _featureEnabled &&
            (_hasAvailableRecording ||
             recordingMode);
        StepModeButton.IsEnabled =
            _interactionEnabled;
        DisabledFeatureText.Visibility =
            recordingMode &&
            !_featureEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;

        string recordingGuidance =
            !_featureEnabled
                ? "Enable advanced manual recordings in Settings before choosing Recording Mode."
                : !_hasAvailableRecording &&
                  !recordingMode
                    ? "Create a recording on the Recordings page before choosing Recording Mode."
                    : "Replay a saved manual recording instead of the Match Steps timeline.";
        RecordingModeButton.ToolTip =
            recordingGuidance;
        AutomationProperties.SetHelpText(
            RecordingModeButton,
            recordingGuidance);
    }

    private void ModeButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        SelectionChanged?.Invoke(
            this,
            new PlacementPlaybackModeChangedEventArgs(
                ReferenceEquals(
                    sender,
                    RecordingModeButton)));
    }
}
