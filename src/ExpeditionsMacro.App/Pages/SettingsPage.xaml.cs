using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.App.Windows;
using ExpeditionsMacro.Automation.Updates;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.App.Pages;

public partial class SettingsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private bool _loading = true;
    private bool _captureOperationActive;
    private bool _capturingHotkey;
    private bool _capturingPlayMenuKey;
    private bool _rebindingHotkey;
    private bool _savingPlayMenuKey;
    private bool _uiScaleOverlayChanging;
    private UiScaleOverlayWindow? _uiScaleOverlay;

    public SettingsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ThemeCombo.ItemsSource = Enum.GetValues<AppTheme>();
        DataPath.Text = services.Paths.Root;
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateCaptureState);
        _services.Hotkey.BindingChanged += (_, _) => Dispatcher.BeginInvoke(() => UpdateHotkeyDisplay());
        _services.Hotkey.Pressed += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_capturingHotkey) CancelHotkeyCapture($"{_services.Hotkey.DisplayName} is already the macro hotkey.");
            if (_capturingPlayMenuKey) CancelPlayMenuKeyCapture($"{_services.Hotkey.DisplayName} is already the macro hotkey. Choose a different letter for Toggle Play Menu.");
        });
        Unloaded += (_, _) =>
        {
            if (_capturingHotkey) CancelHotkeyCapture("Key change canceled.");
            if (_capturingPlayMenuKey) CancelPlayMenuKeyCapture("Key change canceled.");
            CloseUiScaleOverlay();
        };
    }

    public Func<Task>? IdleHotkeyAction => null;

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
        AutoCaptureOnErrorCheck.IsChecked = _services.Settings.AutoCaptureOnMacroError;
        IncludeLogsCheck.IsChecked = _services.Settings.IncludeLogsInDiagnosticArchives;
        _loading = false;
        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        RobloxText.Text = _services.Automation.FindWindow() is { } window
            ? $"Found: {window.Title} ({window.ProcessDescription})"
            : "Not found";
        UpdateHotkeyDisplay();
        UpdatePlayMenuKeyDisplay();
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

    private void PlayMenuKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy || _savingPlayMenuKey || _capturingPlayMenuKey) return;
        if (_capturingHotkey) CancelHotkeyCapture("Macro hotkey change canceled.");
        _capturingPlayMenuKey = true;
        PlayMenuKeyButton.Content = "Press a letter...";
        PlayMenuKeyStatusText.Text = "Press the letter assigned to Toggle Play Menu in Anime Expeditions. Escape cancels.";
        Keyboard.Focus(PlayMenuKeyButton);
        UpdateCaptureState();
    }

    private async Task ApplyPlayMenuKeyAsync(char key)
    {
        _savingPlayMenuKey = true;
        UpdateCaptureState();
        try
        {
            _ = AppSettings.ParsePlayMenuKey(key.ToString(), _services.Hotkey.VirtualKey);
            await _services.UpdateSettingsAsync(settings => settings with { PlayMenuKey = key.ToString() });
            UpdatePlayMenuKeyDisplay();
        }
        catch (Exception error)
        {
            UpdatePlayMenuKeyDisplay();
            PlayMenuKeyStatusText.Text = $"Could not save the Play menu key: {error.Message}";
        }
        finally
        {
            _savingPlayMenuKey = false;
            UpdateCaptureState();
        }
    }

    private void UpdatePlayMenuKeyDisplay()
    {
        try
        {
            char key = AppSettings.ParsePlayMenuKey(_services.Settings.PlayMenuKey, _services.Hotkey.VirtualKey);
            if (!_capturingPlayMenuKey) PlayMenuKeyButton.Content = key.ToString();
            PlayMenuKeyStatusText.Text = $"{key} must match Anime Expeditions' Toggle Play Menu binding.";
            PlayMenuKeyDiagnosticText.Text = key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(_services.Settings.PlayMenuKey);
            if (!_capturingPlayMenuKey) PlayMenuKeyButton.Content = empty ? "Set key" : _services.Settings.PlayMenuKey;
            PlayMenuKeyStatusText.Text = empty
                ? "Required before a macro can start. Click the button, then press a letter from A through Z."
                : error.Message;
            PlayMenuKeyDiagnosticText.Text = empty ? "Not set" : "Conflict";
        }
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy || _rebindingHotkey) return;
        if (_capturingPlayMenuKey) CancelPlayMenuKeyCapture("Play menu key change canceled.");
        _capturingHotkey = true;
        HotkeyButton.Content = "Press a key...";
        HotkeyStatusText.Text = "Press a letter, number, punctuation key, numpad key, F1-F11, or F13-F24. Escape cancels; F12 is reserved.";
        Keyboard.Focus(HotkeyButton);
    }

    private async void SettingsPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey && !_capturingPlayMenuKey) return;
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_capturingPlayMenuKey)
        {
            if (key == Key.Escape)
            {
                CancelPlayMenuKeyCapture("Key change canceled.");
                return;
            }

            int playMenuVirtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (playMenuVirtualKey is < 0x41 or > 0x5A)
            {
                PlayMenuKeyStatusText.Text = "Toggle Play Menu must use one letter from A through Z. Press a letter, or Escape to cancel.";
                return;
            }

            if (playMenuVirtualKey == _services.Hotkey.VirtualKey)
            {
                PlayMenuKeyStatusText.Text = $"{(char)playMenuVirtualKey} is already the macro hotkey. Press a different letter.";
                return;
            }

            _capturingPlayMenuKey = false;
            await ApplyPlayMenuKeyAsync((char)playMenuVirtualKey);
            return;
        }

        if (key == Key.Escape)
        {
            CancelHotkeyCapture("Key change canceled.");
            return;
        }

        if (key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            HotkeyStatusText.Text = "Modifier keys cannot be used alone. Press the letter, number, punctuation, numpad, or function key you want.";
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (!GlobalHotkeyService.IsSupportedVirtualKey(virtualKey))
        {
            HotkeyStatusText.Text = "That key is not supported. Choose a letter, number, punctuation key, numpad key, or supported function key.";
            return;
        }

        _capturingHotkey = false;
        await ApplyHotkeyAsync(virtualKey);
    }

    private async Task ApplyHotkeyAsync(int virtualKey)
    {
        int previous = _services.Hotkey.VirtualKey;
        string displayName = GlobalHotkeyService.GetDisplayName(virtualKey);
        try
        {
            if (!string.IsNullOrWhiteSpace(_services.Settings.PlayMenuKey))
            {
                _ = AppSettings.ParsePlayMenuKey(_services.Settings.PlayMenuKey, virtualKey);
            }
        }
        catch (InvalidDataException error)
        {
            _capturingHotkey = false;
            UpdateHotkeyDisplay(updateStatus: false);
            HotkeyStatusText.Text = error.Message;
            return;
        }

        _rebindingHotkey = true;
        HotkeyButton.IsEnabled = false;
        HotkeyButton.Content = displayName;
        HotkeyStatusText.Text = $"Registering {displayName} globally…";
        try
        {
            await Task.Run(() => _services.Hotkey.Rebind(virtualKey));
            await _services.UpdateSettingsAsync(settings => settings with { MacroHotkeyVirtualKey = virtualKey });
            HotkeyStatusText.Text = $"{displayName} is now the macro start and stop key.";
        }
        catch (Exception error)
        {
            if (_services.Hotkey.VirtualKey != previous)
            {
                try { await Task.Run(() => _services.Hotkey.Rebind(previous)); } catch { }
            }
            try
            {
                await _services.UpdateSettingsAsync(settings => settings with { MacroHotkeyVirtualKey = previous });
            }
            catch { }
            HotkeyStatusText.Text = $"Could not change the hotkey: {error.Message}";
        }
        finally
        {
            _rebindingHotkey = false;
            UpdateHotkeyDisplay(updateStatus: false);
            UpdatePlayMenuKeyDisplay();
            UpdateCaptureState();
        }
    }

    private void CancelHotkeyCapture(string status)
    {
        _capturingHotkey = false;
        UpdateHotkeyDisplay(updateStatus: false);
        HotkeyStatusText.Text = status;
        UpdateCaptureState();
    }

    private void CancelPlayMenuKeyCapture(string status)
    {
        _capturingPlayMenuKey = false;
        UpdatePlayMenuKeyDisplay();
        PlayMenuKeyStatusText.Text = status;
        UpdateCaptureState();
    }

    private void UpdateHotkeyDisplay() => UpdateHotkeyDisplay(updateStatus: true);

    private void UpdateHotkeyDisplay(bool updateStatus)
    {
        string hotkey = _services.Hotkey.DisplayName;
        if (!_capturingHotkey) HotkeyButton.Content = hotkey;
        HotkeyText.Text = _services.Hotkey.IsRegistered ? $"{hotkey} registered" : "Unavailable";
        DebugCaptureDescription.Text = $"Record the Roblox client at the standard 808 by 611 size. {hotkey} starts and stops capture and saves a ZIP for bug reports.";
        if (updateStatus && !_capturingHotkey && !_rebindingHotkey)
        {
            HotkeyStatusText.Text = $"{hotkey} is registered globally for every macro workflow.";
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Root);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Logs);

    private void OpenCaptures_Click(object sender, RoutedEventArgs e) => OpenFolder(_services.Paths.Diagnostics);

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
            Progress<DiagnosticCaptureProgress> progress = new(value => CaptureStatusText.Text = value.Message);
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
        PlayMenuKeyButton.IsEnabled = !busy && !_capturingHotkey && !_savingPlayMenuKey;
        CaptureStopButton.IsEnabled = _captureOperationActive && busy;
        CaptureStopButton.Content = _services.Coordinator.State == OperationState.Armed ? "Cancel" : "Stop and save";
        HotkeyButton.IsEnabled = !busy && !_rebindingHotkey && !_capturingPlayMenuKey;
        UiScaleOverlayButton.IsEnabled = !busy && !_uiScaleOverlayChanging;
        if (busy && _capturingHotkey) CancelHotkeyCapture("Stop the current operation before changing the hotkey.");
        if (busy && _capturingPlayMenuKey) CancelPlayMenuKeyCapture("Stop the current operation before changing the Play menu key.");
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
