using System.Windows;
using ExpeditionsMacro.Automation.Updates;

namespace ExpeditionsMacro.App.Pages;

public partial class SettingsPage
{
    private bool _applicationUpdateSubscribed;

    private void InitializeApplicationUpdateControls()
    {
        Loaded += ApplicationUpdates_Loaded;
        Unloaded += ApplicationUpdates_Unloaded;
        UpdateApplicationUpdateState();
    }

    private void ApplicationUpdates_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_applicationUpdateSubscribed)
        {
            return;
        }
        _services.ApplicationUpdates.StateChanged +=
            ApplicationUpdates_StateChanged;
        _applicationUpdateSubscribed = true;
        UpdateApplicationUpdateState();
    }

    private void ApplicationUpdates_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (!_applicationUpdateSubscribed)
        {
            return;
        }
        _services.ApplicationUpdates.StateChanged -=
            ApplicationUpdates_StateChanged;
        _applicationUpdateSubscribed = false;
    }

    private void ApplicationUpdates_StateChanged(
        object? sender,
        EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateApplicationUpdateState);

    private async void AutoCheckForUpdatesCheck_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        await _services.UpdateSettingsAsync(settings =>
            settings with
            {
                AutoCheckForUpdates =
                    AutoCheckForUpdatesCheck.IsChecked == true,
            });
    }

    private async void CheckForUpdates_Click(
        object sender,
        RoutedEventArgs e)
    {
        ApplicationUpdatePhase phase =
            _services.ApplicationUpdates.State.Phase;
        if (phase is ApplicationUpdatePhase.Checking or
            ApplicationUpdatePhase.Downloading)
        {
            _services.ApplicationUpdates.Cancel();
            return;
        }
        await _services.ApplicationUpdates.CheckAsync();
    }

    private async void ApplicationUpdateAction_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow window)
        {
            await window.PerformApplicationUpdateActionAsync();
        }
    }

    private void UpdateApplicationUpdateState()
    {
        ApplicationUpdateState state =
            _services.ApplicationUpdates.State;
        UpdateChannelText.Text =
            _services.ApplicationUpdates.ChannelDescription;
        ApplicationUpdateStatusText.Text = state.Message;
        bool active = state.Phase is
            ApplicationUpdatePhase.Checking or
            ApplicationUpdatePhase.Downloading;
        CheckForUpdatesButton.Content = active
            ? "Cancel"
            : "Check now";
        CheckForUpdatesButton.IsEnabled =
            state.Phase != ApplicationUpdatePhase.Ready;

        bool actionVisible = state.Phase is
            ApplicationUpdatePhase.Available or
            ApplicationUpdatePhase.Ready;
        ApplicationUpdateActionButton.Visibility =
            actionVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        ApplicationUpdateActionButton.Content =
            state.Phase == ApplicationUpdatePhase.Ready
                ? "Open installer"
                : "Download";
        ApplicationUpdateActionButton.IsEnabled =
            state.Phase != ApplicationUpdatePhase.Ready ||
            !_services.Coordinator.IsBusy;
    }
}
