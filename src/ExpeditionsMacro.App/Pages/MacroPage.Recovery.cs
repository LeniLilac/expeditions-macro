using System.Windows;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private sealed record PrivateServerRecoverySelection(
        string Link,
        RobloxPrivateServerLaunchTarget? Target);

    private async Task RunPlanWithFailureHandlingAsync(
        MacroPlan plan,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        RobloxPrivateServerLaunchTarget? restartTarget,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        bool captureHistory = _services.Settings.AutoCaptureOnMacroError;
        if (captureHistory) _services.DiagnosticCapture.BeginAutomaticHistory("Macro plan started");
        try
        {
            await _services.RecoveringScheduler.RunAsync(
                plan,
                restartTarget,
                (task, recordResult, token) => ExecuteTaskAsync(
                    task,
                    recordResult,
                    webhook,
                    discordUserId,
                    playMenuKey,
                    unitMenuKey,
                    progress,
                    token),
                progress,
                changed => Dispatcher.BeginInvoke(() => ApplyPlanProgress(changed)),
                entry => DispatchLog(entry),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PlayMenuBindingException error)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                PhaseText.Text = "Play menu key setup is required.";
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
    }

    private void ClearPrivateServerRecoverySnapshot()
    {
        PrivateServerLinkPassword.Password = string.Empty;
        PrivateServerLinkVisible.Text = string.Empty;
        RestartRobloxCheck.IsChecked = false;
        PrivateServerStatusText.Text = string.Empty;
    }

    private PrivateServerRecoverySelection ReadPrivateServerRecoverySelection()
    {
        string link = CurrentPrivateServerLink();
        RobloxPrivateServerLaunchTarget? target = null;
        if (link.Length > 0)
        {
            target = RobloxPrivateServerLaunchTarget.Parse(link);
        }
        if (RestartRobloxCheck.IsChecked == true && target is null)
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
            RestartRobloxCheck.IsChecked == true ? target : null);
    }

    private Task SavePrivateServerRecoverySettingsAsync(
        PrivateServerRecoverySelection selection) =>
        _services.UpdateSettingsAsync(settings => settings with
        {
            EncryptedPrivateServerLink =
                _services.SecretProtector.Protect(selection.Link),
            RestartRobloxWithPrivateServer = selection.Target is not null,
        });

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
    }

    private string CurrentPrivateServerLink() =>
        ShowPrivateServerLinkCheck.IsChecked == true
            ? PrivateServerLinkVisible.Text.Trim()
            : PrivateServerLinkPassword.Password.Trim();
}
