using System.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private async void Test_Click(
        object sender,
        RoutedEventArgs e)
    {
        PlacementModel model;
        char cancelPlacementKey;
        try
        {
            model = BuildModel();
            cancelPlacementKey =
                PlacementControlRequirements
                    .ValidateStepModeBindingsForPlayback(
                    model,
                    _services.Settings);
            _placementAutoSave.ScheduleSave(
                model);
            if (!await FlushPlacementAutoSaveAsync())
            {
                return;
            }
            _selectedModel = model;
        }
        catch (Exception error)
        {
            SetStatus(error.Message);
            return;
        }

        int delay =
            _fastPlacementIntervalMilliseconds;
        _services.Coordinator.Arm(
            "Placement playback",
            token => TestPlacementAsync(
                model,
                delay,
                cancelPlacementKey,
                token),
            new DeepDebugOperationContext
            {
                PlacementModelIds = [model.Id],
                OperationSettings = new
                {
                    Model = model.Id,
                    UseDefaultInterval = true,
                    DefaultDelayMilliseconds =
                        delay,
                    FastNoAlign = true,
                    model.ManualInputRecordingId,
                },
            });
        SetStatus(
            $"Playback armed. Focus Roblox and press {_services.Hotkey.DisplayName} to begin.");
        UpdateBusyState();
    }

    private async Task TestPlacementAsync(
        PlacementModel model,
        int delay,
        char cancelPlacementKey,
        CancellationToken token)
    {
        Progress<MacroProgress> progress =
            new(
                value => Dispatcher.BeginInvoke(
                    () => FastStatusText.Text =
                        value.Message));
        await _services.CameraPose
            .PrepareWithoutYawAsync(
                progress: progress,
                cancellationToken: token)
            .ConfigureAwait(false);
        if (ManualInputRouteService.IsConfigured(
                model))
        {
            RobloxWindow window =
                _services.Automation.FindWindow() ??
                throw new RobloxSessionUnavailableException(
                    "Open Roblox before testing the manual recording.");
            if (model.Target is { } target &&
                PlacementRoutePositioningService
                    .IsAvailable(target))
            {
                await _services.RoutePositioning
                    .PositionAsync(
                        window,
                        target,
                        progress,
                        token)
                    .ConfigureAwait(false);
            }
            await _services.ManualRoutes.PlayAsync(
                    window,
                    model,
                    progress,
                    token)
                .ConfigureAwait(false);
            return;
        }

        await _services.Placement.PlayAsync(
            model,
            useDefaultInterval: true,
            delay,
            cancelPlacementKey:
                cancelPlacementKey,
            stepSent: (index, total, step) =>
            {
                _services.DeepDebug.RecordEvent(
                    "placement",
                    "playback_step",
                    new
                    {
                        Index = index,
                        Total = total,
                        Step = step,
                    });
                Dispatcher.BeginInvoke(
                    () => SetOperationProgress(
                        100d * index / total));
            },
            status: message =>
            {
                _services.DeepDebug.RecordEvent(
                    "placement",
                    "playback_status",
                    new { Message = message });
                Dispatcher.BeginInvoke(
                    () => SetStatus(message));
            },
            cancellationToken: token)
            .ConfigureAwait(false);
    }
}
