using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class RaidPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<RaidPreset> _presets = [];
    private readonly ObservableCollection<CameraModelManifest> _cameras = [];
    private readonly ObservableCollection<CatalogOption> _placements = [];
    private bool _loading;

    public RaidPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        PresetCombo.ItemsSource = _presets;
        CameraCombo.ItemsSource = _cameras;
        PrestartCombo.ItemsSource = _placements;
        DelayedCombo.ItemsSource = _placements;
        ActCombo.ItemsSource = new[] { new NamedChoice<RaidAct>(RaidAct.Act1, "Act 1"), new NamedChoice<RaidAct>(RaidAct.Act2, "Act 2"), new NamedChoice<RaidAct>(RaidAct.Act3, "Act 3") };
        TeamCombo.ItemsSource = TeamChoices();
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdatePresetActions);
    }

    public Func<Task>? IdleHotkeyAction => null;

    public async Task OnShownAsync()
    {
        _loading = true;
        await RefreshCatalogsAsync();
        await RefreshPresetsAsync();
        PresetCombo.SelectedItem = _presets.FirstOrDefault(value => value.Id == _services.Settings.SelectedRaidPresetId) ?? _presets.FirstOrDefault();
        if (PresetCombo.SelectedItem is RaidPreset preset) Apply(preset); else ApplyNew();
        _loading = false;
        UpdatePresetActions();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RaidPreset preset = Build();
            await _services.RaidPresets.SaveAsync(preset);
            await _services.UpdateSettingsAsync(settings => settings with { SelectedRaidPresetId = preset.Id });
            await RefreshPresetsAsync();
            PresetCombo.SelectedItem = _presets.First(value => value.Id == preset.Id);
            StatusText.Text = $"Preset '{preset.Name}' saved locally.";
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }

    private void New_Click(object sender, RoutedEventArgs e) { PresetCombo.SelectedItem = null; ApplyNew(); UpdatePresetActions(); }

    private async void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePresetActions();
        if (_loading || PresetCombo.SelectedItem is not RaidPreset preset) return;
        Apply(preset);
        await _services.UpdateSettingsAsync(settings => settings with { SelectedRaidPresetId = preset.Id });
    }

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy || PresetCombo.SelectedItem is not RaidPreset preset) return;
        MessageBoxResult confirmation = MessageBox.Show(
            Window.GetWindow(this),
            $"Delete Raid preset '{preset.Name}'?\n\nCamera and placement models are kept.",
            "Delete preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            await _services.PresetDeletion.DeleteAsync(MacroTaskKind.Raid, preset.Id);
            _loading = true;
            await RefreshPresetsAsync();
            RaidPreset? replacement = _presets.FirstOrDefault();
            PresetCombo.SelectedItem = replacement;
            if (replacement is not null) Apply(replacement);
            else ApplyNew();
            await _services.UpdateSettingsAsync(settings => settings with { SelectedRaidPresetId = replacement?.Id ?? string.Empty });
            StatusText.Text = $"Preset '{preset.Name}' deleted.";
        }
        catch (PresetInUseException error)
        {
            MessageBox.Show(Window.GetWindow(this), error.Message, "Preset in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = error.Message;
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
        finally
        {
            _loading = false;
            UpdatePresetActions();
        }
    }

    private void TuningToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = TuningPanel.Visibility != Visibility.Visible;
        TuningPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TuningToggle.Content = show ? "Hide tuning" : "Show tuning";
    }

    private RaidPreset Build()
    {
        if (ActCombo.SelectedItem is not NamedChoice<RaidAct> act) throw new InvalidOperationException("Choose a Raid act.");
        if (CameraCombo.SelectedItem is not CameraModelManifest camera) throw new InvalidOperationException("Choose a camera model.");
        if (TeamCombo.SelectedItem is not TeamChoice team) throw new InvalidOperationException("Choose a team setting.");
        string name = NameText.Text.Trim();
        RaidPreset preset = new()
        {
            Id = ModelId.FromName(name),
            Name = name,
            Act = act.Value,
            CameraModelId = camera.Id,
            PrestartPlacementModelId = SelectedPlacement(PrestartCombo),
            DelayedPlacementModelId = SelectedPlacement(DelayedCombo),
            DelayedPlacementSeconds = ParseInt(DelayText, "After-start delay"),
            TeamSlot = team.Value,
            DefeatRetries = ParseInt(RetriesText, "Defeat retries"),
            AutoRecover = AutoRecoverCheck.IsChecked == true,
            ZoomTicks = ParseInt(ZoomText, "Zoom ticks"),
            PitchDragPixels = ParseInt(PitchText, "Pitch drag"),
            PollMilliseconds = ParseInt(PollText, "Detection interval"),
            StableDetections = ParseInt(StableText, "Stable frames"),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        preset.Validate();
        return preset;
    }

    private void Apply(RaidPreset preset)
    {
        NameText.Text = preset.Name;
        ActCombo.SelectedItem = ActCombo.Items.Cast<NamedChoice<RaidAct>>().First(value => value.Value == preset.Act);
        CameraCombo.SelectedItem = _cameras.FirstOrDefault(value => value.Id == preset.CameraModelId);
        SelectPlacement(PrestartCombo, preset.PrestartPlacementModelId);
        SelectPlacement(DelayedCombo, preset.DelayedPlacementModelId);
        DelayText.Text = preset.DelayedPlacementSeconds.ToString(CultureInfo.InvariantCulture);
        TeamCombo.SelectedItem = TeamChoices().First(value => value.Value == preset.TeamSlot);
        RetriesText.Text = preset.DefeatRetries.ToString(CultureInfo.InvariantCulture);
        AutoRecoverCheck.IsChecked = preset.AutoRecover;
        ZoomText.Text = preset.ZoomTicks.ToString(CultureInfo.InvariantCulture);
        PitchText.Text = preset.PitchDragPixels.ToString(CultureInfo.InvariantCulture);
        PollText.Text = preset.PollMilliseconds.ToString(CultureInfo.InvariantCulture);
        StableText.Text = preset.StableDetections.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyNew()
    {
        NameText.Text = "Raid route"; ActCombo.SelectedIndex = 0; CameraCombo.SelectedIndex = _cameras.Count > 0 ? 0 : -1;
        PrestartCombo.SelectedIndex = 0; DelayedCombo.SelectedIndex = 0; DelayText.Text = "30"; TeamCombo.SelectedIndex = 0;
        RetriesText.Text = "0"; AutoRecoverCheck.IsChecked = true; ZoomText.Text = "30"; PitchText.Text = "1800"; PollText.Text = "450"; StableText.Text = "2";
    }

    private async Task RefreshCatalogsAsync()
    {
        _cameras.Clear(); foreach (CameraModelManifest camera in await _services.CameraModels.ListAsync()) _cameras.Add(camera);
        _placements.Clear(); _placements.Add(new CatalogOption(string.Empty, "Don't place"));
        foreach (PlacementModel placement in await _services.PlacementModels.ListAsync()) _placements.Add(new CatalogOption(placement.Id, placement.Name));
    }

    private async Task RefreshPresetsAsync()
    {
        string? selected = (PresetCombo.SelectedItem as RaidPreset)?.Id; _presets.Clear();
        foreach (RaidPreset preset in await _services.RaidPresets.ListAsync()) _presets.Add(preset);
        PresetCombo.SelectedItem = _presets.FirstOrDefault(value => value.Id == selected);
    }

    private static string SelectedPlacement(ComboBox combo) => (combo.SelectedItem as CatalogOption)?.Id ?? string.Empty;
    private void SelectPlacement(ComboBox combo, string id) => combo.SelectedItem = _placements.FirstOrDefault(value => value.Id == id) ?? _placements[0];
    private static int ParseInt(TextBox box, string label) => int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : throw new InvalidDataException($"{label} must be a whole number.");
    private static IReadOnlyList<TeamChoice> TeamChoices() => [new(0, "Don't change"), .. Enumerable.Range(1, 8).Select(value => new TeamChoice(value, $"Team {value}"))];

    private void UpdatePresetActions()
    {
        bool enabled = !_services.Coordinator.IsBusy;
        SavePresetButton.IsEnabled = enabled;
        NewPresetButton.IsEnabled = enabled;
        DeletePresetButton.IsEnabled = enabled && PresetCombo.SelectedItem is RaidPreset;
    }
}
