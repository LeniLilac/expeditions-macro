using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class ExpeditionsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<ExpeditionPreset> _presets = [];
    private readonly ObservableCollection<CameraModelManifest> _cameraModels = [];
    private readonly ObservableCollection<PlacementModel> _placementModels = [];
    private readonly ObservableCollection<DetectorPackManifest> _detectorPacks = [];
    private readonly DispatcherTimer _runtimeTimer;
    private DateTimeOffset? _runStarted;
    private bool _macroOwned;
    private bool _loading;

    public ExpeditionsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        PresetCombo.ItemsSource = _presets;
        CameraCombo.ItemsSource = _cameraModels;
        PlacementCombo.ItemsSource = _placementModels;
        _runtimeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateRuntime(), Dispatcher);
        _runtimeTimer.Stop();
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(CoordinatorStateChanged);
        _services.Hotkey.BindingChanged += (_, _) => Dispatcher.BeginInvoke(UpdateHotkeyText);
    }

    public Func<Task>? IdleHotkeyAction => StartFromHotkeyAsync;

    public async Task OnShownAsync()
    {
        UpdateHotkeyText();
        _loading = true;
        await RefreshCatalogsAsync();
        await RefreshPresetsAsync();
        string selectedId = _services.Settings.SelectedPresetId;
        PresetCombo.SelectedItem = _presets.FirstOrDefault(preset => preset.Id == selectedId) ?? _presets.FirstOrDefault();
        if (PresetCombo.SelectedItem is ExpeditionPreset preset) ApplyPreset(preset);
        else SelectCatalogDefaults();
        string webhook = string.Empty;
        try { webhook = _services.SecretProtector.Unprotect(_services.Settings.EncryptedWebhook); } catch { }
        WebhookPassword.Password = webhook;
        WebhookVisible.Text = webhook;
        DiscordErrorUserIdText.Text = _services.Settings.DiscordErrorUserId;
        _loading = false;
        CoordinatorStateChanged();
    }

    public Task StartFromHotkeyAsync() => StartMacroAsync();

    private async void Start_Click(object sender, RoutedEventArgs e) => await StartMacroAsync();

    private void Stop_Click(object sender, RoutedEventArgs e) => _services.Coordinator.Cancel();

    private void UpdateHotkeyText()
    {
        string hotkey = _services.Hotkey.DisplayName;
        StartButton.Content = $"Start macro  {hotkey}";
        StopButton.Content = $"Stop macro  {hotkey}";
    }

    private async Task StartMacroAsync()
    {
        if (_services.Coordinator.IsBusy) return;
        ExpeditionPreset preset;
        string webhook = CurrentWebhook();
        string discordUserId = DiscordErrorUserIdText.Text.Trim();
        try
        {
            if (!DiscordWebhookClient.ValidateWebhookUrl(webhook)) throw new InvalidOperationException("Enter a valid Discord webhook URL, or leave it blank.");
            if (!DiscordWebhookClient.ValidateDiscordUserId(discordUserId)) throw new InvalidOperationException("Enter a valid Discord user ID, or leave it blank.");
            if (discordUserId.Length > 0 && webhook.Length == 0) throw new InvalidOperationException("A Discord webhook is required when an error-ping user ID is entered.");
            preset = await SavePresetInternalAsync();
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
            AppendLog($"ERROR: {error.Message}");
            return;
        }

        CameraModel camera = await _services.CameraModels.LoadAsync(preset.CameraModelId) ?? throw new InvalidOperationException("The selected camera model could not be loaded.");
        PlacementModel placement = await _services.PlacementModels.LoadAsync(preset.PlacementModelId) ?? throw new InvalidOperationException("The selected placement model could not be loaded.");
        IDetectorPack detector = await _services.DetectorPacks.LoadAsync(preset.DetectorPackId) ?? throw new InvalidOperationException("The selected detector pack could not be loaded.");
        LogText.Clear();
        _runStarted = DateTimeOffset.Now;
        _macroOwned = true;
        _runtimeTimer.Start();
        MacroProgress.Value = 0;
        RepeatsText.Text = VictoriesText.Text = DefeatsText.Text = RecoveriesText.Text = "0";
        AppendLog("Starting Expeditions macro.");
        Progress<MacroProgress> progress = new(value =>
        {
            PhaseText.Text = value.Message;
            MacroProgress.Value = value.Percent;
            if (value.DetectedState is not null) DetectionText.Text = $"Last detection: {Label(value.DetectedState)}{(value.Confidence is null ? string.Empty : $" ({value.Confidence:P0})")}";
        });
        await _services.Coordinator.RunNowAsync("Expeditions macro", token => RunWithFailureHandlingAsync(
            "Expeditions Macro",
            webhook,
            discordUserId,
            () => _services.Expeditions.RunAsync(
                preset,
                camera,
                placement,
                detector,
                webhook,
                progress,
                entry => Dispatcher.BeginInvoke(() => AppendLog(entry.Level == MacroEventLevel.Error ? $"ERROR: {entry.Message}" : entry.Message)),
                summary => Dispatcher.BeginInvoke(() => ApplySummary(summary)),
                token),
            token));
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExpeditionPreset preset = await SavePresetInternalAsync();
            PhaseText.Text = $"Preset '{preset.Name}' saved.";
        }
        catch (Exception error)
        {
            PhaseText.Text = error.Message;
        }
    }

    private async Task<ExpeditionPreset> SavePresetInternalAsync()
    {
        if (CameraCombo.SelectedItem is not CameraModelManifest camera) throw new InvalidOperationException("Create and select a camera model.");
        if (PlacementCombo.SelectedItem is not PlacementModel placement) throw new InvalidOperationException("Create and select a placement model.");
        DetectorPackManifest detector = CurrentDetectorPack();
        string name = PresetNameText.Text.Trim();
        string id = ModelId.FromName(name);
        ExpeditionPreset preset = new()
        {
            Id = id,
            Name = name,
            MapNumber = SelectedTag(MapCombo),
            Difficulty = SelectedTag(DifficultyCombo),
            CameraModelId = camera.Id,
            PlacementModelId = placement.Id,
            DetectorPackId = detector.PackId,
            ExtractAtCheckpoint = ExtractCheck.IsChecked == true,
            BossesBeforeExtract = ParseInt(BossTargetText, "Boss nodes before extraction"),
            AutoRecover = AutoRecoverCheck.IsChecked == true,
            ZoomTicks = ParseInt(ZoomTicksText, "Zoom ticks"),
            PitchDragPixels = ParseInt(PitchPixelsText, "Pitch drag"),
            PollMilliseconds = ParseInt(PollText, "Detection interval"),
            StableDetections = ParseInt(StableText, "Stable frames"),
            UnitKeyHoldMilliseconds = ParseInt(KeyHoldText, "Key hold"),
            UnitSelectDelayMilliseconds = ParseInt(KeyDelayText, "Key-to-click delay"),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        preset.Validate();
        string webhook = CurrentWebhook();
        string discordUserId = DiscordErrorUserIdText.Text.Trim();
        if (!DiscordWebhookClient.ValidateWebhookUrl(webhook)) throw new InvalidOperationException("Enter a valid Discord webhook URL, or leave it blank.");
        if (!DiscordWebhookClient.ValidateDiscordUserId(discordUserId)) throw new InvalidOperationException("Enter a valid Discord user ID, or leave it blank.");
        if (discordUserId.Length > 0 && webhook.Length == 0) throw new InvalidOperationException("A Discord webhook is required when an error-ping user ID is entered.");
        await _services.Presets.SaveAsync(preset);
        await _services.UpdateSettingsAsync(settings => settings with
        {
            SelectedPresetId = preset.Id,
            EncryptedWebhook = _services.SecretProtector.Protect(webhook),
            DiscordErrorUserId = discordUserId,
        });
        await RefreshPresetsAsync();
        PresetCombo.SelectedItem = _presets.FirstOrDefault(value => value.Id == preset.Id);
        return preset;
    }

    private async void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PresetCombo.SelectedItem is not ExpeditionPreset preset) return;
        ApplyPreset(preset);
        await _services.UpdateSettingsAsync(settings => settings with { SelectedPresetId = preset.Id });
    }

    private void ApplyPreset(ExpeditionPreset preset)
    {
        PresetNameText.Text = preset.Name;
        MapCombo.SelectedIndex = preset.MapNumber - 1;
        DifficultyCombo.SelectedIndex = preset.Difficulty - 1;
        CameraCombo.SelectedItem = _cameraModels.FirstOrDefault(model => model.Id == preset.CameraModelId);
        PlacementCombo.SelectedItem = _placementModels.FirstOrDefault(model => model.Id == preset.PlacementModelId);
        ExtractCheck.IsChecked = preset.ExtractAtCheckpoint;
        BossTargetText.Text = preset.BossesBeforeExtract.ToString(CultureInfo.InvariantCulture);
        AutoRecoverCheck.IsChecked = preset.AutoRecover;
        ZoomTicksText.Text = preset.ZoomTicks.ToString(CultureInfo.InvariantCulture);
        PitchPixelsText.Text = preset.PitchDragPixels.ToString(CultureInfo.InvariantCulture);
        PollText.Text = preset.PollMilliseconds.ToString(CultureInfo.InvariantCulture);
        StableText.Text = preset.StableDetections.ToString(CultureInfo.InvariantCulture);
        KeyHoldText.Text = preset.UnitKeyHoldMilliseconds.ToString(CultureInfo.InvariantCulture);
        KeyDelayText.Text = preset.UnitSelectDelayMilliseconds.ToString(CultureInfo.InvariantCulture);
        ExtractCheck_Changed(this, new RoutedEventArgs());
    }

    private void NewPreset_Click(object sender, RoutedEventArgs e)
    {
        PresetCombo.SelectedItem = null;
        PresetNameText.Text = "Expedition route";
        MapCombo.SelectedIndex = 0;
        DifficultyCombo.SelectedIndex = 0;
        ExtractCheck.IsChecked = true;
        BossTargetText.Text = "1";
        AutoRecoverCheck.IsChecked = true;
        SelectCatalogDefaults();
    }

    private async Task RefreshCatalogsAsync()
    {
        IReadOnlyList<CameraModelManifest> cameras = await _services.CameraModels.ListAsync();
        IReadOnlyList<PlacementModel> placements = await _services.PlacementModels.ListAsync();
        IReadOnlyList<DetectorPackManifest> detectors = await _services.DetectorPacks.ListAsync();
        _cameraModels.Clear(); foreach (CameraModelManifest model in cameras) _cameraModels.Add(model);
        _placementModels.Clear(); foreach (PlacementModel model in placements) _placementModels.Add(model);
        _detectorPacks.Clear(); foreach (DetectorPackManifest pack in detectors) _detectorPacks.Add(pack);
    }

    private async Task RefreshPresetsAsync()
    {
        IReadOnlyList<ExpeditionPreset> presets = await _services.Presets.ListAsync();
        _presets.Clear(); foreach (ExpeditionPreset preset in presets) _presets.Add(preset);
    }

    private void SelectCatalogDefaults()
    {
        CameraCombo.SelectedItem ??= _cameraModels.FirstOrDefault();
        PlacementCombo.SelectedItem ??= _placementModels.FirstOrDefault();
    }

    private void ApplySummary(ExpeditionRunSummary summary)
    {
        RuntimeText.Text = summary.Runtime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        RepeatsText.Text = summary.Repeats.ToString(CultureInfo.InvariantCulture);
        VictoriesText.Text = summary.Victories.ToString(CultureInfo.InvariantCulture);
        DefeatsText.Text = summary.Defeats.ToString(CultureInfo.InvariantCulture);
        RecoveriesText.Text = summary.Recoveries.ToString(CultureInfo.InvariantCulture);
    }

    private void CoordinatorStateChanged()
    {
        bool busy = _services.Coordinator.IsBusy;
        StartButton.IsEnabled = !busy;
        StopButton.IsEnabled = busy;
        SetConfigurationEnabled(!busy);
        if (!busy && _macroOwned && _runStarted is not null)
        {
            _macroOwned = false;
            _runtimeTimer.Stop();
            PhaseText.Text = "Macro stopped. Roblox remains at the standard client size.";
            AppendLog("Macro stopped.");
        }
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        foreach (Control control in new Control[] { PresetCombo, PresetNameText, MapCombo, DifficultyCombo, CameraCombo, PlacementCombo, BossTargetText, WebhookPassword, WebhookVisible, DiscordErrorUserIdText, ZoomTicksText, PitchPixelsText, PollText, StableText, KeyHoldText, KeyDelayText }) control.IsEnabled = enabled;
        ExtractCheck.IsEnabled = enabled;
        AutoRecoverCheck.IsEnabled = enabled;
        ShowWebhookCheck.IsEnabled = enabled;
    }

    private void ExtractCheck_Changed(object sender, RoutedEventArgs e)
    {
        // IsChecked is applied while XAML is still constructing the grid, before
        // controls declared later in the document have generated fields.
        if (ExtractCheck is null || BossTargetText is null) return;
        BossTargetText.IsEnabled = ExtractCheck.IsChecked == true && !_services.Coordinator.IsBusy;
    }

    private void ShowWebhook_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowWebhookCheck is null || WebhookPassword is null || WebhookVisible is null) return;
        if (ShowWebhookCheck.IsChecked == true)
        {
            WebhookVisible.Text = WebhookPassword.Password;
            WebhookPassword.Visibility = Visibility.Collapsed;
            WebhookVisible.Visibility = Visibility.Visible;
        }
        else
        {
            WebhookPassword.Password = WebhookVisible.Text;
            WebhookVisible.Visibility = Visibility.Collapsed;
            WebhookPassword.Visibility = Visibility.Visible;
        }
    }

    private void TuningToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = TuningPanel.Visibility != Visibility.Visible;
        TuningPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TuningToggle.Content = show ? "Hide tuning" : "Show tuning";
    }

    private void UpdateRuntime()
    {
        if (_runStarted is not null) RuntimeText.Text = (DateTimeOffset.Now - _runStarted.Value).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private string CurrentWebhook() => ShowWebhookCheck.IsChecked == true ? WebhookVisible.Text.Trim() : WebhookPassword.Password.Trim();

    private DetectorPackManifest CurrentDetectorPack() =>
        _detectorPacks.FirstOrDefault()
        ?? throw new InvalidOperationException("No detector pack is installed.");

    private async Task RunWithFailureHandlingAsync(
        string macroName,
        string webhook,
        string discordUserId,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                PhaseText.Text = "Macro failed. Running configured error diagnostics.";
                AppendLog($"ERROR: {error.Message}");
            });
            MacroFailureHandlingResult result = await _services.HandleMacroFailureAsync(macroName, webhook, discordUserId, error);
            await Dispatcher.InvokeAsync(() => AppendFailureHandlingResult(result));
            throw;
        }
    }

    private void AppendFailureHandlingResult(MacroFailureHandlingResult result)
    {
        if (result.DiagnosticArchivePath is not null)
        {
            AppendLog($"Automatic error diagnostics saved to {System.IO.Path.GetFileName(result.DiagnosticArchivePath)}.");
        }
        if (result.DiagnosticError is not null)
        {
            AppendLog($"ERROR: Automatic error diagnostics: {result.DiagnosticError}");
        }
        if (result.DiscordPingsSent)
        {
            AppendLog($"Sent {DiscordWebhookClient.ErrorPingCount} Discord error alerts.");
        }
        if (result.DiscordError is not null)
        {
            AppendLog($"ERROR: Discord error alerts: {result.DiscordError}");
        }
    }

    private void AppendLog(string message)
    {
        if (message.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)) _services.Log.Error(message[6..].Trim());
        else _services.Log.Info(message);
        LogText.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        if (LogText.LineCount > 500)
        {
            int removeThrough = LogText.GetCharacterIndexFromLineIndex(LogText.LineCount - 500);
            LogText.Text = LogText.Text[removeThrough..];
            LogText.CaretIndex = LogText.Text.Length;
        }
        LogText.ScrollToEnd();
    }

    private static int ParseInt(TextBox field, string label) => int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : throw new FormatException($"{label} must be a whole number.");

    private static int SelectedTag(ComboBox combo) => combo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int value) ? value : throw new InvalidOperationException("Choose a route value.");

    private static string Label(string value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' '));
}
