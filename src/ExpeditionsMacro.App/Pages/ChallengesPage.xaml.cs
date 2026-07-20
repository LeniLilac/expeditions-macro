using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class ChallengesPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<ChallengePreset> _presets = [];
    private readonly ObservableCollection<ExpeditionPreset> _expeditionPresets = [];
    private readonly ObservableCollection<DetectorPackManifest> _detectorPacks = [];
    private readonly DispatcherTimer _resetTimer;
    private readonly DispatcherTimer _runtimeTimer;
    private DateTimeOffset? _runStarted;
    private bool _macroOwned;
    private bool _loading;

    public ChallengesPage(AppServices services)
    {
        _services = services;
        MapRows = new ObservableCollection<ChallengeMapRow>(Enum.GetValues<ChallengeMapId>().Select(map => new ChallengeMapRow(map)));
        InitializeComponent();
        DataContext = this;
        PresetCombo.ItemsSource = _presets;
        ExpeditionPresetCombo.ItemsSource = _expeditionPresets;
        DetectorCombo.ItemsSource = _detectorPacks;
        _resetTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateResetCountdown(), Dispatcher);
        _resetTimer.Start();
        _runtimeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateRuntime(), Dispatcher);
        _runtimeTimer.Stop();
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(CoordinatorStateChanged);
        _services.Hotkey.BindingChanged += (_, _) => Dispatcher.BeginInvoke(UpdateHotkeyText);
        UpdateResetCountdown();
    }

    public ObservableCollection<ChallengeMapRow> MapRows { get; }

    public ObservableCollection<CatalogOption> CameraOptions { get; } = [];

    public ObservableCollection<CatalogOption> PlacementOptions { get; } = [];

    public Func<Task>? IdleHotkeyAction => StartFromHotkeyAsync;

    internal void SetSnapshotScroll(bool showEnd)
    {
        UpdateLayout();
        if (showEnd) PageScroll.ScrollToEnd();
        else PageScroll.ScrollToTop();
    }

    public async Task OnShownAsync()
    {
        UpdateHotkeyText();
        _loading = true;
        await RefreshCatalogsAsync();
        await RefreshPresetsAsync();
        string selectedId = _services.Settings.SelectedChallengePresetId;
        PresetCombo.SelectedItem = _presets.FirstOrDefault(preset => preset.Id == selectedId) ?? _presets.FirstOrDefault();
        if (PresetCombo.SelectedItem is ChallengePreset preset) ApplyPreset(preset);
        else ApplyNewPreset();
        string webhook = string.Empty;
        try { webhook = _services.SecretProtector.Unprotect(_services.Settings.EncryptedWebhook); } catch { }
        WebhookPassword.Password = webhook;
        WebhookVisible.Text = webhook;
        DiscordErrorUserIdText.Text = _services.Settings.DiscordErrorUserId;
        _loading = false;
        CoordinatorStateChanged();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _services.Coordinator.Cancel();

    public Task StartFromHotkeyAsync() => StartMacroAsync();

    private async void Start_Click(object sender, RoutedEventArgs e) => await StartMacroAsync();

    private void UpdateHotkeyText()
    {
        string hotkey = _services.Hotkey.DisplayName;
        StartButton.Content = $"Start macro  {hotkey}";
        StopButton.Content = $"Stop and restore  {hotkey}";
    }

    private async Task StartMacroAsync()
    {
        if (_services.Coordinator.IsBusy) return;
        ChallengePreset preset;
        IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels> mapModels;
        IDetectorPack detector;
        string webhook = CurrentWebhook();
        string discordUserId = DiscordErrorUserIdText.Text.Trim();
        Func<DateTimeOffset, CancellationToken, Task>? idleWorkflow = null;
        try
        {
            if (!DiscordWebhookClient.ValidateWebhookUrl(webhook)) throw new InvalidOperationException("Enter a valid Discord webhook URL, or leave it blank.");
            if (!DiscordWebhookClient.ValidateDiscordUserId(discordUserId)) throw new InvalidOperationException("Enter a valid Discord user ID, or leave it blank.");
            if (discordUserId.Length > 0 && webhook.Length == 0) throw new InvalidOperationException("A Discord webhook is required when an error-ping user ID is entered.");
            preset = await SavePresetInternalAsync();
            preset.ValidateReady();
            detector = await _services.DetectorPacks.LoadAsync(preset.DetectorPackId) ?? throw new InvalidOperationException("The selected detector pack could not be loaded.");
            mapModels = await LoadMapModelsAsync(preset);
            if (preset.IdleBehavior == ChallengeIdleBehavior.RunExpeditions)
            {
                ExpeditionPreset expedition = await _services.Presets.LoadAsync(preset.ExpeditionPresetId) ?? throw new InvalidOperationException("The selected Expeditions fallback preset could not be loaded.");
                CameraModel camera = await _services.CameraModels.LoadAsync(expedition.CameraModelId) ?? throw new InvalidOperationException("The fallback Expeditions camera model could not be loaded.");
                PlacementModel placement = await _services.PlacementModels.LoadAsync(expedition.PlacementModelId) ?? throw new InvalidOperationException("The fallback Expeditions placement model could not be loaded.");
                IDetectorPack expeditionDetector = await _services.DetectorPacks.LoadAsync(expedition.DetectorPackId) ?? throw new InvalidOperationException("The fallback Expeditions detector pack could not be loaded.");
                idleWorkflow = (untilUtc, token) => RunExpeditionsUntilAsync(untilUtc, expedition, camera, placement, expeditionDetector, webhook, token);
            }
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
            AppendLog($"ERROR: {error.Message}");
            return;
        }

        LogText.Clear();
        _runStarted = DateTimeOffset.Now;
        _macroOwned = true;
        _runtimeTimer.Start();
        MacroProgress.Value = 0;
        CompletedText.Text = VictoriesText.Text = DefeatsText.Text = "0";
        RecoveryText.Text = "0 / 0";
        AppendLog("Starting Challenge macro.");
        Progress<MacroProgress> progress = new(value =>
        {
            StatusText.Text = value.Message;
            MacroProgress.Value = value.Percent;
            if (value.DetectedState is not null) DetectionText.Text = $"Last detection: {Label(value.DetectedState)}{(value.Confidence is null ? string.Empty : $" ({value.Confidence:P0})")}";
        });
        await _services.Coordinator.RunNowAsync("Challenge macro", token => RunWithFailureHandlingAsync(
            "Challenge Macro",
            webhook,
            discordUserId,
            () => _services.Challenges.RunAsync(
                preset,
                mapModels,
                detector,
                webhook,
                idleWorkflow,
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
            ChallengePreset preset = await SavePresetInternalAsync();
            StatusText.Text = $"Preset '{preset.Name}' saved locally.";
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
    }

    private void ValidateSetup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ChallengePreset preset = BuildPreset();
            preset.ValidateReady();
            StatusText.Text = "All five map profiles are complete and ready to run.";
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
    }

    private async Task<ChallengePreset> SavePresetInternalAsync()
    {
        ChallengePreset preset = BuildPreset();
        preset.Validate();
        string webhook = CurrentWebhook();
        if (!DiscordWebhookClient.ValidateWebhookUrl(webhook)) throw new InvalidOperationException("Enter a valid Discord webhook URL, or leave it blank.");
        string discordUserId = DiscordErrorUserIdText.Text.Trim();
        if (!DiscordWebhookClient.ValidateDiscordUserId(discordUserId)) throw new InvalidOperationException("Enter a valid Discord user ID, or leave it blank.");
        if (discordUserId.Length > 0 && webhook.Length == 0) throw new InvalidOperationException("A Discord webhook is required when an error-ping user ID is entered.");
        await _services.ChallengePresets.SaveAsync(preset);
        await _services.UpdateSettingsAsync(settings => settings with
        {
            SelectedChallengePresetId = preset.Id,
            EncryptedWebhook = _services.SecretProtector.Protect(webhook),
            DiscordErrorUserId = discordUserId,
        });
        await RefreshPresetsAsync();
        PresetCombo.SelectedItem = _presets.FirstOrDefault(value => value.Id == preset.Id);
        return preset;
    }

    private ChallengePreset BuildPreset()
    {
        if (DetectorCombo.SelectedItem is not DetectorPackManifest detector) throw new InvalidOperationException("No detector pack is installed.");
        string name = PresetNameText.Text.Trim();
        ChallengeIdleBehavior idleBehavior = SelectedIdleBehavior();
        return new ChallengePreset
        {
            Id = ModelId.FromName(name),
            Name = name,
            RunTraitChallenge = TraitCheck.IsChecked == true,
            RunStatChallenge = StatCheck.IsChecked == true,
            RunSpriteChallenge = SpriteCheck.IsChecked == true,
            Maps = MapRows.Select(row => row.ToProfile()).ToArray(),
            DetectorPackId = detector.PackId,
            IdleBehavior = idleBehavior,
            ExpeditionPresetId = idleBehavior == ChallengeIdleBehavior.RunExpeditions && ExpeditionPresetCombo.SelectedItem is ExpeditionPreset expedition ? expedition.Id : string.Empty,
            AutoRecover = AutoRecoverCheck.IsChecked == true,
            DefeatRetries = ParseInt(DefeatRetriesText, "Defeat retries"),
            ZoomTicks = ParseInt(ZoomTicksText, "Zoom ticks"),
            PitchDragPixels = ParseInt(PitchPixelsText, "Pitch drag"),
            PollMilliseconds = ParseInt(PollText, "Detection interval"),
            StableDetections = ParseInt(StableText, "Stable frames"),
            UnitKeyHoldMilliseconds = ParseInt(KeyHoldText, "Key hold"),
            UnitSelectDelayMilliseconds = ParseInt(KeyDelayText, "Key-to-click delay"),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || PresetCombo.SelectedItem is not ChallengePreset preset) return;
        ApplyPreset(preset);
        await _services.UpdateSettingsAsync(settings => settings with { SelectedChallengePresetId = preset.Id });
    }

    private void ApplyPreset(ChallengePreset preset)
    {
        PresetNameText.Text = preset.Name;
        TraitCheck.IsChecked = preset.RunTraitChallenge;
        StatCheck.IsChecked = preset.RunStatChallenge;
        SpriteCheck.IsChecked = preset.RunSpriteChallenge;
        IdleBehaviorCombo.SelectedIndex = preset.IdleBehavior == ChallengeIdleBehavior.RunExpeditions ? 1 : 0;
        ExpeditionPresetCombo.SelectedItem = _expeditionPresets.FirstOrDefault(value => value.Id == preset.ExpeditionPresetId);
        AutoRecoverCheck.IsChecked = preset.AutoRecover;
        DefeatRetriesText.Text = preset.DefeatRetries.ToString(CultureInfo.InvariantCulture);
        DetectorCombo.SelectedItem = _detectorPacks.FirstOrDefault(pack => pack.PackId == preset.DetectorPackId);
        ZoomTicksText.Text = preset.ZoomTicks.ToString(CultureInfo.InvariantCulture);
        PitchPixelsText.Text = preset.PitchDragPixels.ToString(CultureInfo.InvariantCulture);
        PollText.Text = preset.PollMilliseconds.ToString(CultureInfo.InvariantCulture);
        StableText.Text = preset.StableDetections.ToString(CultureInfo.InvariantCulture);
        KeyHoldText.Text = preset.UnitKeyHoldMilliseconds.ToString(CultureInfo.InvariantCulture);
        KeyDelayText.Text = preset.UnitSelectDelayMilliseconds.ToString(CultureInfo.InvariantCulture);
        foreach (ChallengeMapRow row in MapRows)
        {
            ChallengeMapProfile profile = preset.Maps.Single(value => value.Map == row.Map);
            row.Apply(profile);
        }
        UpdateIdleBehaviorAvailability();
    }

    private void NewPreset_Click(object sender, RoutedEventArgs e)
    {
        PresetCombo.SelectedItem = null;
        ApplyNewPreset();
    }

    private void ApplyNewPreset()
    {
        PresetNameText.Text = "Challenge rotation";
        TraitCheck.IsChecked = StatCheck.IsChecked = SpriteCheck.IsChecked = true;
        IdleBehaviorCombo.SelectedIndex = 0;
        ExpeditionPresetCombo.SelectedItem = _expeditionPresets.FirstOrDefault();
        AutoRecoverCheck.IsChecked = true;
        DefeatRetriesText.Text = "0";
        DetectorCombo.SelectedItem = _detectorPacks.FirstOrDefault();
        ZoomTicksText.Text = "30";
        PitchPixelsText.Text = "1800";
        PollText.Text = "450";
        StableText.Text = "2";
        KeyHoldText.Text = "110";
        KeyDelayText.Text = "250";
        foreach (ChallengeMapRow row in MapRows) row.Apply(new ChallengeMapProfile { Map = row.Map });
        UpdateIdleBehaviorAvailability();
        StatusText.Text = "Configuration can be saved. Complete all five map profiles before starting.";
    }

    private async Task RefreshCatalogsAsync()
    {
        IReadOnlyList<CameraModelManifest> cameras = await _services.CameraModels.ListAsync();
        IReadOnlyList<PlacementModel> placements = await _services.PlacementModels.ListAsync();
        IReadOnlyList<ExpeditionPreset> expeditionPresets = await _services.Presets.ListAsync();
        IReadOnlyList<DetectorPackManifest> detectorPacks = await _services.DetectorPacks.ListAsync();

        CameraOptions.Clear();
        CameraOptions.Add(new CatalogOption(string.Empty, "Choose model"));
        foreach (CameraModelManifest camera in cameras) CameraOptions.Add(new CatalogOption(camera.Id, camera.Name));
        PlacementOptions.Clear();
        PlacementOptions.Add(new CatalogOption(string.Empty, "None"));
        foreach (PlacementModel placement in placements) PlacementOptions.Add(new CatalogOption(placement.Id, placement.Name));
        _expeditionPresets.Clear();
        foreach (ExpeditionPreset preset in expeditionPresets) _expeditionPresets.Add(preset);
        _detectorPacks.Clear();
        foreach (DetectorPackManifest pack in detectorPacks) _detectorPacks.Add(pack);
    }

    private async Task RefreshPresetsAsync()
    {
        IReadOnlyList<ChallengePreset> presets = await _services.ChallengePresets.ListAsync();
        _presets.Clear();
        foreach (ChallengePreset preset in presets) _presets.Add(preset);
    }

    private void IdleBehaviorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateIdleBehaviorAvailability();
    }

    private void UpdateIdleBehaviorAvailability()
    {
        if (ExpeditionPresetCombo is null || IdleBehaviorCombo is null) return;
        ExpeditionPresetCombo.IsEnabled = SelectedIdleBehavior() == ChallengeIdleBehavior.RunExpeditions && !_services.Coordinator.IsBusy;
    }

    private ChallengeIdleBehavior SelectedIdleBehavior() =>
        IdleBehaviorCombo.SelectedItem is ComboBoxItem item && string.Equals(item.Tag?.ToString(), "expeditions", StringComparison.OrdinalIgnoreCase)
            ? ChallengeIdleBehavior.RunExpeditions
            : ChallengeIdleBehavior.WaitForReset;

    private void TuningToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = TuningPanel.Visibility != Visibility.Visible;
        TuningPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TuningToggle.Content = show ? "Hide tuning" : "Show tuning";
    }

    private void CoordinatorStateChanged()
    {
        bool enabled = !_services.Coordinator.IsBusy;
        StopButton.IsEnabled = !enabled;
        PresetCombo.IsEnabled = enabled;
        PresetNameText.IsEnabled = enabled;
        TraitCheck.IsEnabled = enabled;
        StatCheck.IsEnabled = enabled;
        SpriteCheck.IsEnabled = enabled;
        IdleBehaviorCombo.IsEnabled = enabled;
        AutoRecoverCheck.IsEnabled = enabled;
        DefeatRetriesText.IsEnabled = enabled;
        DetectorCombo.IsEnabled = enabled;
        ZoomTicksText.IsEnabled = enabled;
        PitchPixelsText.IsEnabled = enabled;
        PollText.IsEnabled = enabled;
        StableText.IsEnabled = enabled;
        KeyHoldText.IsEnabled = enabled;
        KeyDelayText.IsEnabled = enabled;
        WebhookPassword.IsEnabled = enabled;
        WebhookVisible.IsEnabled = enabled;
        DiscordErrorUserIdText.IsEnabled = enabled;
        ShowWebhookCheck.IsEnabled = enabled;
        MapItems.IsEnabled = enabled;
        StartButton.IsEnabled = enabled;
        UpdateIdleBehaviorAvailability();
        if (enabled && _macroOwned && _runStarted is not null)
        {
            _macroOwned = false;
            _runtimeTimer.Stop();
            StatusText.Text = "Macro stopped. Roblox window and input state were restored.";
            AppendLog("Macro stopped.");
        }
    }

    private void UpdateResetCountdown()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset next = ChallengeRunPolicy.NextGlobalReset(now);
        TimeSpan remaining = next - now;
        NextResetText.Text = $"Next reset in {(int)remaining.TotalMinutes}m {remaining.Seconds:00}s";
    }

    private async Task<IReadOnlyDictionary<ChallengeMapId, ChallengeMapRuntimeModels>> LoadMapModelsAsync(ChallengePreset preset)
    {
        Dictionary<ChallengeMapId, ChallengeMapRuntimeModels> result = [];
        foreach (ChallengeMapProfile profile in preset.Maps)
        {
            CameraModel camera = await _services.CameraModels.LoadAsync(profile.CameraModelId) ?? throw new InvalidOperationException($"The {Label(profile.Map)} camera model could not be loaded.");
            PlacementModel? prestart = string.IsNullOrWhiteSpace(profile.PrestartPlacementModelId)
                ? null
                : await _services.PlacementModels.LoadAsync(profile.PrestartPlacementModelId) ?? throw new InvalidOperationException($"The {Label(profile.Map)} before-start placement model could not be loaded.");
            PlacementModel? delayed = string.IsNullOrWhiteSpace(profile.DelayedPlacementModelId)
                ? null
                : await _services.PlacementModels.LoadAsync(profile.DelayedPlacementModelId) ?? throw new InvalidOperationException($"The {Label(profile.Map)} delayed placement model could not be loaded.");
            result[profile.Map] = new ChallengeMapRuntimeModels(camera, prestart, delayed);
        }
        return result;
    }

    private async Task RunExpeditionsUntilAsync(
        DateTimeOffset untilUtc,
        ExpeditionPreset preset,
        CameraModel camera,
        PlacementModel placement,
        IDetectorPack detector,
        string webhookUrl,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = untilUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(remaining);
        try
        {
            AppendLog($"Running Expeditions preset '{preset.Name}' while Challenges are unavailable.");
            await _services.Expeditions.RunAsync(
                preset,
                camera,
                placement,
                detector,
                webhookUrl,
                progress: new Progress<MacroProgress>(value => DispatchUi(() => StatusText.Text = $"Expeditions fallback: {value.Message}")),
                log: entry => AppendLog($"Expeditions: {entry.Message}"),
                summaryChanged: null,
                cancellationToken: deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && DateTimeOffset.UtcNow >= untilUtc - TimeSpan.FromSeconds(1))
        {
            AppendLog("Challenge reset reached. Stopping the Expeditions fallback and returning to Challenges.");
        }
    }

    private void ApplySummary(ChallengeRunSummary summary)
    {
        RuntimeText.Text = summary.Runtime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        CompletedText.Text = summary.Completed.ToString(CultureInfo.InvariantCulture);
        VictoriesText.Text = summary.Victories.ToString(CultureInfo.InvariantCulture);
        DefeatsText.Text = summary.Defeats.ToString(CultureInfo.InvariantCulture);
        RecoveryText.Text = $"{summary.Retries} / {summary.Recoveries}";
        if (summary.DailyLimitReached && summary.WaitingUntilUtc is DateTimeOffset until)
        {
            DetectionText.Text = $"Daily limits reached · resumes {until:HH:mm} UTC";
        }
    }

    private void UpdateRuntime()
    {
        if (_runStarted is not null) RuntimeText.Text = (DateTimeOffset.Now - _runStarted.Value).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
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

    private string CurrentWebhook() => ShowWebhookCheck.IsChecked == true ? WebhookVisible.Text.Trim() : WebhookPassword.Password.Trim();

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
                StatusText.Text = "Macro failed. Running configured error diagnostics.";
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

        DispatchUi(() => AppendLogText(message));
    }

    private void AppendLogText(string message)
    {
        LogText.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        if (LogText.LineCount > 500)
        {
            int removeThrough = LogText.GetCharacterIndexFromLineIndex(LogText.LineCount - 500);
            LogText.Text = LogText.Text[removeThrough..];
            LogText.CaretIndex = LogText.Text.Length;
        }
        LogText.ScrollToEnd();
    }

    private void DispatchUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
        {
            _ = Dispatcher.BeginInvoke(action);
        }
    }

    private static string Label(object value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToString()!.Replace('_', ' '));

    private static int ParseInt(TextBox field, string label) =>
        int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new FormatException($"{label} must be a whole number.");
}
