using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class ChallengesPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<ChallengePreset> _presets = [];
    private readonly ObservableCollection<ExpeditionPreset> _expeditionPresets = [];
    private readonly ObservableCollection<DetectorPackManifest> _detectorPacks = [];
    private readonly DispatcherTimer _resetTimer;
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
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(CoordinatorStateChanged);
        UpdateResetCountdown();
    }

    public ObservableCollection<ChallengeMapRow> MapRows { get; }

    public ObservableCollection<CatalogOption> CameraOptions { get; } = [];

    public ObservableCollection<CatalogOption> PlacementOptions { get; } = [];

    public Func<Task>? IdleF6Action => null;

    public async Task OnShownAsync()
    {
        _loading = true;
        await RefreshCatalogsAsync();
        await RefreshPresetsAsync();
        string selectedId = _services.Settings.SelectedChallengePresetId;
        PresetCombo.SelectedItem = _presets.FirstOrDefault(preset => preset.Id == selectedId) ?? _presets.FirstOrDefault();
        if (PresetCombo.SelectedItem is ChallengePreset preset) ApplyPreset(preset);
        else ApplyNewPreset();
        _loading = false;
        CoordinatorStateChanged();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _services.Coordinator.Cancel();

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ChallengePreset preset = await SavePresetInternalAsync();
            StatusText.Text = $"Preset '{preset.Name}' saved locally. Map detector calibration is still required.";
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
            StatusText.Text = "All five map model profiles are complete. Detector calibration is the remaining blocker.";
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
        await _services.ChallengePresets.SaveAsync(preset);
        await _services.UpdateSettingsAsync(settings => settings with { SelectedChallengePresetId = preset.Id });
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
        DetectorCombo.SelectedItem = _detectorPacks.FirstOrDefault();
        ZoomTicksText.Text = "30";
        PitchPixelsText.Text = "1800";
        PollText.Text = "450";
        StableText.Text = "2";
        KeyHoldText.Text = "110";
        KeyDelayText.Text = "250";
        foreach (ChallengeMapRow row in MapRows) row.Apply(new ChallengeMapProfile { Map = row.Map });
        UpdateIdleBehaviorAvailability();
        StatusText.Text = "Configuration can be saved. Automation is waiting for map detector data.";
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
        DetectorCombo.IsEnabled = enabled;
        ZoomTicksText.IsEnabled = enabled;
        PitchPixelsText.IsEnabled = enabled;
        PollText.IsEnabled = enabled;
        StableText.IsEnabled = enabled;
        KeyHoldText.IsEnabled = enabled;
        KeyDelayText.IsEnabled = enabled;
        MapItems.IsEnabled = enabled;
        UpdateIdleBehaviorAvailability();
    }

    private void UpdateResetCountdown()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset next = ChallengeRunPolicy.NextGlobalReset(now);
        TimeSpan remaining = next - now;
        NextResetText.Text = $"Next reset in {(int)remaining.TotalMinutes}m {remaining.Seconds:00}s";
    }

    private static int ParseInt(TextBox field, string label) =>
        int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new FormatException($"{label} must be a whole number.");
}
