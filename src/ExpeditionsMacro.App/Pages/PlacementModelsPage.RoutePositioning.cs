using System.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void FastPosition_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy)
        {
            return;
        }

        PlacementTarget target =
            CurrentFastTarget();
        _services.Coordinator.Arm(
            "Prepare recording start",
            async token =>
            {
                Progress<MacroProgress> progress =
                    new(
                        value =>
                        {
                            _services.DeepDebug
                                .RecordProgress(
                                    value);
                            Dispatcher.BeginInvoke(
                                () =>
                                {
                                    FastStatusText.Text =
                                        value.Message;
                                    FastOperationProgress
                                        .Value =
                                        value.Percent;
                                });
                        });
                await _services.CameraPose
                    .PrepareWithoutYawAsync(
                        progress: progress,
                        cancellationToken: token)
                    .ConfigureAwait(false);
                RobloxWindow window =
                    _services.Automation.FindWindow() ??
                    throw new RobloxSessionUnavailableException(
                        "Roblox closed before route positioning.");
                await _services.RoutePositioning
                    .PositionAsync(
                        window,
                        target,
                        progress,
                        token)
                    .ConfigureAwait(false);
            },
            new DeepDebugOperationContext
            {
                OperationSettings = new
                {
                    Action =
                        "prepare_recording_start",
                    Target = target,
                },
            });
        FastStatusText.Text =
            $"Recording start preparation armed. Focus Roblox and press {_services.Hotkey.DisplayName}.";
        UpdateBusyState();
    }
}
