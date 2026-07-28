using System.Windows;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
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

        MacroPlan plan;
        char playMenuKey;
        char? unitMenuKey = null;
        char cancelPlacementKey = default;
        string webhook = CurrentWebhook();
        string discordUserId =
            DiscordUserIdText.Text.Trim();
        PrivateServerRecoverySelection
            privateServerRecovery;
        try
        {
            plan = await SavePlanInternalAsync();
            if (await PlanRequiresQuickPlacementKeyAsync(
                    plan,
                    CancellationToken.None))
            {
                _ = AppSettings.ParseQuickPlacementKey(
                    _services.Settings);
            }
            playMenuKey =
                AppSettings.ParsePlayMenuKey(
                    _services.Settings.PlayMenuKey,
                    _services.Settings
                        .MacroHotkeyVirtualKey);
            if (!string.IsNullOrWhiteSpace(
                    _services.Settings.UnitMenuKey))
            {
                unitMenuKey =
                    AppSettings.ParseUnitMenuKey(
                        _services.Settings.UnitMenuKey,
                        _services.Settings
                            .MacroHotkeyVirtualKey,
                        _services.Settings.PlayMenuKey);
            }
            ValidateDiscord(
                webhook,
                discordUserId);
            privateServerRecovery =
                ReadPrivateServerRecoverySelection();
            await SaveReportingSettingsAsync(
                webhook,
                discordUserId);
            await SavePrivateServerRecoverySettingsAsync(
                privateServerRecovery);
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
            AppendLog($"ERROR: {error.Message}");
            return;
        }

        LogText.Clear();
        _runStarted = DateTimeOffset.Now;
        _macroOwned = true;
        _runtimeTimer.Start();
        MacroProgress.Value = 0;
        VictoriesText.Text = "0";
        DefeatsText.Text = "0";
        AppendLog(
            $"Starting macro plan '{plan.Name}'.");

        IProgress<MacroProgress> progress =
            new InlineProgress<MacroProgress>(
                value =>
                {
                    _services.DeepDebug.RecordProgress(
                        value);
                    _services.DiagnosticCapture
                        .RecordActionState(
                            $"{value.Phase}: {value.Message}");
                    Dispatcher.BeginInvoke(
                        () =>
                        {
                            PhaseText.Text =
                                value.Message;
                            MacroProgress.Value =
                                Math.Clamp(
                                    value.Percent,
                                    0,
                                    100);
                        });
                });
        _services.FastNoAlign.Invalidate();
        await _services.Coordinator.RunNowAsync(
            "Macro plan",
            token =>
                RunPlanWithFailureHandlingAsync(
                    plan,
                    webhook,
                    discordUserId,
                    playMenuKey,
                    unitMenuKey,
                    cancelPlacementKey,
                    privateServerRecovery.RecoveryTarget,
                    privateServerRecovery.StartupTarget,
                    progress,
                    token),
            new DeepDebugOperationContext
            {
                MacroPlanId = plan.Id,
            });
    }
}
