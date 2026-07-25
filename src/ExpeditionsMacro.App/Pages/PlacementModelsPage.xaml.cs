using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<PlacementModel> _models = [];
    private readonly ObservableCollection<PlacementSetupRow>
        _setupRows = [];
    private readonly ObservableCollection<PlacementSetupNode>
        _setupNodes = [];
    private readonly List<PlacementSetupNode>
        _setupRoots = [];
    private readonly ObservableCollection<PlacementStepRow> _steps = [];
    private PlacementModel? _selectedModel;
    private PlacementTarget? _selectedSetupTarget;
    private int _selectedFastUnit = 1;
    private PlacementPhase _selectedFastPhase =
        PlacementPhase.BeforeStart;

    public PlacementModelsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        WireFastEditorEvents();
        ModelsList.ItemsSource = _models;
        FastSetupList.ItemsSource = _setupNodes;
        StepsGrid.ItemsSource = _steps;
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
        _steps.CollectionChanged += (_, _) =>
            UpdateFastPlacementCount();
        _services.Coordinator.StateChanged +=
            (_, _) => Dispatcher.BeginInvoke(UpdateBusyState);
        _services.Hotkey.BindingChanged +=
            (_, _) => Dispatcher.BeginInvoke(UpdateHotkeyText);
        _services.SettingsChanged +=
            (_, _) => Dispatcher.BeginInvoke(
                ApplyWorkflowMode);
    }

    public Func<Task>? IdleHotkeyAction => null;

    private bool FastWorkflow =>
        _services.Settings.FastNoAlignEnabled;

    private Selector ActiveStepsSelector =>
        FastWorkflow ? FastStepsList : StepsGrid;

    public async Task OnShownAsync()
    {
        UpdateHotkeyText();
        ApplyWorkflowMode();
        await RefreshModelsAsync(_selectedModel?.Id);
    }

    internal async Task RefreshModelsAsync(
        string? selectedId = null)
    {
        IReadOnlyList<PlacementModel> models =
            await _services.PlacementModels
                .ListAsync()
                .ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            _models.Clear();
            foreach (PlacementModel model in models)
            {
                _models.Add(model);
            }
            if (FastWorkflow)
            {
                RefreshSetupRows(models, selectedId);
                return;
            }
            if (selectedId is not null)
            {
                ModelsList.SelectedItem =
                    _models.FirstOrDefault(
                        model => model.Id == selectedId);
            }
        });
    }

    private void ModelsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (ModelsList.SelectedItem is PlacementModel model)
        {
            ApplyModel(model);
        }
    }

    private void FastSetupList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!FastWorkflow ||
            FastSetupList.SelectedItem is not
                PlacementSetupNode node)
        {
            return;
        }

        ApplySetup(node.Row);
    }

    private void ApplyModel(PlacementModel model)
    {
        _selectedModel = model;
        _steps.Clear();
        foreach (PlacementStep step in model.Steps)
        {
            _steps.Add(PlacementStepRow.FromModel(step));
        }

        if (FastWorkflow)
        {
            FastDefaultDelayText.Text =
                (model.Steps.FirstOrDefault()?
                    .DelayAfterMilliseconds ?? 900)
                .ToString(CultureInfo.InvariantCulture);
            if (model.CameraPreparationMode ==
                CameraPreparationMode.FastNoAlign &&
                model.Target is not null)
            {
                ApplyFastTarget(model.Target);
                FastTeamCombo.SelectedItem =
                    FastTeamCombo.Items
                        .Cast<TeamChoice>()
                        .First(choice =>
                            choice.Value ==
                            model.TeamSlot);
                FastStatusText.Text = string.Empty;
                FastDetailText.Text =
                    $"{model.Steps.Count} placements · Fast no align";
            }
            else
            {
                ApplyDefaultFastTarget();
                FastStatusText.Text =
                    "This is a legacy camera-model placement.";
                FastDetailText.Text =
                    "Disable Fast no align in Settings to edit or use it.";
            }
        }
        else
        {
            ModelNameText.Text = model.Name;
            StatusText.Text = model.CameraPreparationMode ==
                CameraPreparationMode.CameraModel
                ? $"Loaded {model.Name}."
                : "This Fast no align model cannot be used by camera-model presets.";
            DetailText.Text =
                $"{model.Steps.Count} placements · Roblox client {model.ClientWidth} × {model.ClientHeight}";
        }
        UpdateFastPlacementCount();
    }

    private void NewModel_Click(
        object sender,
        RoutedEventArgs e)
    {
        ModelsList.SelectedItem = null;
        _selectedModel = null;
        _steps.Clear();
        ApplyNewModelDefaults();
    }

    private void ApplyNewModelDefaults()
    {
        if (FastWorkflow)
        {
            FastDefaultDelayText.Text = "900";
            FastAfterStartDelayText.Text = "0";
            FastBeforeStartButton.IsChecked = true;
            FastUnit1Button.IsChecked = true;
            if (_selectedSetupTarget is not null)
            {
                ApplyFastTarget(
                    _selectedSetupTarget);
            }
            else
            {
                ApplyDefaultFastTarget();
            }
            FastTeamCombo.SelectedIndex = 0;
            FastStatusText.Text = string.Empty;
            FastDetailText.Text = string.Empty;
        }
        else
        {
            ModelNameText.Text = "Placement model";
            StatusText.Text = "Ready to record a new model.";
            DetailText.Text =
                $"Click Record placements, focus Roblox, then press {_services.Hotkey.DisplayName}.";
        }
        UpdateFastPlacementCount();
    }

    private async void Save_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            PlacementModel model = BuildModel();
            await _services.PlacementModels.SaveAsync(model);
            _selectedModel = model;
            _selectedSetupTarget = model.Target;
            await RefreshModelsAsync(model.Id);
            SetStatus(
                FastWorkflow
                    ? "Placement setup saved."
                    : "Placement model saved.");
        }
        catch (Exception error)
        {
            SetStatus(error.Message);
        }
    }

    private void Record_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (FastWorkflow) return;
        string name = ModelNameText.Text.Trim();
        if (name.Length == 0)
        {
            StatusText.Text = "Enter a model name.";
            return;
        }
        if (!TryReadDelay(
            DefaultDelayText,
            out int delay))
        {
            StatusText.Text =
                "Default delay must be a non-negative whole number.";
            return;
        }

        bool useRecordedTiming =
            RecordedTimingCheck.IsChecked == true;
        _steps.Clear();
        _services.Coordinator.Arm(
            "Placement recording",
            async token =>
            {
                PlacementModel model =
                    await _services.Placement.RecordAsync(
                        name,
                        delay,
                        useRecordedTiming,
                        captured: capture =>
                        {
                            _services.DeepDebug.RecordEvent(
                                "placement",
                                "capture_recorded",
                                capture);
                            Dispatcher.BeginInvoke(() =>
                            {
                                _steps.Add(new PlacementStepRow
                                {
                                    UnitKey = capture.UnitKey,
                                    X = capture.X,
                                    Y = capture.Y,
                                    DelayAfterMilliseconds = delay,
                                });
                                OperationProgress.Value =
                                    Math.Min(95, _steps.Count * 10);
                            });
                        },
                        status: message =>
                        {
                            _services.DeepDebug.RecordEvent(
                                "placement",
                                "recording_status",
                                new { Message = message });
                            Dispatcher.BeginInvoke(() =>
                            {
                                StatusText.Text = message;
                                if (message.Contains(
                                    "Recording",
                                    StringComparison.OrdinalIgnoreCase))
                                {
                                    DetailText.Text =
                                        $"Press {_services.Hotkey.DisplayName} again to finish and save.";
                                }
                            });
                        },
                        cancellationToken: token);
                await Dispatcher.InvokeAsync(() =>
                {
                    ApplyModel(model);
                    OperationProgress.Value = 100;
                });
                await RefreshModelsAsync(model.Id);
            },
            new DeepDebugOperationContext
            {
                PlacementModelIds = [ModelId.FromName(name)],
                OperationSettings = new
                {
                    Name = name,
                    DefaultDelayMilliseconds = delay,
                    UseRecordedTiming = useRecordedTiming,
                    CameraPreparationMode =
                        CameraPreparationMode.CameraModel,
                },
                RefreshReferencedModelsAfterOperation = true,
            });
        StatusText.Text = "Placement recording armed.";
        DetailText.Text =
            $"Focus Roblox and press {_services.Hotkey.DisplayName} to begin. Press it again after the final placement.";
        UpdateBusyState();
    }

    private async void Test_Click(
        object sender,
        RoutedEventArgs e)
    {
        PlacementModel model;
        try
        {
            model = BuildModel();
            await _services.PlacementModels.SaveAsync(model);
            _selectedModel = model;
        }
        catch (Exception error)
        {
            SetStatus(error.Message);
            return;
        }

        int delay = ReadPlaybackDelay();
        bool fast = FastWorkflow;
        bool overrideTiming =
            fast ||
            PlaybackOverrideCheck.IsChecked == true;
        _services.Coordinator.Arm(
            "Placement playback",
            async token =>
            {
                if (fast)
                {
                    await _services.CameraPose
                        .PrepareWithoutYawAsync(
                            progress: new Progress<MacroProgress>(
                                value => Dispatcher.BeginInvoke(
                                    () => FastStatusText.Text =
                                        value.Message)),
                            cancellationToken: token);
                }
                await _services.Placement.PlayAsync(
                    model,
                    overrideTiming,
                    delay,
                    stepSent: (index, total, step) =>
                    {
                        _services.DeepDebug.RecordEvent(
                            "placement",
                            "playback_step",
                            new
                            {
                                Index = index,
                                Total = total,
                                Step = step,
                            });
                        Dispatcher.BeginInvoke(() =>
                            SetOperationProgress(
                                100d * index / total));
                    },
                    status: message =>
                    {
                        _services.DeepDebug.RecordEvent(
                            "placement",
                            "playback_status",
                            new { Message = message });
                        Dispatcher.BeginInvoke(() =>
                            SetStatus(message));
                    },
                    cancellationToken: token);
            },
            new DeepDebugOperationContext
            {
                PlacementModelIds = [model.Id],
                OperationSettings = new
                {
                    Model = model.Id,
                    UseDefaultInterval = overrideTiming,
                    DefaultDelayMilliseconds = delay,
                    FastNoAlign = fast,
                },
            });
        SetStatus(
            $"Playback armed. Focus Roblox and press {_services.Hotkey.DisplayName} to begin.");
        UpdateBusyState();
    }

    private void Stop_Click(
        object sender,
        RoutedEventArgs e) =>
        _services.Coordinator.Cancel();

    private void UpdateHotkeyText()
    {
        string hotkey = _services.Hotkey.DisplayName;
        RecordingDescription.Text =
            $"Recording starts after {hotkey} and ends when {hotkey} is pressed again.";
    }

    private void SetStatus(string message)
    {
        if (FastWorkflow) FastStatusText.Text = message;
        else StatusText.Text = message;
    }

    private void SetOperationProgress(double value)
    {
        if (FastWorkflow) FastOperationProgress.Value = value;
        else OperationProgress.Value = value;
    }

    private void UpdateBusyState()
    {
        bool busy = _services.Coordinator.IsBusy;
        SaveButton.IsEnabled = !busy;
        RecordButton.IsEnabled = !busy;
        TestButton.IsEnabled = !busy;
        StopButton.IsEnabled = busy;
        FastSaveButton.IsEnabled = !busy;
        FastPrepareButton.IsEnabled = !busy;
        FastTestButton.IsEnabled = !busy;
        FastStopButton.IsEnabled = busy;
        TargetModeCombo.IsEnabled = !busy;
        TargetMapCombo.IsEnabled = !busy;
        TargetVariantCombo.IsEnabled = !busy;
        FastTeamCombo.IsEnabled = !busy;
        FastUnit1Button.IsEnabled = !busy;
        FastUnit2Button.IsEnabled = !busy;
        FastUnit3Button.IsEnabled = !busy;
        FastUnit4Button.IsEnabled = !busy;
        FastUnit5Button.IsEnabled = !busy;
        FastUnit6Button.IsEnabled = !busy;
        FastBeforeStartButton.IsEnabled = !busy;
        FastAfterStartButton.IsEnabled = !busy;
        FastDefaultDelayText.IsEnabled = !busy;
        FastAfterStartDelayText.IsEnabled =
            !busy &&
            _selectedFastPhase ==
                PlacementPhase.AfterStart;
        PlacementCanvas.IsEnabled = !busy;
    }
}
