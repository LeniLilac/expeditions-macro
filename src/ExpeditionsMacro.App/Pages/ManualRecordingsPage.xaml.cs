using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class ManualRecordingsPage :
    UserControl,
    IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<ManualRecordingRow>
        _recordings = [];
    private bool _snapshotInteractionLocked;

    public ManualRecordingsPage(
        AppServices services)
    {
        _services = services;
        InitializeComponent();
        RecordingsList.ItemsSource = _recordings;
        _services.Coordinator.StateChanged +=
            (_, _) => Dispatcher.BeginInvoke(
                HandleCoordinatorStateChanged);
    }

    public Func<Task>? IdleHotkeyAction =>
        StartRecordingAsync;

    public async Task OnShownAsync()
    {
        await RefreshAsync();
        UpdateBusyState();
        RecordingStatusText.Text =
            $"Ready. Press {_services.Hotkey.DisplayName} to start.";
        UpdatePlaybackUi();
    }

    internal void SetSnapshotState(
        ManualRecordingsSnapshotState state =
            ManualRecordingsSnapshotState.Ready)
    {
        _recordings.Clear();
        _recordings.Add(
            new ManualRecordingRow(
                new ManualInputRecording
                {
                    Id = "school-grounds-route",
                    Name = "School Grounds route",
                    InitialClientX = 404,
                    InitialClientY = 305,
                    CreatedAtUtc =
                        new DateTimeOffset(
                            2026,
                            7,
                            27,
                            12,
                            0,
                            0,
                            TimeSpan.Zero),
                    DurationMicroseconds =
                        42_600_000,
                    Events =
                    [
                        new ManualInputEvent
                        {
                            OffsetMicroseconds = 0,
                            Kind =
                                ManualInputEventKind.KeyDown,
                            VirtualKey = 'W',
                            ScanCode = 17,
                        },
                    ],
                }));
        RecordingsList.SelectedIndex = 0;
        RecordingCountText.Text = "1 recording";
        EmptyRecordingsText.Visibility =
            Visibility.Collapsed;
        RecordingStatusText.Text =
            state switch
            {
                ManualRecordingsSnapshotState
                    .ArmedRecording =>
                    "Armed. Focus Roblox, place the pointer inside it, then press F6. Press it again to stop and save.",
                ManualRecordingsSnapshotState
                    .RunningRecording =>
                    "Recording. Press F6 to stop and save.",
                _ =>
                    "Ready. Recording and playback own Roblox input exclusively.",
            };
        _snapshotInteractionLocked =
            state is not
                ManualRecordingsSnapshotState.Ready;
        SetPlaybackState(
            state switch
            {
                ManualRecordingsSnapshotState
                    .ArmedPlayback =>
                    ManualPlaybackTestState.Armed,
                ManualRecordingsSnapshotState
                    .RunningPlayback =>
                    ManualPlaybackTestState.Running,
                _ =>
                    ManualPlaybackTestState.Ready,
            },
            "School Grounds route");
        UpdateBusyState();
    }

    private async Task RefreshAsync(
        string? selectedId = null)
    {
        IReadOnlyList<ManualInputRecording> recordings =
            await _services.ManualRecordings
                .ListAsync()
                .ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            _recordings.Clear();
            foreach (ManualInputRecording recording in
                     recordings)
            {
                _recordings.Add(
                    new ManualRecordingRow(
                        recording));
            }
            RecordingsList.SelectedItem =
                selectedId is null
                    ? _recordings.FirstOrDefault()
                    : _recordings.FirstOrDefault(
                        row =>
                            row.Recording.Id ==
                            selectedId);
            RecordingCountText.Text =
                _recordings.Count == 1
                    ? "1 recording"
                    : $"{_recordings.Count} recordings";
            EmptyRecordingsText.Visibility =
                _recordings.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            UpdateBusyState();
        });
    }

    private async void StartRecording_Click(
        object sender,
        RoutedEventArgs e)
    {
        ManualInputCaptureOptions? options =
            await CreateCaptureOptionsAsync();
        if (options is null)
        {
            return;
        }
        _services.Coordinator.Arm(
            $"Recording {options.RecordingName}",
            token => RecordAsync(options, token),
            DebugContext(options));
        RecordingStatusText.Text =
            $"Armed. Focus Roblox, place the pointer inside it, then press {_services.Hotkey.DisplayName}. Press it again to stop and save.";
    }

    private async Task StartRecordingAsync()
    {
        if (_services.Coordinator.IsBusy)
        {
            return;
        }

        ManualInputCaptureOptions? options =
            await CreateCaptureOptionsAsync();
        if (options is null)
        {
            return;
        }

        RecordingStatusText.Text =
            $"Recording will start now. Press {_services.Hotkey.DisplayName} again to stop and save.";
        await _services.Coordinator.RunNowAsync(
            $"Recording {options.RecordingName}",
            token => RecordAsync(options, token),
            DebugContext(options));
    }

    private async Task<ManualInputCaptureOptions?>
        CreateCaptureOptionsAsync()
    {
        string requestedName =
            RecordingNameText.Text.Trim();
        try
        {
            string baseId =
                ModelId.FromName(requestedName);
            IReadOnlyList<ManualInputRecording>
                existing =
                    await _services.ManualRecordings
                        .ListAsync()
                        .ConfigureAwait(true);
            HashSet<string> ids =
                existing.Select(recording =>
                        recording.Id)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);
            string id = baseId;
            string name = requestedName;
            for (int suffix = 2;
                 ids.Contains(id);
                 suffix++)
            {
                name =
                    $"{requestedName} ({suffix})";
                id = ModelId.FromName(name);
            }

            ManualInputCaptureOptions options = new()
            {
                RecordingId = id,
                RecordingName = name,
                IgnoredVirtualKeys =
                [
                    _services.Settings
                        .MacroHotkeyVirtualKey,
                ],
            };
            options.Validate();
            return options;
        }
        catch (Exception error)
        {
            RecordingStatusText.Text = error.Message;
            return null;
        }
    }

    private static DeepDebugOperationContext DebugContext(
        ManualInputCaptureOptions options) =>
        new()
        {
            OperationSettings = new
            {
                Action =
                    "manual_input_recording",
                options.RecordingId,
                options.RecordingName,
            },
        };

    private async Task RecordAsync(
        ManualInputCaptureOptions options,
        CancellationToken stopToken)
    {
        try
        {
            RobloxWindow window =
                _services.Automation.FindWindow() ??
                throw new RobloxSessionUnavailableException(
                    "Open Roblox before starting a manual recording.");
            await _services.Automation.ResizeClientAsync(
                    window,
                    RobloxClientProfile.Width,
                    RobloxClientProfile.Height,
                    stopToken)
                .ConfigureAwait(false);
            ManualInputRecording recording =
                await _services.ManualRecorder.RecordAsync(
                        window,
                        options,
                        stopToken)
                    .ConfigureAwait(false);
            if (recording.Events.Count == 0)
            {
                await SetStatusAsync(
                    "Nothing was recorded, so no file was saved.");
                return;
            }

            await _services.ManualRecordings.SaveAsync(
                    recording,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await RefreshAsync(recording.Id);
            await SetStatusAsync(
                $"Saved {recording.Events.Count:N0} input events over {FormatDuration(recording.DurationMicroseconds)}.");
        }
        catch (Exception error)
        {
            await SetStatusAsync(error.Message);
            throw;
        }
        finally
        {
            await Dispatcher.InvokeAsync(
                UpdateBusyState);
        }
    }

    private async void DeleteRecording_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy ||
            RecordingsList.SelectedItem is not
                ManualRecordingRow row)
        {
            return;
        }

        IReadOnlyList<PlacementModel> references =
            await _services.PlacementModels
                .ListReferencingManualRecordingAsync(
                    row.Recording.Id)
                .ConfigureAwait(true);
        if (references.Count != 0)
        {
            string names = string.Join(
                ", ",
                references.Select(model =>
                    model.Name));
            RecordingStatusText.Text =
                $"Cannot delete {row.Name}. It is used by Placement Setup route(s): {names}. Turn off Use manual recording for those routes first.";
            return;
        }

        if (MessageBox.Show(
                Window.GetWindow(this),
                $"Delete '{row.Name}'?",
                "Delete recording",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) !=
            MessageBoxResult.Yes)
        {
            return;
        }

        _services.ManualRecordings.Delete(
            row.Recording.Id);
        await RefreshAsync();
        RecordingStatusText.Text =
            $"{row.Name} was deleted.";
    }

    private void RecordingsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!OwnsPlaybackOperation)
        {
            SetPlaybackState(
                ManualPlaybackTestState.Ready,
                SelectedRecording?.Name);
        }
        UpdateBusyState();
    }

    private void UpdateBusyState()
    {
        bool busy =
            _services.Coordinator.IsBusy;
        bool interactionLocked =
            busy ||
            _snapshotInteractionLocked;
        StartRecordingButton.IsEnabled =
            !interactionLocked;
        RecordingNameText.IsEnabled =
            !interactionLocked;
        RecordingsList.IsHitTestVisible =
            !interactionLocked;
        RecordingsList.Focusable =
            !interactionLocked;
        RecordingsList.IsTabStop =
            !interactionLocked;
        System.Windows.Automation
            .AutomationProperties.SetHelpText(
                RecordingsList,
                interactionLocked
                    ? "Saved recordings are locked while recording or playback owns Roblox input."
                    : "Select a saved recording.");
        System.Windows.Automation
            .AutomationProperties.SetItemStatus(
                RecordingsList,
                interactionLocked
                    ? "Locked"
                    : "Available");
        TestRecordingButton.IsEnabled =
            OwnsPlaybackOperation
                ? _services.Coordinator.State !=
                    OperationState.Stopping
                : !interactionLocked &&
                    SelectedRecording is not null;
        DeleteRecordingButton.IsEnabled =
            !interactionLocked &&
            RecordingsList.SelectedItem is not null;
    }

    private Task SetStatusAsync(
        string message) =>
        Dispatcher.InvokeAsync(
            () => RecordingStatusText.Text = message)
        .Task;

    private static string FormatDuration(
        long microseconds) =>
        TimeSpan.FromMilliseconds(
                microseconds / 1_000d)
            .ToString(
                @"m\:ss\.fff",
                CultureInfo.InvariantCulture);

    private sealed class ManualRecordingRow(
        ManualInputRecording recording)
    {
        public ManualInputRecording Recording { get; } =
            recording;

        public string Name => Recording.Name;

        public string Detail =>
            $"{FormatDuration(Recording.DurationMicroseconds)} · " +
            $"{Recording.Events.Count:N0} events";

        public string Created =>
            Recording.CreatedAtUtc.LocalDateTime
                .ToString(
                    "g",
                    CultureInfo.CurrentCulture);
    }
}

internal enum ManualRecordingsSnapshotState
{
    Ready,
    ArmedRecording,
    RunningRecording,
    ArmedPlayback,
    RunningPlayback,
}
