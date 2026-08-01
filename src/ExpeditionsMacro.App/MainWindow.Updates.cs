using System.Diagnostics;
using System.Windows;
using ExpeditionsMacro.Automation.Updates;

namespace ExpeditionsMacro.App;

public partial class MainWindow
{
    private string? _dismissedUpdateVersion;

    private void InitializeApplicationUpdates()
    {
        _services.ApplicationUpdates.StateChanged +=
            ApplicationUpdates_StateChanged;
        UpdateApplicationUpdateBanner();
    }

    private async Task StartApplicationUpdatesAsync()
    {
        try
        {
            await _services.ApplicationUpdates.InitializeAsync(
                _services.Settings.AutoCheckForUpdates);
        }
        catch (Exception error)
        {
            _services.Log.Warning(
                $"Application update initialization failed ({error.GetType().Name}).");
        }
    }

    private void DisposeApplicationUpdates()
    {
        _services.ApplicationUpdates.StateChanged -=
            ApplicationUpdates_StateChanged;
    }

    private void ApplicationUpdates_StateChanged(
        object? sender,
        EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateApplicationUpdateBanner);

    private void UpdateApplicationUpdateBanner()
    {
        ApplicationUpdateState state =
            _services.ApplicationUpdates.State;
        string? version = state.Version?.ToString();
        bool display =
            (state.Phase is
                ApplicationUpdatePhase.Available or
                ApplicationUpdatePhase.Downloading or
                ApplicationUpdatePhase.Ready) &&
            !string.Equals(
                version,
                _dismissedUpdateVersion,
                StringComparison.Ordinal);
        UpdateBanner.Visibility = display
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!display)
        {
            return;
        }

        UpdateBannerTitle.Text = state.Phase switch
        {
            ApplicationUpdatePhase.Ready =>
                $"Version {version} is ready to install",
            ApplicationUpdatePhase.Downloading =>
                $"Downloading version {version}",
            _ => $"Version {version} is available",
        };
        UpdateBannerDetail.Text = state.Message;
        UpdateBannerProgress.Visibility =
            state.Phase == ApplicationUpdatePhase.Downloading
                ? Visibility.Visible
                : Visibility.Collapsed;
        UpdateBannerProgress.Value = state.Progress ?? 0;
        UpdateReleaseButton.IsEnabled =
            state.ReleaseUri is not null;
        UpdatePrimaryButton.Visibility =
            state.Phase == ApplicationUpdatePhase.Downloading
                ? Visibility.Collapsed
                : Visibility.Visible;
        UpdateCancelButton.Visibility =
            state.Phase == ApplicationUpdatePhase.Downloading
                ? Visibility.Visible
                : Visibility.Collapsed;
        UpdatePrimaryButton.Content =
            state.Phase == ApplicationUpdatePhase.Ready
                ? "Open installer"
                : "Download";
        UpdatePrimaryButton.IsEnabled =
            state.Phase != ApplicationUpdatePhase.Ready ||
            !_services.Coordinator.IsBusy;
    }

    private async void UpdatePrimary_Click(
        object sender,
        RoutedEventArgs e) =>
        await PerformApplicationUpdateActionAsync();

    internal async Task PerformApplicationUpdateActionAsync()
    {
        ApplicationUpdateState state =
            _services.ApplicationUpdates.State;
        if (state.Phase == ApplicationUpdatePhase.Available)
        {
            await _services.ApplicationUpdates.DownloadAsync();
            return;
        }
        if (state.Phase != ApplicationUpdatePhase.Ready ||
            _services.Coordinator.IsBusy)
        {
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            "The verified Expeditions Macro installer will open. The installer may replace application files, but your plans, settings, recordings, logs, and diagnostics under your Windows profile remain in place. You can cancel in the installer without changing this version.\n\nOpen the installer and close Expeditions Macro?",
            "Install application update?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            string installer =
                await _services.ApplicationUpdates
                    .VerifyReadyInstallerAsync();
            Process.Start(new ProcessStartInfo(installer)
            {
                UseShellExecute = true,
            });
            Close();
        }
        catch (Exception error)
        {
            _services.Log.Warning(
                $"The application update installer could not be opened ({error.GetType().Name}).");
            string message =
                _services.ApplicationUpdates.State.Phase ==
                    ApplicationUpdatePhase.Error
                    ? _services.ApplicationUpdates.State.Message
                    : "The verified installer could not be opened. You can retry from Application updates in Settings.";
            MessageBox.Show(
                this,
                message,
                "Update installer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void UpdateCancel_Click(
        object sender,
        RoutedEventArgs e) =>
        _services.ApplicationUpdates.Cancel();

    private void UpdateDismiss_Click(
        object sender,
        RoutedEventArgs e)
    {
        _dismissedUpdateVersion =
            _services.ApplicationUpdates.State.Version?.ToString();
        UpdateApplicationUpdateBanner();
    }

    private void UpdateRelease_Click(
        object sender,
        RoutedEventArgs e)
    {
        Uri? releaseUri =
            _services.ApplicationUpdates.State.ReleaseUri;
        if (releaseUri is not null)
        {
            OpenExternalLink(
                releaseUri.AbsoluteUri,
                "the update release notes",
                "Application update");
        }
    }

    internal void SetUpdateAvailableForSnapshot(bool visible)
    {
        UpdateBanner.Visibility = visible
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!visible)
        {
            return;
        }
        UpdateBannerTitle.Text =
            "Version 1.3.0-beta.54 is available";
        UpdateBannerDetail.Text =
            "Download the verified installer when you are ready.";
        UpdateBannerProgress.Visibility = Visibility.Collapsed;
        UpdatePrimaryButton.Visibility = Visibility.Visible;
        UpdatePrimaryButton.Content = "Download";
        UpdatePrimaryButton.IsEnabled = true;
        UpdateCancelButton.Visibility = Visibility.Collapsed;
        UpdateReleaseButton.IsEnabled = true;
    }
}
