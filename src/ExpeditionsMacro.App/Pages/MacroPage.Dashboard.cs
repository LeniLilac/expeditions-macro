using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private bool _loadingDashboardSettings;
    private string _activeWorkspace = "Dashboard";

    internal void SelectWorkspace(string key)
    {
        bool dashboard = string.Equals(
            key,
            "Dashboard",
            StringComparison.OrdinalIgnoreCase);
        _activeWorkspace = dashboard
            ? "Dashboard"
            : "Macro Plan";
        DashboardScroll.Visibility = dashboard
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlanScroll.Visibility = dashboard
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    internal void SetNativeDockingEnabled(
        bool enabled) =>
        RobloxPreview.SetNativeDockingEnabled(
            enabled);

    internal bool KeepsDashboardWindowVisible =>
        RobloxPreview.RequiresVisibleOwner;

    internal bool TrySetDashboardActive(
        bool active,
        out string error)
    {
        if (!RobloxPreview.SetDashboardActive(
            active,
            out error))
        {
            return false;
        }
        if (active)
        {
            return RobloxPreview.SetPinned(
                PinRobloxCheck.IsChecked == true,
                out error);
        }
        return true;
    }

    internal bool TrySetDashboardOwnerVisible(
        bool visible,
        out string error) =>
        RobloxPreview.SetOwnerVisible(
            visible,
            out error);

    internal bool TryDetachRoblox(
        out string error) =>
        RobloxPreview.TryDetach(out error);

    private void InitializeDashboard()
    {
        RobloxPreview.Initialize(_services);
        DashboardKeyBindingsPanel.Initialize(_services);
        _services.SettingsChanged += (_, _) =>
            Dispatcher.BeginInvoke(
                RefreshDashboardSettings);
        RefreshDashboardSettings();
    }

    private void RefreshDashboardSettings()
    {
        _loadingDashboardSettings = true;
        try
        {
            DashboardAutoGameSettingsCheck.IsChecked =
                _services.Settings
                    .AutoCheckGameSettingsOnStart;
            DashboardKeyBindingsPanel.Refresh();
        }
        finally
        {
            _loadingDashboardSettings = false;
        }
    }

    private async void DashboardAutoGameSettingsCheck_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadingDashboardSettings)
        {
            return;
        }

        DashboardAutoGameSettingsCheck.IsEnabled =
            false;
        try
        {
            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    AutoCheckGameSettingsOnStart =
                        DashboardAutoGameSettingsCheck
                            .IsChecked == true,
                });
        }
        catch
        {
            RefreshDashboardSettings();
            throw;
        }
        finally
        {
            UpdateDashboardBusyState();
        }
    }

    private void PinRoblox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (RobloxPreview is null)
        {
            return;
        }
        bool requested =
            PinRobloxCheck.IsChecked == true;
        if (RobloxPreview.SetPinned(
            requested,
            out string error))
        {
            return;
        }

        PinRobloxCheck.IsChecked = true;
        MessageBox.Show(
            Window.GetWindow(this),
            error,
            "Roblox pinning",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void UpdateDashboardBusyState()
    {
        bool busy = _services.Coordinator.IsBusy;
        DashboardAutoGameSettingsCheck.IsEnabled =
            !busy;
        DashboardKeyBindingsPanel.UpdateBusyState(
            busy);
    }

    private void SetActiveWorkspaceSnapshotScroll(
        bool showEnd)
    {
        ScrollViewer scroll =
            string.Equals(
                _activeWorkspace,
                "Dashboard",
                StringComparison.OrdinalIgnoreCase)
                ? DashboardScroll
                : PlanScroll;
        if (showEnd)
        {
            scroll.ScrollToEnd();
        }
        else
        {
            scroll.ScrollToTop();
        }
    }
}
