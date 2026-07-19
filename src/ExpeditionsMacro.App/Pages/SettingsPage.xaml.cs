using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Updates;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class SettingsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private bool _loading;
    private bool _captureOperationActive;

    public SettingsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ThemeCombo.ItemsSource = Enum.GetValues<AppTheme>();
        DataPath.Text = services.Paths.Root;
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateCaptureState);
    }

    public Func<Task>? IdleF6Action => null;

    internal void SetSnapshotScroll(bool showDebug)
    {
        SettingsScroll.UpdateLayout();
        if (showDebug) SettingsScroll.ScrollToEnd();
        else SettingsScroll.ScrollToTop();
    }

    public async Task OnShownAsync()
    {
        _loading = true;
        ThemeCombo.SelectedItem = _services.Settings.Theme;
        MinimizeCheck.IsChecked = _services.Settings.MinimizeDuringAutomation;
        AutoUpdateCheck.IsChecked = _services.Settings.CheckDetectorUpdates;
        _loading = false;
        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        RobloxText.Text = _services.Automation.FindWindow() is { } window ? $"Found: {window.Title}" : "Not found";
        HotkeyText.Text = _services.Hotkey.IsRegistered ? "F6 registered" : "Unavailable";
        await RefreshDetectorAsync();
        UpdateCaptureState();
    }

    private async void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not AppTheme theme) return;
        ThemeService.Apply(theme);
        await _services.UpdateSettingsAsync(settings => settings with { Theme = theme });
    }

    private async void MinimizeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        await _services.UpdateSettingsAsync(settings => settings with { MinimizeDuringAutomation = MinimizeCheck.IsChecked == true });
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Root);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Logs);

    private void OpenCaptures_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Diagnostics);

    private void CaptureArm_Click(object sender, RoutedEventArgs e)
    {
        string name = CaptureNameText.Text.Trim();
        if (!double.TryParse(CaptureIntervalText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
        {
            CaptureStatusText.Text = "Interval must be a number of seconds.";
            return;
        }

        try
        {
            Progress<DiagnosticCaptureProgress> progress = new(value => CaptureStatusText.Text = value.Message);
            _captureOperationActive = true;
            _services.Coordinator.Arm("Diagnostic capture", async token =>
            {
                try
                {
                    DiagnosticCaptureResult result = await _services.DiagnosticCapture.CaptureAsync(name, TimeSpan.FromSeconds(seconds), progress, token);
                    await Dispatcher.InvokeAsync(() => CaptureStatusText.Text = result.RestoreWarning is null
                        ? $"Saved {result.Captures} screenshot(s) to {Path.GetFileName(result.ArchivePath)}."
                        : $"Saved {result.Captures} screenshot(s). {result.RestoreWarning}");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    await Dispatcher.InvokeAsync(() => CaptureStatusText.Text = "Capture canceled before the first screenshot.");
                    throw;
                }
                catch (Exception error)
                {
                    await Dispatcher.InvokeAsync(() => CaptureStatusText.Text = error.Message);
                    throw;
                }
                finally
                {
                    _captureOperationActive = false;
                }
            });
            CaptureStatusText.Text = "Capture armed. Focus Roblox and press F6 to begin.";
            UpdateCaptureState();
        }
        catch (Exception error)
        {
            _captureOperationActive = false;
            CaptureStatusText.Text = error.Message;
            UpdateCaptureState();
        }
    }

    private void CaptureStop_Click(object sender, RoutedEventArgs e) => _services.Coordinator.Cancel();

    private void UpdateCaptureState()
    {
        bool busy = _services.Coordinator.IsBusy;
        CaptureArmButton.IsEnabled = !busy;
        CaptureNameText.IsEnabled = !busy;
        CaptureIntervalText.IsEnabled = !busy;
        CaptureStopButton.IsEnabled = _captureOperationActive && busy;
        CaptureStopButton.Content = _services.Coordinator.State == OperationState.Armed ? "Cancel" : "Stop and save";
    }

    private async void RefreshDetector_Click(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync(automatic: false);

    private async void AutoUpdateCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        await _services.UpdateSettingsAsync(settings => settings with { CheckDetectorUpdates = AutoUpdateCheck.IsChecked == true });
    }

    public async Task CheckForUpdatesAsync(bool automatic)
    {
        if (_services.Coordinator.IsBusy) return;
        if (automatic)
        {
            if (!_services.Settings.CheckDetectorUpdates) return;
            if (_services.Settings.LastDetectorUpdateCheck is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromHours(24)) return;
        }
        try
        {
            IReadOnlyList<DetectorPackManifest> packs = await _services.DetectorPacks.ListAsync();
            DetectorPackManifest? installed = packs.FirstOrDefault(pack => pack.PackId == "anime-expeditions-expeditions");
            if (installed is null) return;
            DetectorDetail.Text = "Checking GitHub Releases…";
            await _services.UpdateSettingsAsync(settings => settings with { LastDetectorUpdateCheck = DateTimeOffset.UtcNow });
            DetectorPackUpdate? update = await _services.DetectorUpdates.CheckAsync(installed.PackId, Version.Parse(installed.Version));
            if (update is null)
            {
                DetectorDetail.Text = $"Version {installed.Version} is current.";
                return;
            }
            MessageBoxResult answer = MessageBox.Show(
                Window.GetWindow(this),
                $"Detector pack {update.Version} is available. Install it now?\n\nThe current pack will be retained for rollback.",
                "Detector update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes)
            {
                DetectorDetail.Text = $"Version {update.Version} is available.";
                return;
            }
            DetectorDetail.Text = $"Installing detector pack {update.Version}…";
            await _services.DetectorUpdates.InstallAsync(update);
            await RefreshDetectorAsync();
        }
        catch (Exception error)
        {
            DetectorDetail.Text = automatic ? "Automatic update check will retry later." : $"Update check failed: {error.Message}";
        }
    }

    private async Task RefreshDetectorAsync()
    {
        IReadOnlyList<DetectorPackManifest> packs = await _services.DetectorPacks.ListAsync();
        DetectorPackManifest? pack = packs.FirstOrDefault(value => value.PackId == "anime-expeditions-expeditions");
        DetectorTitle.Text = pack is null ? "No detector pack installed" : $"Anime Expeditions, Expeditions {pack.Version}";
        DetectorDetail.Text = pack is null
            ? "Reinstall the application to restore the bundled detector pack."
            : $"{pack.States.Count} UI states · {pack.Files.Count} compiled references · built {pack.BuiltAt.LocalDateTime:g}";
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }
}
