using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExpeditionsMacro.App.Controls;
using ExpeditionsMacro.App.Pages;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App;

public partial class MainWindow : Window
{
    private const string DiscordInviteUrl = "https://discord.gg/wE6XSVyXsN";
    private const string SetupGuideUrl = "https://docs.google.com/document/d/10NeDNa3BNEwPEpZj0oVQiR98_7GN67dmKS-OZwaxALM/edit?usp=sharing";
    private readonly AppServices _services;
    private readonly Dictionary<string, IAppPage> _pages;
    private readonly MacroPage _macroPage;
    private bool _autoMinimized;
    private bool _closingAfterStop;
    private bool _selectingSnapshotPage;
    private bool _handlingWindowState;
    private readonly bool _snapshotMode;
    private string _currentPageKey = "Dashboard";

    public MainWindow(AppServices services, bool snapshotMode = false)
    {
        _services = services;
        _snapshotMode = snapshotMode;
        InitializeComponent();
        InitializeNavigationRail();
        _macroPage = new MacroPage(services);
        _macroPage.SetNativeDockingEnabled(
            !snapshotMode);
        _pages = new Dictionary<string, IAppPage>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = _macroPage,
            ["Macro Plan"] = _macroPage,
            ["Placement Setup"] = new PlacementModelsPage(services),
            ["Recordings"] = new ManualRecordingsPage(services),
            ["Debug"] = new DebugPage(services),
            ["Settings"] = new SettingsPage(services),
        };
        _services.Coordinator.StateChanged += Coordinator_StateChanged;
        _services.Coordinator.OperationFailed += Coordinator_OperationFailed;
        _services.Hotkey.BindingChanged += Hotkey_BindingChanged;
        _services.SettingsChanged += Services_SettingsChanged;
        UpdateNavigationAvailability();
        UpdateProductFooter();
        if (!snapshotMode)
        {
            Loaded += async (_, _) =>
                await ShowPageAsync("Dashboard");
        }
        StateChanged += Window_StateChanged;
        SourceInitialized +=
            Window_SourceInitialized;
        Closed += Window_Closed;
    }

    private async void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (_selectingSnapshotPage || !IsLoaded || sender is not RadioButton button || button.Tag is not string key) return;
        await ShowPageAsync(key);
    }

    private async Task ShowPageAsync(string key)
    {
        bool dashboard =
            string.Equals(
                key,
                "Dashboard",
                StringComparison.OrdinalIgnoreCase);
        if (!_macroPage.TrySetDashboardActive(
            dashboard,
            out string pinningError))
        {
            RestoreNavigationSelection();
            MessageBox.Show(
                this,
                pinningError,
                "Roblox pinning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        IAppPage page = _pages[key];
        if (page is MacroPage macro)
        {
            macro.SelectWorkspace(key);
        }
        PageHost.Content = page;
        TitleContext.Text = key;
        _services.Coordinator.DefaultIdleHotkeyAction = page.IdleHotkeyAction;
        await page.OnShownAsync();
        _currentPageKey = key;
        EnsureWorkspaceSize(key);
    }

    private void RestoreNavigationSelection()
    {
        RadioButton navigation =
            _currentPageKey switch
            {
                "Dashboard" => DashboardNav,
                "Macro Plan" => MacroPlanNav,
                "Placement Setup" => PlacementNav,
                "Recordings" => RecordingsNav,
                "Debug" => DebugNav,
                "Settings" => SettingsNav,
                _ => DashboardNav,
            };
        _selectingSnapshotPage = true;
        try
        {
            navigation.IsChecked = true;
        }
        finally
        {
            _selectingSnapshotPage = false;
        }
    }

    internal async Task SelectPageForSnapshotAsync(
        string key,
        bool showPageEnd = false,
        bool showDebugUtilities = false,
        MacroPlanSnapshotState macroPlanState =
            MacroPlanSnapshotState.NestedLoops,
        ManualRecordingsSnapshotState
            recordingsState =
                ManualRecordingsSnapshotState.Ready)
    {
        RadioButton navigation = key switch
        {
            "Dashboard" => DashboardNav,
            "Macro Plan" => MacroPlanNav,
            "Placement Setup" => PlacementNav,
            "Recordings" => RecordingsNav,
            "Debug" => DebugNav,
            "Settings" => SettingsNav,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown snapshot page."),
        };

        _selectingSnapshotPage = true;
        try
        {
            navigation.IsChecked = true;
        }
        finally
        {
            _selectingSnapshotPage = false;
        }

        await ShowPageAsync(key);
        if (_pages[key] is PlacementModelsPage placement)
        {
            placement.SetSnapshotState(
                showPageEnd);
        }
        if (_pages[key] is ManualRecordingsPage recordings)
        {
            recordings.SetSnapshotState(
                recordingsState);
        }
        if (_pages[key] is MacroPage macro)
        {
            macro.SetSnapshotScroll(
                showPageEnd,
                macroPlanState);
        }
        if (_pages[key] is SettingsPage settings) settings.SetSnapshotScroll(showPageEnd);
        if (_pages[key] is DebugPage debug)
        {
            debug.SetSnapshotState();
            debug.SetSnapshotScroll(
                showPageEnd,
                showDebugUtilities);
        }
    }

    internal async Task VerifyBackgroundModelRefreshAsync()
    {
        // Setup operations complete through a diagnostic wrapper that deliberately
        // does not retain the WPF synchronization context. Exercise that exact
        // boundary in the repeatable UI snapshot check.
        await Task.Run(() => ((PlacementModelsPage)_pages["Placement Setup"]).RefreshModelsAsync());
    }

    private void Coordinator_StateChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            OperationLabel.Text = _services.Coordinator.Description;
            OperationDot.Fill = (Brush)FindResource(_services.Coordinator.State switch
            {
                OperationState.Armed => "WarningBrush",
                OperationState.Running => "SuccessBrush",
                OperationState.Stopping => "WarningBrush",
                _ => "FaintBrush",
            });
            if (_services.Coordinator.State ==
                    OperationState.Running &&
                _services.Settings
                    .MinimizeDuringAutomation &&
                !_macroPage
                    .KeepsDashboardWindowVisible &&
                WindowState !=
                    WindowState.Minimized)
            {
                _autoMinimized = true;
                WindowState = WindowState.Minimized;
            }
            else if (_services.Coordinator.State == OperationState.Idle && _autoMinimized)
            {
                _autoMinimized = false;
                WindowState = WindowState.Normal;
                Activate();
            }
            if (_closingAfterStop && _services.Coordinator.State == OperationState.Idle) Close();
        });
    }

    private void Window_StateChanged(
        object? sender,
        EventArgs e)
    {
        Lucide.SetIcon(
            MaximizeButton,
            WindowState ==
                WindowState.Maximized
                ? LucideIconKind.Copy
                : LucideIconKind.Square);
        if (_snapshotMode ||
            _handlingWindowState)
        {
            return;
        }

        bool ownerVisible =
            WindowState !=
                WindowState.Minimized;
        if (_macroPage
            .TrySetDashboardOwnerVisible(
                ownerVisible,
                out string error))
        {
            return;
        }

        _handlingWindowState = true;
        try
        {
            WindowState =
                WindowState.Normal;
            Activate();
            MessageBox.Show(
                this,
                error,
                "Roblox pinning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _handlingWindowState = false;
        }
    }

    private void Coordinator_OperationFailed(object? sender, Exception error)
    {
        _services.Log.Error("Automation operation stopped.", error);
        Dispatcher.BeginInvoke(() =>
        {
            bool suspended =
                _macroPage
                    .TrySuspendRobloxForOwnedDialog(
                        out string pinError);
            if (!suspended)
            {
                _services.Log.Warning(
                    $"Roblox could not be suspended before showing the operation error: {pinError}");
            }
            try
            {
                Activate();
                MessageBox.Show(
                    this,
                    error.Message,
                    "Operation stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (suspended)
                {
                    _macroPage
                        .ResumeRobloxAfterOwnedDialog();
                }
            }
        });
    }

    private void Hotkey_BindingChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(UpdateProductFooter);

    private void Services_SettingsChanged(
        object? sender,
        EventArgs e) =>
        Dispatcher.BeginInvoke(UpdateNavigationAvailability);

    private void UpdateNavigationAvailability()
    {
        bool debugVisible =
            _snapshotMode ||
            _services.Settings.DebugModeEnabled;
        DebugNav.Visibility = debugVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!debugVisible &&
            ReferenceEquals(
                PageHost.Content,
                _pages["Debug"]))
        {
            DashboardNav.IsChecked = true;
        }

        bool recordingsVisible =
            _snapshotMode ||
            _services.Settings
                .ManualInputRecordingEnabled;
        RecordingsNav.Visibility =
            recordingsVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        if (!recordingsVisible &&
            ReferenceEquals(
                PageHost.Content,
                _pages["Recordings"]))
        {
            PlacementNav.IsChecked = true;
        }

        SetNavigationRailCollapsed(
            _navigationRailCollapsed);
    }

    private void UpdateProductFooter()
    {
        HotkeyHint.Text = $"{_services.Hotkey.DisplayName} start / stop";
        string version = ProductVersion.Current;
        VersionLabel.Text = $"Version {version}";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_macroPage.TryDetachRoblox(
            out string pinningError))
        {
            e.Cancel = true;
            MessageBox.Show(
                this,
                pinningError,
                "Roblox pinning",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }
        if (_services.Coordinator.State == OperationState.Idle) return;
        e.Cancel = true;
        _closingAfterStop = true;
        _services.Coordinator.Cancel();
    }

    private void TitleBar_RightClick(object sender, MouseButtonEventArgs e) => SystemCommands.ShowSystemMenu(this, PointToScreen(e.GetPosition(this)));

    private void Minimize_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_macroPage
            .TrySetDashboardOwnerVisible(
                visible: false,
                out string error))
        {
            MessageBox.Show(
                this,
                error,
                "Roblox pinning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        WindowState =
            WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void SetupGuide_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(SetupGuideUrl, "the setup guide", "Setup guide");

    private void JoinDiscord_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(DiscordInviteUrl, "the Discord invite", "Join Discord");

    private void OpenExternalLink(string url, string description, string title)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            _services.Log.Error($"Could not open {description}.", error);
            MessageBox.Show(this, $"Could not open {description}.\n\n{url}", title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
