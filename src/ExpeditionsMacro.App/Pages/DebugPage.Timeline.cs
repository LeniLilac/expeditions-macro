using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage
{
    private void DebugCheckpoint_Added(
        DebugCheckpoint checkpoint) =>
        Dispatcher.BeginInvoke(() => AddCheckpoint(checkpoint));

    private void AddCheckpoint(DebugCheckpoint checkpoint)
    {
        bool select = _followLive ||
            TimelineList.SelectedIndex ==
            _timeline.Count - 1;
        DebugCheckpointRow row = new(
            checkpoint.Sequence,
            checkpoint.Title,
            checkpoint.Detail,
            checkpoint.State,
            checkpoint.Confidence,
            checkpoint.Frame is null
                ? null
                : BitmapSourceFactory.Create(checkpoint.Frame));
        _timeline.Add(row);
        if (select)
        {
            _followLive = true;
            TimelineList.SelectedIndex = _timeline.Count - 1;
            TimelineList.ScrollIntoView(row);
        }
    }

    private void Timeline_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        ShowCheckpoint(TimelineList.SelectedIndex);

    private void Previous_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TimelineList.SelectedIndex > 0)
        {
            _followLive = false;
            TimelineList.SelectedIndex--;
            TimelineList.ScrollIntoView(
                TimelineList.SelectedItem);
        }
    }

    private void NextReview_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TimelineList.SelectedIndex + 1 <
            _timeline.Count)
        {
            TimelineList.SelectedIndex++;
            _followLive =
                TimelineList.SelectedIndex ==
                _timeline.Count - 1;
            TimelineList.ScrollIntoView(
                TimelineList.SelectedItem);
        }
    }

    private void Pause_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugStepMode mode = SelectedStepMode();
        if (mode == DebugStepMode.Continuous)
        {
            mode = DebugStepMode.BeforeActions;
        }
        _services.DebugCheckpoints.PauseAtNext(mode);
    }

    private void Step_Click(
        object sender,
        RoutedEventArgs e) =>
        _services.DebugCheckpoints.Step();

    private void Resume_Click(
        object sender,
        RoutedEventArgs e) =>
        _services.DebugCheckpoints.Resume();

    private void Stop_Click(
        object sender,
        RoutedEventArgs e) =>
        _services.Coordinator.Cancel();

    private void ShowCheckpoint(int index)
    {
        if (index < 0 || index >= _timeline.Count)
        {
            CheckpointImage.Source = null;
            NoFrameText.Visibility = Visibility.Visible;
            CheckpointTitle.Text = "Checkpoint timeline";
            CheckpointPosition.Text = $"0 of {_timeline.Count}";
            return;
        }
        DebugCheckpointRow row = _timeline[index];
        CheckpointImage.Source = row.Frame;
        NoFrameText.Visibility = row.Frame is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        CheckpointTitle.Text = row.Title;
        CheckpointPosition.Text =
            $"{index + 1} of {_timeline.Count}";
        PreviousButton.IsEnabled = index > 0;
        NextReviewButton.IsEnabled =
            index + 1 < _timeline.Count;
    }

    private void DebugCheckpoint_StateChanged(
        object? sender,
        EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateControls);

    private void Coordinator_StateChanged(
        object? sender,
        EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateControls);

    private void UpdateControls()
    {
        bool busy = _services.Coordinator.IsBusy;
        bool debugActive =
            _services.DebugCheckpoints.IsActive;
        bool waiting =
            _services.DebugCheckpoints.IsWaiting;
        RunNavigationButton.IsEnabled = !busy;
        RunTeamButton.IsEnabled = !busy;
        InspectScreenButton.IsEnabled = !busy;
        NormalizeClientButton.IsEnabled = !busy;
        NavigationStartCombo.IsEnabled = !busy;
        NavigationModeCombo.IsEnabled = !busy;
        NavigationPresetCombo.IsEnabled = !busy;
        ChallengeTypeCombo.IsEnabled = !busy;
        TeamCombo.IsEnabled = !busy;
        StepModeCombo.IsEnabled = !busy;
        PauseButton.IsEnabled = debugActive && !waiting;
        StepButton.IsEnabled = debugActive && waiting;
        ResumeButton.IsEnabled = debugActive;
        StopButton.IsEnabled = busy;
        DebugStateText.Text = waiting
            ? "Paused before the next live step"
            : busy
                ? _services.Coordinator.Description
                : "Ready";
        DebugStateDot.Fill = (Brush)FindResource(
            waiting
                ? "WarningBrush"
                : busy
                    ? "SuccessBrush"
                    : "FaintBrush");
    }
}
