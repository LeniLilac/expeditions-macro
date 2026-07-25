using System.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage
{
    private async void RunUiScale_Click(
        object sender,
        RoutedEventArgs e)
    {
        DeepDebugOperationContext context = new()
        {
            DebugTool = "ui-scale",
            DebugStepMode = SelectedStepMode().ToString(),
            OperationSettings = new
            {
                StartState = "Any fully loaded Roblox state with Settings closed",
                EndState = "Same game state with Settings closed",
                Scope = "UI Scale only",
                TargetScale = 1.00,
            },
        };
        await RunDebugOperationAsync(
            "Debug UI Scale",
            "ui-scale",
            context,
            async token =>
            {
                IDetectorPack detector =
                    await LoadDetectorAsync(token)
                        .ConfigureAwait(false);
                await _services.StartupPreflight
                    .RunUiScaleAsync(
                        detector,
                        CreateProgressReporter(),
                        RecordSettingsEvent,
                        token)
                    .ConfigureAwait(false);
            });
    }

    private async void RunGameSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        DeepDebugOperationContext context = new()
        {
            DebugTool = "game-settings",
            DebugStepMode = SelectedStepMode().ToString(),
            OperationSettings = new
            {
                StartState = "Fully loaded lobby with Settings closed",
                EndState = "Lobby with Settings closed",
                Scope = "Required toggles only",
                UiScaleInputAllowed = false,
            },
        };
        await RunDebugOperationAsync(
            "Debug game settings",
            "game-settings",
            context,
            async token =>
            {
                IDetectorPack detector =
                    await LoadDetectorAsync(token)
                        .ConfigureAwait(false);
                await _services.StartupPreflight
                    .RunGameSettingsAsync(
                        detector,
                        CreateProgressReporter(),
                        RecordSettingsEvent,
                        token)
                    .ConfigureAwait(false);
            });
    }

    private void RecordSettingsEvent(
        MacroEvent entry)
    {
        _services.DeepDebug.RecordEvent(
            "macro_event",
            "startup_settings",
            new
            {
                Level = entry.Level.ToString(),
                entry.Message,
                entry.State,
                entry.Confidence,
            });
        _services.Log.Info(entry.Message);
    }
}
