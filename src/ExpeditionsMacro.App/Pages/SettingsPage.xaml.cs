using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.App.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Automation.Updates;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class SettingsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private bool _loading = true;
    private bool _captureOperationActive;
    private bool _uiScaleOverlayChanging;
    private UiScaleOverlayWindow? _uiScaleOverlay;

    public SettingsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ThemeCombo.ItemsSource = Enum.GetValues<AppTheme>();
        DataPath.Text = services.Paths.Root;
        KeyBindingsPanel.Initialize(services);
        KeyBindingsPanel.BindingsChanged += (_, _) => UpdateKeyBindingDiagnostics();
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateCaptureState);
        Unloaded += (_, _) =>
        {
            CloseUiScaleOverlay();
        };
    }

    public Func<Task>? IdleHotkeyAction => null;

    internal void SetSnapshotScroll(bool showDebug)
    {
        // CI publishes UI snapshots as build artifacts; keep the local Windows
        // profile path out of those images.
        DataPath.Text = @"C:\Users\example\AppData\Local\ExpeditionsMacro";
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
        AutoCaptureOnErrorCheck.IsChecked = _services.Settings.AutoCaptureOnMacroError;
        IncludeLogsCheck.IsChecked = _services.Settings.IncludeLogsInDiagnosticArchives;
        DeepDebugCheck.IsChecked = _services.Settings.DeepDebugEnabled;
        _loading = false;
        VersionText.Text = ProductVersion.Current;
        RobloxText.Text = _services.Automation.FindWindow() is { } window
            ? $"Found: {window.Title} ({window.ProcessDescription})"
            : "Not found";
        KeyBindingsPanel.Refresh();
        UpdateKeyBindingDiagnostics();
        UpdateDeepDebugStatus();
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

    private async void AutoCaptureOnErrorCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        await _services.UpdateSettingsAsync(settings => settings with { AutoCaptureOnMacroError = AutoCaptureOnErrorCheck.IsChecked == true });
    }

    private async void IncludeLogsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        await _services.UpdateSettingsAsync(settings => settings with { IncludeLogsInDiagnosticArchives = IncludeLogsCheck.IsChecked == true });
    }

    private async void DeepDebugCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool enable = DeepDebugCheck.IsChecked == true;
        if (enable)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                Window.GetWindow(this),
                "Deep debug saves every detector frame and input event, plus the selected settings, presets, detector pack, and camera/placement models. A single long run can create a multi-gigabyte ZIP, slow automation, and fill the disk. Files are not deleted automatically.\n\nWebhook values and Discord user IDs are excluded.\n\nEnable deep debug logging?",
                "Enable deep debug logging?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
            {
                _loading = true;
                DeepDebugCheck.IsChecked = false;
                _loading = false;
                DeepDebugStatusText.Text = "Deep debug remains disabled.";
                return;
            }
        }

        DeepDebugCheck.IsEnabled = false;
        try
        {
            await _services.UpdateSettingsAsync(settings => settings with { DeepDebugEnabled = enable });
            UpdateDeepDebugStatus();
        }
        catch (Exception error)
        {
            _loading = true;
            DeepDebugCheck.IsChecked = _services.Settings.DeepDebugEnabled;
            _loading = false;
            DeepDebugStatusText.Text = $"Deep debug setting could not be saved: {error.Message}";
        }
        finally
        {
            UpdateCaptureState();
        }
    }

    private void UpdateKeyBindingDiagnostics()
    {
        HotkeyText.Text = KeyBindingsPanel.MacroDiagnostic;
        PlayMenuKeyDiagnosticText.Text = KeyBindingsPanel.PlayDiagnostic;
        UnitMenuKeyDiagnosticText.Text = KeyBindingsPanel.UnitDiagnostic;
        ShiftLockKeyDiagnosticText.Text = KeyBindingsPanel.ShiftLockDiagnostic;
        DebugCaptureDescription.Text = $"Record the Roblox client at the standard 808 by 611 size. {KeyBindingsPanel.HotkeyDisplayName} starts and stops capture and saves a ZIP for bug reports.";
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Root);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Logs);

    private void OpenCaptures_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Diagnostics);

    private void UpdateDeepDebugStatus()
    {
        DeepDebugStatusText.Text = _services.Settings.DeepDebugEnabled
            ? "Deep debug is enabled. Every completed, canceled, or failed operation will produce a ZIP in Diagnostics."
            : "Deep debug is disabled.";
        DeepDebugStatusText.Foreground = (System.Windows.Media.Brush)FindResource(
            _services.Settings.DeepDebugEnabled ? "ErrorBrush" : "MutedBrush");
    }

    private async void UiScaleOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_uiScaleOverlay is not null)
        {
            CloseUiScaleOverlay();
            UiScaleOverlayStatusText.Text = "Calibration overlay hidden.";
            return;
        }
        if (_services.Coordinator.IsBusy || _uiScaleOverlayChanging) return;

        _uiScaleOverlayChanging = true;
        UiScaleOverlayButton.IsEnabled = false;
        UiScaleOverlayStatusText.Text = "Preparing the standard Roblox client size…";
        try
        {
            RobloxWindow window = _services.Automation.FindWindow()
                ?? throw new InvalidOperationException("Open Roblox before showing the calibration overlay.");
            await _services.Automation.ResizeClientAsync(
                window,
                RobloxClientProfile.Width,
                RobloxClientProfile.Height,
                CancellationToken.None);
            await Task.Delay(250);
            ClientBounds bounds = _services.Automation.GetClientBounds(window);
            if (bounds.Width != RobloxClientProfile.Width || bounds.Height != RobloxClientProfile.Height)
            {
                throw new InvalidOperationException($"Roblox did not accept the standard {RobloxClientProfile.Width} by {RobloxClientProfile.Height} client size.");
            }

            UiScaleOverlayWindow overlay = new(window, _services.Automation);
            overlay.Closed += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(_uiScaleOverlay, overlay))
                {
                    _uiScaleOverlay = null;
                    UiScaleOverlayStatusText.Text = "Calibration overlay closed because Roblox closed or changed size.";
                }
                UiScaleOverlayButton.Content = "Show calibration overlay";
                UpdateCaptureState();
            });
            _uiScaleOverlay = overlay;
            overlay.Show();
            _services.Automation.Focus(window);
            overlay.RefreshPosition();
            UiScaleOverlayButton.Content = "Hide calibration overlay";
            UiScaleOverlayStatusText.Text = "Adjust Roblox UI Scale until the level bar matches the green reference.";
        }
        catch (Exception error)
        {
            CloseUiScaleOverlay();
            UiScaleOverlayStatusText.Text = error.Message;
        }
        finally
        {
            _uiScaleOverlayChanging = false;
            UpdateCaptureState();
        }
    }

    private void CloseUiScaleOverlay()
    {
        UiScaleOverlayWindow? overlay = _uiScaleOverlay;
        _uiScaleOverlay = null;
        overlay?.Close();
        UiScaleOverlayButton.Content = "Show calibration overlay";
    }

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
            Progress<DiagnosticCaptureProgress> progress = new(value =>
            {
                _services.DeepDebug.RecordEvent("diagnostic_capture", "progress", value);
                CaptureStatusText.Text = value.Message;
            });
            _captureOperationActive = true;
            _services.Coordinator.Arm("Diagnostic capture", async token =>
            {
                try
                {
                    DiagnosticCaptureResult result = await _services.DiagnosticCapture.CaptureAsync(
                        name,
                        TimeSpan.FromSeconds(seconds),
                        progress,
                        token,
                        logFilePath: _services.Settings.IncludeLogsInDiagnosticArchives ? _services.Log.CurrentFile : null);
                    await Dispatcher.InvokeAsync(() => CaptureStatusText.Text = result.LogsIncluded
                        ? $"Saved {result.Captures} screenshot(s) and the current log to {Path.GetFileName(result.ArchivePath)}."
                        : $"Saved {result.Captures} screenshot(s) to {Path.GetFileName(result.ArchivePath)}.");
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
            }, new DeepDebugOperationContext
            {
                OperationSettings = new { CaptureName = name, IntervalSeconds = seconds },
            });
            CaptureStatusText.Text = $"Capture armed. Focus Roblox and press {_services.Hotkey.DisplayName} to begin.";
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
        if (busy && _uiScaleOverlay is not null)
        {
            CloseUiScaleOverlay();
            UiScaleOverlayStatusText.Text = "Calibration overlay closed because automation started.";
        }
        CaptureArmButton.IsEnabled = !busy;
        CaptureNameText.IsEnabled = !busy;
        CaptureIntervalText.IsEnabled = !busy;
        AutoCaptureOnErrorCheck.IsEnabled = !busy;
        IncludeLogsCheck.IsEnabled = !busy;
        DeepDebugCheck.IsEnabled = !busy;
        KeyBindingsPanel.UpdateBusyState(busy);
        CaptureStopButton.IsEnabled = _captureOperationActive && busy;
        CaptureStopButton.Content = _services.Coordinator.State == OperationState.Armed ? "Cancel" : "Stop and save";
        UiScaleOverlayButton.IsEnabled = !busy && !_uiScaleOverlayChanging;
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
