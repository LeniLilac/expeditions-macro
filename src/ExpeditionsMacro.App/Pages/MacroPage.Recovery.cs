using System.Windows;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private bool _loadingPrivateServerRecoverySettings;
    private Task _privateServerRecoverySettingsSave =
        Task.CompletedTask;
    private bool _testingPrivateServer;
    private bool _updatingPrivateServerRecoverySettings;

    private sealed record PrivateServerRecoverySelection(
        string Link,
        RobloxPrivateServerLaunchTarget? RecoveryTarget,
        RobloxPrivateServerLaunchTarget? StartupTarget);

    private async Task RunPlanWithFailureHandlingAsync(
        MacroPlan plan,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? areasMenuKey,
        char? unitMenuKey,
        char cancelPlacementKey,
        RobloxPrivateServerLaunchTarget? restartTarget,
        RobloxPrivateServerLaunchTarget? startupRestartTarget,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        bool captureHistory = _services.Settings.AutoCaptureOnMacroError;
        MacroRunTotals macroTotals = new();
        ChallengeRotationState challengeRotation =
            new(plan.ChallengeRotation);
        RefuelTaskStateSession refuelStates = new(plan);
        BountyOperationSession bountySession =
            new();
        TeamOperationSession teamSession = new();
        if (captureHistory) _services.DiagnosticCapture.BeginAutomaticHistory("Macro plan started");
        try
        {
            await _services.RecoveringScheduler.RunAsync(
                plan,
                restartTarget,
                (task, recordResult, recordCheckpoint, token) => ExecuteTaskAsync(
                    task,
                    recordResult,
                    recordCheckpoint,
                    webhook,
                    discordUserId,
                    playMenuKey,
                    areasMenuKey,
                    unitMenuKey,
                    cancelPlacementKey,
                    macroTotals,
                    challengeRotation,
                    refuelStates,
                    bountySession,
                    teamSession,
                    progress,
                    token),
                progress,
                changed => Dispatcher.BeginInvoke(() => ApplyPlanProgress(changed)),
                entry => DispatchLog(entry),
                cancellationToken,
                (error, token) => HandleRecoverableFailureAsync(
                    "Macro Plan",
                    webhook,
                    discordUserId,
                    error,
                    token),
                async token =>
                {
                    IDetectorPack startupDetector =
                        await LoadDetectorAsync(
                            AnimeExpeditionsDetectorSpec.PackId,
                            token).ConfigureAwait(false);
                    await _services.StartupPreflight.RunAsync(
                        startupDetector,
                        _services.Settings
                            .AutoCheckUiScaleOnStart,
                        _services.Settings
                            .AutoCheckGameSettingsOnStart,
                        progress,
                        entry => DispatchLog(entry),
                        token).ConfigureAwait(false);
                },
                startupRestartTarget).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PlayMenuBindingException error)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                PhaseText.Text =
                    "Toggle Play Menu key is required. Scroll down to Controls on the Dashboard.";
                AppendLog($"ERROR: {error.Message}");
            });
            throw;
        }
        catch (Exception error)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                PhaseText.Text = "Macro failed. Running configured error diagnostics.";
                AppendLog($"ERROR: {error.Message}");
            });
            MacroFailureHandlingResult result = await _services.HandleMacroFailureAsync(
                "Macro Plan",
                webhook,
                discordUserId,
                error).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => AppendFailureHandlingResult(result));
            throw;
        }
        finally
        {
            if (captureHistory) _services.DiagnosticCapture.EndAutomaticHistory();
        }
    }

    private void LoadPrivateServerRecoverySettings()
    {
        _loadingPrivateServerRecoverySettings = true;
        try
        {
            string link = string.Empty;
            try
            {
                link = _services.SecretProtector.Unprotect(
                    _services.Settings.EncryptedPrivateServerLink);
            }
            catch
            {
                PrivateServerStatusText.Text =
                    "The saved private-server link could not be read. Enter it again before enabling restart recovery.";
            }

            PrivateServerLinkPassword.Password = link;
            PrivateServerLinkVisible.Text = link;
            RestartRobloxCheck.IsChecked =
                _services.Settings.RestartRobloxWithPrivateServer;
            RestartRobloxAtStartCheck.IsChecked =
                _services.Settings.RestartRobloxAtMacroStart;
        }
        finally
        {
            _loadingPrivateServerRecoverySettings = false;
        }
    }

    private void ClearPrivateServerRecoverySnapshot()
    {
        _loadingPrivateServerRecoverySettings = true;
        try
        {
            PrivateServerLinkPassword.Password = string.Empty;
            PrivateServerLinkVisible.Text = string.Empty;
            RestartRobloxCheck.IsChecked = false;
            RestartRobloxAtStartCheck.IsChecked = false;
            PrivateServerStatusText.Text = string.Empty;
        }
        finally
        {
            _loadingPrivateServerRecoverySettings = false;
        }
    }

    private PrivateServerRecoverySelection ReadPrivateServerRecoverySelection()
    {
        string link = CurrentPrivateServerLink();
        RobloxPrivateServerLaunchTarget? target = null;
        if (link.Length > 0)
        {
            target = RobloxPrivateServerLaunchTarget.Parse(link);
        }
        bool restartForRecovery =
            RestartRobloxCheck.IsChecked == true;
        bool restartAtStart =
            RestartRobloxAtStartCheck.IsChecked == true;
        if (restartAtStart && target is null)
        {
            throw new InvalidOperationException(
                "Enter a valid Roblox private-server link before starting a Macro plan with startup restart enabled.");
        }
        if (restartForRecovery && target is null)
        {
            throw new InvalidOperationException(
                "Enter a valid Roblox private-server link before enabling restart recovery.");
        }

        PrivateServerStatusText.Text = target is null
            ? string.Empty
            : target.Kind == RobloxPrivateServerLinkKind.ShareCode
                ? "Modern Roblox share link recognized."
                : "Legacy Roblox private-server link recognized.";
        return new PrivateServerRecoverySelection(
            link,
            restartForRecovery ? target : null,
            restartAtStart ? target : null);
    }

    private Task SavePrivateServerRecoverySettingsAsync(
        PrivateServerRecoverySelection selection) =>
        _services.UpdateSettingsAsync(settings => settings with
        {
            EncryptedPrivateServerLink =
                _services.SecretProtector.Protect(selection.Link),
            RestartRobloxWithPrivateServer =
                selection.RecoveryTarget is not null,
            RestartRobloxAtMacroStart =
                selection.StartupTarget is not null,
        });

    private async void PrivateServerRestartSetting_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadingPrivateServerRecoverySettings ||
            _updatingPrivateServerRecoverySettings)
        {
            return;
        }

        bool restartForRecovery =
            RestartRobloxCheck.IsChecked == true;
        bool restartAtStart =
            RestartRobloxAtStartCheck.IsChecked == true;
        _privateServerRecoverySettingsSave =
            PersistPrivateServerRestartSettingsAsync(
                restartForRecovery,
                restartAtStart);
        await _privateServerRecoverySettingsSave;
    }

    private async Task
        PersistPrivateServerRestartSettingsAsync(
        bool restartForRecovery,
        bool restartAtStart)
    {
        _updatingPrivateServerRecoverySettings = true;
        SetPrivateServerRecoveryControlsEnabled(
            enabled: false);
        try
        {
            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    RestartRobloxWithPrivateServer =
                        restartForRecovery,
                    RestartRobloxAtMacroStart =
                        restartAtStart,
                });
            PrivateServerStatusText.Text =
                "Restart options saved.";
        }
        catch (Exception error)
        {
            LoadPrivateServerRecoverySettings();
            PrivateServerStatusText.Text =
                $"Could not save restart options: {error.Message}";
        }
        finally
        {
            _updatingPrivateServerRecoverySettings =
                false;
            SetPrivateServerRecoveryControlsEnabled(
                !_services.Coordinator.IsBusy);
        }
    }

    private Task
        FlushPrivateServerRecoverySettingsAsync() =>
        _privateServerRecoverySettingsSave;

    private void ShowPrivateServerLink_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (ShowPrivateServerLinkCheck.IsChecked == true)
        {
            PrivateServerLinkVisible.Text = PrivateServerLinkPassword.Password;
            PrivateServerLinkPassword.Visibility = Visibility.Collapsed;
            PrivateServerLinkVisible.Visibility = Visibility.Visible;
        }
        else
        {
            PrivateServerLinkPassword.Password = PrivateServerLinkVisible.Text;
            PrivateServerLinkVisible.Visibility = Visibility.Collapsed;
            PrivateServerLinkPassword.Visibility = Visibility.Visible;
        }
    }

    private void SetPrivateServerRecoveryControlsEnabled(bool enabled)
    {
        PrivateServerLinkPassword.IsEnabled = enabled;
        PrivateServerLinkVisible.IsEnabled = enabled;
        ShowPrivateServerLinkCheck.IsEnabled = enabled;
        RestartRobloxCheck.IsEnabled = enabled;
        RestartRobloxAtStartCheck.IsEnabled = enabled;
        TestPrivateServerButton.IsEnabled = enabled && !_testingPrivateServer;
    }

    private async void TestPrivateServer_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_testingPrivateServer || _services.Coordinator.IsBusy) return;
        try
        {
            RobloxPrivateServerLaunchTarget target =
                RobloxPrivateServerLaunchTarget.Parse(CurrentPrivateServerLink());
            _testingPrivateServer = true;
            TestPrivateServerButton.Content = "Opening...";
            SetPrivateServerRecoveryControlsEnabled(enabled: true);
            PrivateServerStatusText.Text =
                "Opening Roblox through its registered roblox:// protocol.";
            await _services.RobloxRecovery.LaunchAsync(target);
            await SavePrivateServerRecoverySettingsAsync(
                ReadPrivateServerRecoverySelection());
            PrivateServerStatusText.Text =
                "Roblox private-server launch sent through Windows.";
        }
        catch (Exception error)
        {
            PrivateServerStatusText.Text = error.Message;
        }
        finally
        {
            _testingPrivateServer = false;
            TestPrivateServerButton.Content = "Test link";
            SetPrivateServerRecoveryControlsEnabled(
                !_services.Coordinator.IsBusy);
        }
    }

    private string CurrentPrivateServerLink() =>
        ShowPrivateServerLinkCheck.IsChecked == true
            ? PrivateServerLinkVisible.Text.Trim()
            : PrivateServerLinkPassword.Password.Trim();
}
