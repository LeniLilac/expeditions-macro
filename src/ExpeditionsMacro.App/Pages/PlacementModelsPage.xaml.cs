using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage :
    UserControl,
    IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<PlacementSetupRow>
        _setupRows = [];
    private readonly ObservableCollection<PlacementSetupNode>
        _setupNodes = [];
    private readonly List<PlacementSetupNode>
        _setupRoots = [];
    private readonly ObservableCollection<PlacementStepRow>
        _steps = [];
    private PlacementModel? _selectedModel;
    private PlacementTarget? _selectedSetupTarget;
    private int _selectedFastUnit = 1;
    private PlacementPhase _selectedFastPhase =
        PlacementPhase.BeforeStart;
    private int _fastPlacementIntervalMilliseconds =
        PlacementAuthoringRules
            .DefaultStepDelayMilliseconds;
    private int _fastDefaultAfterStartDelayMilliseconds =
        PlacementAuthoringRules
            .DefaultAfterStartDelayMilliseconds;

    public PlacementModelsPage(AppServices services)
    {
        _services = services;
        _placementAutoSave =
            new PlacementModelAutoSaveSession(
                services.PlacementModels);
        InitializeComponent();
        InitializeCatalogRail();
        WireFastEditorEvents();
        FastSetupList.ItemsSource = _setupNodes;
        FastStepsList.ItemsSource = _steps;
        PlacementMarkers.ItemsSource = _steps;
        InitializeFastEditor();
        FastTeamCombo.ItemsSource = Enumerable
            .Range(0, 9)
            .Select(slot => new TeamChoice(
                slot,
                slot == 0
                    ? "Don't change"
                    : $"Team {slot}"))
            .ToArray();
        FastTeamCombo.SelectedIndex = 0;
        InitializePlacementAutoSave();
        _services.Coordinator.StateChanged +=
            (_, _) => Dispatcher.BeginInvoke(
                UpdateBusyState);
    }

    public Func<Task>? IdleHotkeyAction => null;

    private Selector ActiveStepsSelector =>
        FastStepsList;

    public async Task OnShownAsync()
    {
        await RefreshManualRecordingChoicesAsync();
        await RefreshModelsAsync(_selectedModel?.Id);
    }

    internal async Task RefreshModelsAsync(
        string? selectedId = null)
    {
        IReadOnlyList<PlacementModel> models =
            await _services.PlacementModels
                .ListAsync()
                .ConfigureAwait(false);
        await Dispatcher.InvokeAsync(
            () => RefreshSetupRows(
                models,
                selectedId));
    }

    private async void FastSetupList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_changingSetupSelection ||
            FastSetupList.SelectedItem is not
                PlacementSetupNode node)
        {
            return;
        }

        if (!await FlushPlacementAutoSaveAsync())
        {
            RestoreActiveSetupSelection();
            return;
        }
        ApplySelectedSetupNode(node);
    }

    private void ApplyModel(PlacementModel model)
    {
        using IDisposable suspension =
            SuspendPlacementAutoSave();
        _selectedModel = model;
        _fastPlacementIntervalMilliseconds =
            model.PlacementIntervalMilliseconds;
        _fastDefaultAfterStartDelayMilliseconds =
            model.DefaultAfterStartDelayMilliseconds;
        _fastImpossibilityThresholdMinutes =
            model.ImpossibilityThresholdMinutes;
        _fastManualRecordingId =
            model.ManualInputRecordingId;
        _steps.Clear();
        foreach (PlacementStep step in
                 PlacementAuthoringRules
                     .OrderForAuthoring(model.Steps))
        {
            _steps.Add(
                PlacementStepRow.FromModel(step));
        }

        if (model.CameraPreparationMode !=
                CameraPreparationMode.FastNoAlign ||
            model.Target is null)
        {
            throw new InvalidOperationException(
                "Legacy camera-model placements are no longer supported. Configure this route in Placement Setup.");
        }

        ApplyFastTarget(model.Target);
        FastTeamCombo.SelectedItem =
            FastTeamCombo.Items
                .Cast<TeamChoice>()
                .First(choice =>
                    choice.Value == model.TeamSlot);
        UpdateFastManualRecordingEditor();
        FastStatusText.Text = string.Empty;
        FastDetailText.Text =
            $"{model.Steps.Count} placements";
        UpdateFastPlacementCount();
    }

    private void ApplyNewModelDefaults()
    {
        using IDisposable suspension =
            SuspendPlacementAutoSave();
        ResetFastTimingDefaults();
        ResetFastRecordingSettings();
        FastBeforeStartButton.IsChecked = true;
        FastUnit1Button.IsChecked = true;
        if (_selectedSetupTarget is not null)
        {
            ApplyFastTarget(_selectedSetupTarget);
        }
        else
        {
            ApplyDefaultFastTarget();
        }
        FastTeamCombo.SelectedIndex = 0;
        FastStatusText.Text = string.Empty;
        FastDetailText.Text = string.Empty;
        UpdateFastPlacementCount();
    }

    private void Stop_Click(
        object sender,
        System.Windows.RoutedEventArgs e) =>
        _services.Coordinator.Cancel();

    private void SetStatus(string message) =>
        FastStatusText.Text = message;

    private void SetOperationProgress(double value) =>
        FastOperationProgress.Value = value;
}
