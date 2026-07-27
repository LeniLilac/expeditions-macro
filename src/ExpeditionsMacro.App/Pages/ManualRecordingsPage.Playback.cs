using System.Windows;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class ManualRecordingsPage
{
    private ManualPlaybackTestState _playbackState;
    private string? _playbackRecordingName;

    private ManualRecordingRow? SelectedRecording =>
        RecordingsList.SelectedItem as
            ManualRecordingRow;

    private bool OwnsPlaybackOperation =>
        _playbackState is
            ManualPlaybackTestState.Armed or
            ManualPlaybackTestState.Running or
            ManualPlaybackTestState.Stopping;

    private void TestRecording_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (OwnsPlaybackOperation)
        {
            _services.Coordinator.Cancel();
            HandleCoordinatorStateChanged();
            return;
        }
        if (_services.Coordinator.IsBusy ||
            SelectedRecording is not { } row)
        {
            return;
        }

        ManualInputRecording recording =
            row.Recording
                .CreateImmutableSnapshot();
        ManualRecordingRow armedRow =
            new(recording);
        SetPlaybackState(
            ManualPlaybackTestState.Armed,
            armedRow.Name);
        try
        {
            _services.Coordinator.Arm(
                $"Playing {armedRow.Name}",
                token => PlayTestAsync(
                    armedRow,
                    token),
                new DeepDebugOperationContext
                {
                    OperationSettings = new
                    {
                        Action =
                            "manual_input_playback_test",
                        RecordingId =
                            recording.Id,
                    },
                });
        }
        catch
        {
            SetPlaybackState(
                ManualPlaybackTestState.Ready,
                armedRow.Name);
            throw;
        }
        UpdatePlaybackUi();
        UpdateBusyState();
    }

    private async Task PlayTestAsync(
        ManualRecordingRow row,
        CancellationToken token)
    {
        try
        {
            RobloxWindow window =
                _services.Automation.FindWindow() ??
                throw new RobloxSessionUnavailableException(
                    "Open Roblox before testing a recording.");
            await _services.Automation
                .ResizeClientAsync(
                    window,
                    RobloxClientProfile.Width,
                    RobloxClientProfile.Height,
                    token)
                .ConfigureAwait(false);
            await _services.ManualPlayback.PlayAsync(
                    window,
                    row.Recording,
                    token)
                .ConfigureAwait(false);
            await Dispatcher.InvokeAsync(
                () => SetPlaybackState(
                    ManualPlaybackTestState.Completed,
                    row.Name));
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            await Dispatcher.InvokeAsync(
                () => SetPlaybackState(
                    ManualPlaybackTestState.Canceled,
                    row.Name));
            throw;
        }
        catch (Exception error)
        {
            await Dispatcher.InvokeAsync(
                () => SetPlaybackState(
                    ManualPlaybackTestState.Failed,
                    row.Name,
                    error.Message));
            throw;
        }
        finally
        {
            await Dispatcher.InvokeAsync(
                UpdateBusyState);
        }
    }

    private void HandleCoordinatorStateChanged()
    {
        if (OwnsPlaybackOperation)
        {
            switch (_services.Coordinator.State)
            {
                case OperationState.Running:
                    SetPlaybackState(
                        ManualPlaybackTestState.Running,
                        _playbackRecordingName);
                    break;
                case OperationState.Stopping:
                    SetPlaybackState(
                        ManualPlaybackTestState.Stopping,
                        _playbackRecordingName);
                    break;
                case OperationState.Idle:
                    SetPlaybackState(
                        _playbackState ==
                            ManualPlaybackTestState.Armed
                            ? ManualPlaybackTestState.Disarmed
                            : ManualPlaybackTestState.Canceled,
                        _playbackRecordingName);
                    break;
            }
        }
        UpdatePlaybackUi();
        UpdateBusyState();
    }

    private void SetPlaybackState(
        ManualPlaybackTestState state,
        string? recordingName,
        string? error = null)
    {
        _playbackState = state;
        _playbackRecordingName =
            recordingName;
        PlaybackStatusText.Text =
            DescribePlaybackState(
                state,
                recordingName,
                error);
        UpdatePlaybackUi();
    }

    private void UpdatePlaybackUi()
    {
        (string label,
            LucideIconKind icon,
            string automationName) =
            _playbackState switch
            {
                ManualPlaybackTestState.Armed =>
                    (
                        "Disarm playback",
                        LucideIconKind.X,
                        "Disarm selected recording playback"),
                ManualPlaybackTestState.Running =>
                    (
                        "Stop playback",
                        LucideIconKind.Square,
                        "Stop selected recording playback"),
                ManualPlaybackTestState.Stopping =>
                    (
                        "Stopping…",
                        LucideIconKind.Square,
                        "Stopping selected recording playback"),
                _ =>
                    (
                        "Arm playback",
                        LucideIconKind.CircleDot,
                        "Arm selected recording playback"),
            };
        TestRecordingButton.Content = label;
        Lucide.SetIcon(
            TestRecordingButton,
            icon);
        System.Windows.Automation
            .AutomationProperties.SetName(
                TestRecordingButton,
                automationName);
    }

    private string DescribePlaybackState(
        ManualPlaybackTestState state,
        string? recordingName,
        string? error)
    {
        string name =
            string.IsNullOrWhiteSpace(recordingName)
                ? "the selected recording"
                : recordingName;
        return state switch
        {
            ManualPlaybackTestState.Ready
                when recordingName is null =>
                "Select a recording to test.",
            ManualPlaybackTestState.Ready =>
                $"{name} is ready to test.",
            ManualPlaybackTestState.Armed =>
                $"Armed. Focus Roblox, then press {_services.Hotkey.DisplayName} to start {name}.",
            ManualPlaybackTestState.Running =>
                $"Playing {name}. Press {_services.Hotkey.DisplayName} or Stop playback to cancel.",
            ManualPlaybackTestState.Stopping =>
                $"Stopping {name} and releasing held input.",
            ManualPlaybackTestState.Completed =>
                $"{name} playback finished.",
            ManualPlaybackTestState.Canceled =>
                $"{name} playback stopped.",
            ManualPlaybackTestState.Disarmed =>
                $"{name} playback disarmed.",
            ManualPlaybackTestState.Failed =>
                string.IsNullOrWhiteSpace(error)
                    ? $"{name} playback failed."
                    : error,
            _ => "Select a recording to test.",
        };
    }

    private enum ManualPlaybackTestState
    {
        Ready,
        Armed,
        Running,
        Stopping,
        Completed,
        Canceled,
        Disarmed,
        Failed,
    }
}
