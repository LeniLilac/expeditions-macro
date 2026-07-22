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
    private bool _autoMinimized;
    private bool _closingAfterStop;
    private bool _selectingSnapshotPage;

    public MainWindow(AppServices services, bool snapshotMode = false)
    {
        _services = services;
        InitializeComponent();
        _pages = new Dictionary<string, IAppPage>(StringComparer.OrdinalIgnoreCase)
        {
            ["Macro"] = new MacroPage(services),
            ["Expeditions"] = new ExpeditionsPage(services),
            ["Challenges"] = new ChallengesPage(services),
            ["Story"] = new StoryPage(services),
            ["Raid"] = new RaidPage(services),
            ["Camera Models"] = new CameraModelsPage(services),
            ["Placement Models"] = new PlacementModelsPage(services),
            ["Settings"] = new SettingsPage(services),
        };
        _services.Coordinator.StateChanged += Coordinator_StateChanged;
        _services.Coordinator.OperationFailed += Coordinator_OperationFailed;
        _services.Hotkey.BindingChanged += Hotkey_BindingChanged;
        UpdateProductFooter();
        if (!snapshotMode)
        {
            Loaded += async (_, _) =>
            {
                await ShowPageAsync("Macro");
                if (_pages["Settings"] is SettingsPage settings) await settings.CheckForUpdatesAsync(automatic: true);
            };
        }
        StateChanged += (_, _) => Lucide.SetIcon(MaximizeButton, WindowState == WindowState.Maximized ? LucideIconKind.Copy : LucideIconKind.Square);
    }

    private async void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (_selectingSnapshotPage || !IsLoaded || sender is not RadioButton button || button.Tag is not string key) return;
        await ShowPageAsync(key);
    }

    private async Task ShowPageAsync(string key)
    {
        IAppPage page = _pages[key];
        PageHost.Content = page;
        TitleContext.Text = key;
        _services.Coordinator.DefaultIdleHotkeyAction = page.IdleHotkeyAction;
        await page.OnShownAsync();
    }

    internal async Task SelectPageForSnapshotAsync(string key, bool showPageEnd = false)
    {
        RadioButton navigation = key switch
        {
            "Macro" => MacroNav,
            "Expeditions" => ExpeditionsNav,
            "Challenges" => ChallengesNav,
            "Story" => StoryNav,
            "Raid" => RaidNav,
            "Camera Models" => CameraNav,
            "Placement Models" => PlacementNav,
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
        if (_pages[key] is MacroPage macro) macro.SetSnapshotScroll(showPageEnd);
        if (_pages[key] is SettingsPage settings) settings.SetSnapshotScroll(showPageEnd);
        if (_pages[key] is ChallengesPage challenges) challenges.SetSnapshotScroll(showPageEnd);
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
            if (_services.Coordinator.State == OperationState.Running && _services.Settings.MinimizeDuringAutomation && WindowState != WindowState.Minimized)
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

    private void Coordinator_OperationFailed(object? sender, Exception error)
    {
        _services.Log.Error("Automation operation stopped.", error);
        Dispatcher.BeginInvoke(() => MessageBox.Show(this, error.Message, "Operation stopped", MessageBoxButton.OK, MessageBoxImage.Error));
    }

    private void Hotkey_BindingChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(UpdateProductFooter);

    private void UpdateProductFooter()
    {
        HotkeyHint.Text = $"{_services.Hotkey.DisplayName} start / stop";
        string version = ProductVersion.Current;
        VersionLabel.Text = $"Version {version}";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_services.Coordinator.State == OperationState.Idle) return;
        e.Cancel = true;
        _closingAfterStop = true;
        _services.Coordinator.Cancel();
    }

    private void TitleBar_RightClick(object sender, MouseButtonEventArgs e) => SystemCommands.ShowSystemMenu(this, PointToScreen(e.GetPosition(this)));

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

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
