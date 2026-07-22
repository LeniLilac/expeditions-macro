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

public partial class StoryPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<StoryPreset> _presets = [];
    private readonly ObservableCollection<CameraModelManifest> _cameras = [];
    private readonly ObservableCollection<CatalogOption> _placements = [];
    private bool _loading;

    public StoryPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        PresetCombo.ItemsSource = _presets;
        CameraCombo.ItemsSource = _cameras;
        PrestartCombo.ItemsSource = _placements;
        DelayedCombo.ItemsSource = _placements;
        MapCombo.ItemsSource = MapChoices();
        RunKindCombo.ItemsSource = RunChoices();
        ActCombo.ItemsSource = Enumerable.Range(1, 5).ToArray();
        TeamCombo.ItemsSource = TeamChoices();
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdatePresetActions);
    }

    public Func<Task>? IdleHotkeyAction => null;

    public async Task OnShownAsync()
    {
        _loading = true;
        await RefreshCatalogsAsync();
        await RefreshPresetsAsync();
        PresetCombo.SelectedItem = _presets.FirstOrDefault(value => value.Id == _services.Settings.SelectedStoryPresetId) ?? _presets.FirstOrDefault();
        if (PresetCombo.SelectedItem is StoryPreset preset) Apply(preset); else ApplyNew();
        _loading = false;
        UpdatePresetActions();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StoryPreset preset = Build();
            await _services.StoryPresets.SaveAsync(preset);
            await _services.UpdateSettingsAsync(settings => settings with { SelectedStoryPresetId = preset.Id });
            await RefreshPresetsAsync();
            PresetCombo.SelectedItem = _presets.First(value => value.Id == preset.Id);
            StatusText.Text = $"Preset '{preset.Name}' saved locally.";
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        PresetCombo.SelectedItem = null;
        ApplyNew();
        UpdatePresetActions();
    }

    private async void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePresetActions();
        if (_loading || PresetCombo.SelectedItem is not StoryPreset preset) return;
        Apply(preset);
        await _services.UpdateSettingsAsync(settings => settings with { SelectedStoryPresetId = preset.Id });
    }

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_services.Coordinator.IsBusy || PresetCombo.SelectedItem is not StoryPreset preset) return;
        MessageBoxResult confirmation = MessageBox.Show(
            Window.GetWindow(this),
            $"Delete Story preset '{preset.Name}'?\n\nCamera and placement models are kept.",
            "Delete preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            await _services.PresetDeletion.DeleteAsync(MacroTaskKind.Story, preset.Id);
            _loading = true;
            await RefreshPresetsAsync();
            StoryPreset? replacement = _presets.FirstOrDefault();
            PresetCombo.SelectedItem = replacement;
            if (replacement is not null) Apply(replacement);
            else ApplyNew();
            await _services.UpdateSettingsAsync(settings => settings with { SelectedStoryPresetId = replacement?.Id ?? string.Empty });
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

    private void RunKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ActCombo is null || HardCheck is null) return;
        bool act = SelectedRunKind() == StoryRunKind.Act;
        ActCombo.IsEnabled = act;
        HardCheck.IsEnabled = act;
    }

    private void TuningToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = TuningPanel.Visibility != Visibility.Visible;
        TuningPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TuningToggle.Content = show ? "Hide tuning" : "Show tuning";
    }

    private StoryPreset Build()
    {
        if (MapCombo.SelectedItem is not NamedChoice<ChallengeMapId> map) throw new InvalidOperationException("Choose a Story map.");
        if (RunKindCombo.SelectedItem is not NamedChoice<StoryRunKind> run) throw new InvalidOperationException("Choose a Story run type.");
        if (CameraCombo.SelectedItem is not CameraModelManifest camera) throw new InvalidOperationException("Choose a camera model.");
        if (TeamCombo.SelectedItem is not TeamChoice team) throw new InvalidOperationException("Choose a team setting.");
        string name = NameText.Text.Trim();
        StoryPreset preset = new()
        {
            Id = ModelId.FromName(name),
            Name = name,
            Map = map.Value,
            RunKind = run.Value,
            ActNumber = ActCombo.SelectedItem is int act ? act : 1,
            HardMode = run.Value == StoryRunKind.Act && HardCheck.IsChecked == true,
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

    private void Apply(StoryPreset preset)
    {
        NameText.Text = preset.Name;
        SelectChoice(MapCombo, preset.Map);
        SelectChoice(RunKindCombo, preset.RunKind);
        ActCombo.SelectedItem = preset.ActNumber;
        HardCheck.IsChecked = preset.HardMode;
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
        RunKind_Changed(this, new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, new List<object>(), new List<object>()));
    }

    private void ApplyNew()
    {
        NameText.Text = "Story route";
        MapCombo.SelectedIndex = 0;
        RunKindCombo.SelectedIndex = 0;
        ActCombo.SelectedItem = 1;
        HardCheck.IsChecked = false;
        CameraCombo.SelectedIndex = _cameras.Count > 0 ? 0 : -1;
        PrestartCombo.SelectedIndex = 0;
        DelayedCombo.SelectedIndex = 0;
        DelayText.Text = "30";
        TeamCombo.SelectedIndex = 0;
        RetriesText.Text = "0";
        AutoRecoverCheck.IsChecked = true;
        ZoomText.Text = "30";
        PitchText.Text = "1800";
        PollText.Text = "450";
        StableText.Text = "2";
        RunKind_Changed(this, new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, new List<object>(), new List<object>()));
    }

    private async Task RefreshCatalogsAsync()
    {
        _cameras.Clear();
        foreach (CameraModelManifest camera in await _services.CameraModels.ListAsync()) _cameras.Add(camera);
        _placements.Clear();
        _placements.Add(new CatalogOption(string.Empty, "Don't place"));
        foreach (PlacementModel placement in await _services.PlacementModels.ListAsync()) _placements.Add(new CatalogOption(placement.Id, placement.Name));
    }

    private async Task RefreshPresetsAsync()
    {
        string? selected = (PresetCombo.SelectedItem as StoryPreset)?.Id;
        _presets.Clear();
        foreach (StoryPreset preset in await _services.StoryPresets.ListAsync()) _presets.Add(preset);
        PresetCombo.SelectedItem = _presets.FirstOrDefault(value => value.Id == selected);
    }

    private StoryRunKind SelectedRunKind() => (RunKindCombo.SelectedItem as NamedChoice<StoryRunKind>)?.Value ?? StoryRunKind.Act;
    private static string SelectedPlacement(ComboBox combo) => (combo.SelectedItem as CatalogOption)?.Id ?? string.Empty;
    private void SelectPlacement(ComboBox combo, string id) => combo.SelectedItem = _placements.FirstOrDefault(value => value.Id == id) ?? _placements[0];
    private static void SelectChoice<T>(ComboBox combo, T value) => combo.SelectedItem = combo.Items.Cast<NamedChoice<T>>().First(item => EqualityComparer<T>.Default.Equals(item.Value, value));
    private static int ParseInt(TextBox box, string label) => int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : throw new InvalidDataException($"{label} must be a whole number.");
    private static IReadOnlyList<TeamChoice> TeamChoices() => [new(0, "Don't change"), .. Enumerable.Range(1, 8).Select(value => new TeamChoice(value, $"Team {value}"))];
    private static IReadOnlyList<NamedChoice<ChallengeMapId>> MapChoices() => [new(ChallengeMapId.SchoolGrounds, "School Grounds"), new(ChallengeMapId.FlowerForest, "Flower Forest"), new(ChallengeMapId.RoseKingdom, "Rose Kingdom"), new(ChallengeMapId.FairyKingForest, "Fairy King Forest"), new(ChallengeMapId.KingsTomb, "King's Tomb")];
    private static IReadOnlyList<NamedChoice<StoryRunKind>> RunChoices() => [new(StoryRunKind.Act, "Act"), new(StoryRunKind.Infinite, "Infinite"), new(StoryRunKind.Mastery, "Mastery")];

    private void UpdatePresetActions()
    {
        bool enabled = !_services.Coordinator.IsBusy;
        SavePresetButton.IsEnabled = enabled;
        NewPresetButton.IsEnabled = enabled;
        DeletePresetButton.IsEnabled = enabled && PresetCombo.SelectedItem is StoryPreset;
    }
}
