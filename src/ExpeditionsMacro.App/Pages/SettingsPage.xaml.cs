using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.App.Windows;
using ExpeditionsMacro.Automation.Diagnostics;
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

    internal void SetSnapshotScroll(
        bool showDebug,
        bool showDiagnostics = false)
    {
        // CI publishes UI snapshots as build artifacts; keep the local Windows
        // profile path out of those images.
        DataPath.Text = @"C:\Users\example\AppData\Local\ExpeditionsMacro";
        SettingsScroll.UpdateLayout();
        if (showDiagnostics)
        {
            DiagnosticsSection.BringIntoView();
        }
        else if (showDebug)
        {
            SettingsScroll.ScrollToEnd();
        }
        else
        {
            SettingsScroll.ScrollToTop();
        }
    }

    public Task OnShownAsync()
    {
        _loading = true;
        ThemeCombo.SelectedItem = _services.Settings.Theme;
        MinimizeCheck.IsChecked = _services.Settings.MinimizeDuringAutomation;
        AutoCaptureOnErrorCheck.IsChecked = _services.Settings.AutoCaptureOnMacroError;
        IncludeLogsCheck.IsChecked = _services.Settings.IncludeLogsInDiagnosticArchives;
        DeepDebugCheck.IsChecked = _services.Settings.DeepDebugEnabled;
        DebugModeCheck.IsChecked = _services.Settings.DebugModeEnabled;
        ManualRecordingsCheck.IsChecked =
            _services.Settings
                .ManualInputRecordingEnabled;
        _loading = false;
        VersionText.Text = ProductVersion.Current;
        RobloxText.Text = _services.Automation.FindWindow() is { } window
            ? $"Found: {window.Title} ({window.ProcessDescription})"
            : "Not found";
        KeyBindingsPanel.Refresh();
        UpdateKeyBindingDiagnostics();
        UpdateDeepDebugStatus();
        UpdateCaptureState();
        return Task.CompletedTask;
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

    private async void DebugModeCheck_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_loading) return;
        DebugModeCheck.IsEnabled = false;
        try
        {
            await _services.UpdateSettingsAsync(
                settings => settings with
                {
                    DebugModeEnabled =
                        DebugModeCheck.IsChecked == true,
                });
        }
        catch
        {
            _loading = true;
            DebugModeCheck.IsChecked =
                _services.Settings.DebugModeEnabled;
            _loading = false;
            throw;
        }
        finally
        {
            UpdateCaptureState();
        }
    }

    private async void DeepDebugCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool enable = DeepDebugCheck.IsChecked == true;
        if (enable)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                Window.GetWindow(this),
                "Deep debug saves every detector frame and input event, plus the selected settings, presets, detector pack, and referenced Placement Setups. A single long run can create a multi-gigabyte ZIP, slow automation, and fill the disk. Files are not deleted automatically.\n\nWebhook values and Discord user IDs are excluded.\n\nEnable deep debug logging?",
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
        AreasMenuKeyDiagnosticText.Text =
            KeyBindingsPanel.AreasDiagnostic;
        CancelPlacementKeyDiagnosticText.Text =
            KeyBindingsPanel.CancelPlacementDiagnostic;
        QuickPlacementKeyDiagnosticText.Text =
            KeyBindingsPanel.QuickPlacementDiagnostic;
        TargetingKeyDiagnosticText.Text =
            KeyBindingsPanel.TargetingDiagnostic;
        UpgradeUnitKeyDiagnosticText.Text =
            KeyBindingsPanel.UpgradeDiagnostic;
        AutoUpgradeUnitKeyDiagnosticText.Text =
            KeyBindingsPanel.AutoUpgradeDiagnostic;
        ToggleAutoUpgradePlacedUnitsKeyDiagnosticText.Text =
            KeyBindingsPanel
                .ToggleAutoUpgradePlacedUnitsDiagnostic;
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
        DebugModeCheck.IsEnabled = !busy;
        ManualRecordingsCheck.IsEnabled = !busy;
        KeyBindingsPanel.UpdateBusyState(busy);
        CaptureStopButton.IsEnabled = _captureOperationActive && busy;
        CaptureStopButton.Content = _services.Coordinator.State == OperationState.Armed ? "Cancel" : "Stop and save";
        UiScaleOverlayButton.IsEnabled = !busy && !_uiScaleOverlayChanging;
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }
}
