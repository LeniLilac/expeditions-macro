using System.IO;
using System.Windows;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class ExpeditionsPage
{
    public Task StartFromHotkeyAsync() =>
        StartMacroAsync();

    private async void Start_Click(
        object sender,
        RoutedEventArgs e) =>
        await StartMacroAsync();

    private void Stop_Click(
        object sender,
        RoutedEventArgs e) =>
        _services.Coordinator.Cancel();

    private async Task StartMacroAsync()
    {
        if (_services.Coordinator.IsBusy)
        {
            return;
        }
        ExpeditionPreset preset;
        char playMenuKey;
        char cancelPlacementKey;
        string webhook = CurrentWebhook();
        string discordUserId =
            DiscordErrorUserIdText.Text.Trim();
        try
        {
            playMenuKey =
                AppSettings.ParsePlayMenuKey(
                    _services.Settings.PlayMenuKey,
                    _services.Settings
                        .MacroHotkeyVirtualKey);
            cancelPlacementKey =
                AppSettings.ParseOptionalCancelPlacementKey(
                    _services.Settings
                        .CancelPlacementKey,
                    _services.Settings
                        .MacroHotkeyVirtualKey,
                    _services.Settings.PlayMenuKey,
                    _services.Settings.UnitMenuKey,
                    _services.Settings.AreasMenuKey,
                    _services.Settings
                        .ShiftLockVirtualKey) ?? default;
            if (!DiscordWebhookClient
                    .ValidateWebhookUrl(webhook))
            {
                throw new InvalidOperationException(
                    "Enter a valid Discord webhook URL, or leave it blank.");
            }
            if (!DiscordWebhookClient
                    .ValidateDiscordUserId(
                        discordUserId))
            {
                throw new InvalidOperationException(
                    "Enter a valid Discord user ID, or leave it blank.");
            }
            if (discordUserId.Length > 0 &&
                webhook.Length == 0)
            {
                throw new InvalidOperationException(
                    "A Discord webhook is required when an error-ping user ID is entered.");
            }
            preset = await SavePresetInternalAsync();
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
            AppendLog($"ERROR: {error.Message}");
            if (error is InvalidDataException &&
                (error.Message ==
                    AppSettings
                        .PlayMenuKeySetupInstructions ||
                 error.Message.StartsWith(
                     "The Toggle Play Menu key",
                     StringComparison.Ordinal)))
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    error.Message,
                    "Operation stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            return;
        }

        CameraModel? camera =
            preset.CameraPreparationMode ==
                CameraPreparationMode.CameraModel
                ? await _services.CameraModels
                    .LoadAsync(
                        preset.CameraModelId) ??
                    throw new InvalidOperationException(
                        "The selected camera model could not be loaded.")
                : null;
        PlacementModel placement =
            await _services.PlacementModels
                .LoadAsync(
                    preset.PlacementModelId) ??
            throw new InvalidOperationException(
                "The selected placement model could not be loaded.");
        IDetectorPack detector =
            _services.TraceDetector(
                await _services.DetectorPacks
                    .LoadAsync(
                        preset.DetectorPackId) ??
                throw new InvalidOperationException(
                    "The selected detector pack could not be loaded."));
        LogText.Clear();
        _runStarted = DateTimeOffset.Now;
        _macroOwned = true;
        _runtimeTimer.Start();
        MacroProgress.Value = 0;
        RepeatsText.Text =
            VictoriesText.Text =
                DefeatsText.Text =
                    RecoveriesText.Text = "0";
        AppendLog("Starting Expeditions macro.");
        IProgress<MacroProgress> progress =
            new InlineProgress<MacroProgress>(
                value =>
                {
                    _services.DeepDebug
                        .RecordProgress(value);
                    TrackActionState(
                        value.Phase,
                        value.Message);
                    Dispatcher.BeginInvoke(
                        () =>
                        {
                            PhaseText.Text =
                                value.Message;
                            MacroProgress.Value =
                                value.Percent;
                            if (value.DetectedState
                                is not null)
                            {
                                DetectionText.Text =
                                    $"Last detection: {Label(value.DetectedState)}{(value.Confidence is null ? string.Empty : $" ({value.Confidence:P0})")}";
                            }
                        });
                });
        _services.FastNoAlign.Invalidate();
        await _services.Coordinator.RunNowAsync(
            "Expeditions macro",
            token =>
                RunWithFailureHandlingAsync(
                    "Expeditions Macro",
                    webhook,
                    discordUserId,
                    () =>
                        _services.Expeditions.RunAsync(
                            preset,
                            camera,
                            placement,
                            detector,
                            webhook,
                            playMenuKey,
                            progress,
                            entry =>
                            {
                                _services.DeepDebug
                                    .RecordMacroEvent(
                                        entry);
                                TrackActionState(
                                    entry.State ??
                                        entry.Level
                                            .ToString(),
                                    entry.Message);
                                Dispatcher.BeginInvoke(
                                    () => AppendLog(
                                        entry.Level ==
                                            MacroEventLevel.Error
                                            ? $"ERROR: {entry.Message}"
                                            : entry.Message));
                            },
                            summary =>
                                Dispatcher.BeginInvoke(
                                    () => ApplySummary(
                                        summary)),
                            cancellationToken: token,
                            recoverableFailure:
                                (error, failureToken) =>
                                    HandleRecoverableFailureAsync(
                                        "Expeditions Macro",
                                        webhook,
                                        discordUserId,
                                        error,
                                        failureToken),
                            cancelPlacementKey:
                                cancelPlacementKey),
                    token),
            new DeepDebugOperationContext
            {
                ExpeditionPresetId = preset.Id,
            });
    }
}
