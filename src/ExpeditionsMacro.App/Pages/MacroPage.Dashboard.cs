using System.Windows;
using System.Windows.Controls;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private const double CompactDashboardBreakpoint = 1235;
    private bool _loadingDashboardSettings;
    private bool _updatingDashboardPreparation;
    private bool _compactDashboardLayout;
    private string _activeWorkspace = "Dashboard";

    internal void SelectWorkspace(string key)
    {
        bool dashboard = string.Equals(
            key,
            "Dashboard",
            StringComparison.OrdinalIgnoreCase);
        if (dashboard &&
            TaskEditorOverlay.Visibility ==
                Visibility.Visible)
        {
            ResetTaskEditor();
        }
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
        RobloxPreview.TryPrepareForClose(
            out error);

    internal bool TrySuspendRobloxForOwnedDialog(
        out string error) =>
        RobloxPreview.TrySuspendForOwnedDialog(
            out error);

    internal void ResumeRobloxAfterOwnedDialog() =>
        RobloxPreview.ResumeAfterOwnedDialog();

    private void InitializeDashboard()
    {
        RobloxPreview.Initialize(_services);
        DashboardKeyBindingsPanel.Initialize(_services);
        _services.SettingsChanged += (_, _) =>
            Dispatcher.BeginInvoke(
                RefreshDashboardSettings);
        RefreshDashboardSettings();
        ApplyDashboardLayout(
            DashboardScroll.ActualWidth <
                CompactDashboardBreakpoint);
    }

    private void DashboardScroll_SizeChanged(
        object sender,
        SizeChangedEventArgs e) =>
        ApplyDashboardLayout(
            e.NewSize.Width <
                CompactDashboardBreakpoint);

    private void ApplyDashboardLayout(
        bool compact)
    {
        if (DashboardContentGrid is null ||
            _compactDashboardLayout == compact)
        {
            return;
        }
        _compactDashboardLayout = compact;

        DashboardContentGrid.Margin = compact
            ? new Thickness(16, 20, 18, 24)
            : new Thickness(34, 27, 40, 30);
        RobloxLiveViewCard.Padding = compact
            ? new Thickness(16)
            : new Thickness(24);

        DashboardLiveViewColumn.Width = compact
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;
        DashboardOverviewColumnGap.Width = compact
            ? new GridLength(0)
            : new GridLength(16);
        DashboardCurrentRunColumn.MinWidth = compact
            ? 0
            : 274;
        DashboardCurrentRunColumn.Width = compact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        DashboardOverviewRowGap.Height = compact
            ? new GridLength(14)
            : new GridLength(0);
        DashboardCurrentRunRow.Height = compact
            ? GridLength.Auto
            : new GridLength(0);
        Grid.SetRow(
            CurrentRunCard,
            compact ? 2 : 0);
        Grid.SetColumn(
            CurrentRunCard,
            compact ? 0 : 2);

        DashboardDiscordColumnDivider.Width = compact
            ? new GridLength(0)
            : new GridLength(1);
        DashboardFailureAlertColumn.Width = compact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        DashboardDiscordRowGap.Height = compact
            ? new GridLength(17)
            : new GridLength(0);
        DashboardFailureAlertRow.Height = compact
            ? GridLength.Auto
            : new GridLength(0);
        Grid.SetRow(
            DashboardDiscordDivider,
            compact ? 1 : 0);
        Grid.SetColumn(
            DashboardDiscordDivider,
            compact ? 0 : 1);
        DashboardDiscordDivider.Height = compact
            ? 1
            : double.NaN;
        DashboardDiscordDivider.Margin = compact
            ? new Thickness(0, 8, 0, 8)
            : new Thickness(0);
        Grid.SetRow(
            DashboardFailureAlertPanel,
            compact ? 2 : 0);
        Grid.SetColumn(
            DashboardFailureAlertPanel,
            compact ? 0 : 2);
        DashboardWebhookPanel.Margin = compact
            ? new Thickness(0)
            : new Thickness(0, 0, 24, 0);
        DashboardFailureAlertPanel.Margin = compact
            ? new Thickness(0)
            : new Thickness(24, 0, 0, 0);
    }

    private void RefreshDashboardSettings()
    {
        _loadingDashboardSettings = true;
        try
        {
            DashboardAutoUiScaleCheck.IsChecked =
                _services.Settings
                    .AutoCheckUiScaleOnStart;
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

    private async void DashboardAutoUiScaleCheck_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadingDashboardSettings ||
            _updatingDashboardPreparation)
        {
            return;
        }

        _updatingDashboardPreparation = true;
        UpdateDashboardBusyState();
        try
        {
            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    AutoCheckUiScaleOnStart =
                        DashboardAutoUiScaleCheck
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
            _updatingDashboardPreparation = false;
            UpdateDashboardBusyState();
        }
    }

    private async void DashboardAutoGameSettingsCheck_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadingDashboardSettings ||
            _updatingDashboardPreparation)
        {
            return;
        }

        _updatingDashboardPreparation = true;
        UpdateDashboardBusyState();
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
            _updatingDashboardPreparation = false;
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
        bool preparationEnabled =
            !busy &&
            !_updatingDashboardPreparation;
        DashboardAutoUiScaleCheck.IsEnabled =
            preparationEnabled;
        DashboardAutoGameSettingsCheck.IsEnabled =
            preparationEnabled;
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

    internal void ShowCurrentRunForSnapshot() =>
        CurrentRunCard.BringIntoView();

    internal void ShowBoundedRunLogForSnapshot()
    {
        LogText.Clear();
        UpdateLayout();
        double emptyCardHeight =
            CurrentRunCard.ActualHeight;
        LogText.Text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 80)
                .Select(index =>
                    $"{index:00}:00:00  Snapshot run-log entry {index:00}: placement and detector work completed."));
        LogText.CaretIndex = LogText.Text.Length;
        LogText.ScrollToEnd();
        UpdateLayout();

        if (Math.Abs(
                CurrentRunCard.ActualHeight -
                emptyCardHeight) > 0.5 ||
            Math.Abs(
                RunLogViewport.ActualHeight -
                RunLogViewport.Height) > 0.5)
        {
            throw new InvalidOperationException(
                "Dashboard run-log content changed the bounded Current run layout.");
        }
    }
}
