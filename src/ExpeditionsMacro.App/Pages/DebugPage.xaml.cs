using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class DebugPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<DebugCheckpointRow> _timeline = [];
    private IReadOnlyList<ExpeditionPreset> _expeditions = [];
    private IReadOnlyList<ChallengePreset> _challenges = [];
    private IReadOnlyList<StoryPreset> _stories = [];
    private IReadOnlyList<RaidPreset> _raids = [];
    private bool _followLive = true;

    public DebugPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        TimelineList.ItemsSource = _timeline;
        NavigationStartCombo.ItemsSource = new[]
        {
            new DebugOption<DebugNavigationStart>(
                DebugNavigationStart.Lobby,
                "Lobby (Play closed)"),
            new DebugOption<DebugNavigationStart>(
                DebugNavigationStart.PostMatch,
                "Post-match screen"),
        };
        NavigationModeCombo.ItemsSource = new[]
        {
            new DebugOption<DebugNavigationMode>(
                DebugNavigationMode.Expedition,
                "Expedition"),
            new DebugOption<DebugNavigationMode>(
                DebugNavigationMode.Challenge,
                "Challenge"),
            new DebugOption<DebugNavigationMode>(
                DebugNavigationMode.Story,
                "Story"),
            new DebugOption<DebugNavigationMode>(
                DebugNavigationMode.Raid,
                "Raid"),
        };
        ChallengeTypeCombo.ItemsSource =
            Enum.GetValues<ChallengeType>();
        TeamCombo.ItemsSource = Enumerable
            .Range(1, 8)
            .Select(value => new DebugOption<int>(
                value,
                $"Team {value}"))
            .ToArray();
        StepModeCombo.ItemsSource = new[]
        {
            new DebugOption<DebugStepMode>(
                DebugStepMode.Continuous,
                "Run continuously"),
            new DebugOption<DebugStepMode>(
                DebugStepMode.BeforeActions,
                "Pause before actions"),
            new DebugOption<DebugStepMode>(
                DebugStepMode.EveryDetectionAndAction,
                "Pause after every detection"),
        };
        NavigationStartCombo.SelectedIndex = 0;
        NavigationModeCombo.SelectedIndex = 0;
        ChallengeTypeCombo.SelectedItem = ChallengeType.Trait;
        TeamCombo.SelectedIndex = 0;
        StepModeCombo.SelectedIndex = 0;
        _services.DebugCheckpoints.CheckpointAdded +=
            DebugCheckpoint_Added;
        _services.DebugCheckpoints.StateChanged +=
            DebugCheckpoint_StateChanged;
        _services.Coordinator.StateChanged +=
            Coordinator_StateChanged;
    }

    public Func<Task>? IdleHotkeyAction => null;

    public async Task OnShownAsync()
    {
        await RefreshPresetsAsync();
        UpdateControls();
    }

    internal void SetSnapshotState()
    {
        if (_timeline.Count > 0) return;
        AddCheckpoint(new DebugCheckpoint(
            1,
            DateTimeOffset.UtcNow,
            DebugCheckpointKind.Detection,
            "stage_screen: GameModeSelector",
            "Detected at 96.4% confidence.",
            "GameModeSelector",
            0.964,
            null));
        AddCheckpoint(new DebugCheckpoint(
            2,
            DateTimeOffset.UtcNow,
            DebugCheckpointKind.Action,
            "Click Roblox",
            "Click the selected Story tile.",
            null,
            null,
            null));
    }

    private async Task RefreshPresetsAsync()
    {
        _expeditions = await _services.Presets.ListAsync();
        _challenges =
            await _services.ChallengePresets.ListAsync();
        _stories = await _services.StoryPresets.ListAsync();
        _raids = await _services.RaidPresets.ListAsync();
        RefreshNavigationPresets();
    }

    private void NavigationMode_Changed(
        object sender,
        SelectionChangedEventArgs e) =>
        RefreshNavigationPresets();

    private void RefreshNavigationPresets()
    {
        if (!IsInitialized ||
            NavigationModeCombo.SelectedItem is not
                DebugOption<DebugNavigationMode> selected)
        {
            return;
        }
        IEnumerable<object> presets = selected.Value switch
        {
            DebugNavigationMode.Expedition =>
                _expeditions.Cast<object>(),
            DebugNavigationMode.Challenge =>
                _challenges.Cast<object>(),
            DebugNavigationMode.Story =>
                _stories.Cast<object>(),
            DebugNavigationMode.Raid =>
                _raids.Cast<object>(),
            _ => Enumerable.Empty<object>(),
        };
        NavigationPresetCombo.ItemsSource = presets;
        NavigationPresetCombo.SelectedIndex =
            NavigationPresetCombo.Items.Count > 0 ? 0 : -1;
        ChallengeTypePanel.Visibility =
            selected.Value == DebugNavigationMode.Challenge
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private sealed record DebugCheckpointRow(
        long Sequence,
        string Title,
        string Detail,
        string? State,
        double? Confidence,
        ImageSource? Frame);

    private sealed record DebugOption<T>(
        T Value,
        string Label);

    private sealed record DebugNavigationRequest(
        DebugNavigationStart Start,
        DebugNavigationMode Mode,
        object Preset,
        ChallengeType ChallengeType);

    private enum DebugNavigationStart
    {
        Lobby,
        PostMatch,
    }

    private enum DebugNavigationMode
    {
        Expedition,
        Challenge,
        Story,
        Raid,
    }
}
