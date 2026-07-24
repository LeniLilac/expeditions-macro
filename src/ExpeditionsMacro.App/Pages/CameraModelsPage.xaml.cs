using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.App.Windows;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class CameraModelsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<CameraModelManifest> _models = [];
    private CameraModel? _selectedModel;
    private GoalOverlayWindow? _overlay;

    public CameraModelsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ModelsList.ItemsSource = _models;
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateBusyState);
    }

    public Func<Task>? IdleHotkeyAction => null;

    public async Task OnShownAsync() => await RefreshModelsAsync(_selectedModel?.Manifest.Id);

    internal async Task RefreshModelsAsync(string? selectedId = null)
    {
        IReadOnlyList<CameraModelManifest> models = await _services.CameraModels.ListAsync().ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            _models.Clear();
            foreach (CameraModelManifest model in models) _models.Add(model);
            if (selectedId is not null) ModelsList.SelectedItem = _models.FirstOrDefault(model => model.Id == selectedId);
        });
    }

    private async void ModelsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CloseOverlay();
        if (ModelsList.SelectedItem is not CameraModelManifest manifest)
        {
            _selectedModel = null;
            AlignButton.IsEnabled = false;
            OverlayButton.IsEnabled = false;
            return;
        }
        _selectedModel = await _services.CameraModels.LoadAsync(manifest.Id);
        if (_selectedModel is null) return;
        ModelNameText.Text = manifest.Name;
        PreviewImage.Source = BitmapSourceFactory.Create(_selectedModel.GoalOverlay);
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        AlignButton.IsEnabled = !_services.Coordinator.IsBusy;
        OverlayButton.IsEnabled = true;
        RenameButton.IsEnabled = !_services.Coordinator.IsBusy;
        StatusText.Text = $"Ready. Baseline {manifest.BaselineScore:P0}, alignment target {manifest.SuccessThreshold:P0}.";
    }

    private void NewModel_Click(object sender, RoutedEventArgs e)
    {
        ModelsList.SelectedItem = null;
        _selectedModel = null;
        ModelNameText.Text = "Camera model";
        SettleText.Text = "200";
        PreviewImage.Source = null;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        AlignButton.IsEnabled = false;
        OverlayButton.IsEnabled = false;
        RenameButton.IsEnabled = false;
        StatusText.Text = "Ready to create an automatic multi-region model.";
        CloseOverlay();
    }

    private void Setup_Click(object sender, RoutedEventArgs e)
    {
        CameraCalibrationSettings settings;
        try { settings = ReadSettings(); }
        catch (Exception error) { StatusText.Text = error.Message; return; }
        CloseOverlay();
        Progress<MacroProgress> progress = new(UpdateProgress);
        _services.Coordinator.Arm("Camera setup", async token =>
        {
            CameraModel model = await _services.Camera.CalibrateAsync(settings, progress, token);
            await Dispatcher.InvokeAsync(() => _selectedModel = model);
            await RefreshModelsAsync(model.Manifest.Id);
        }, new DeepDebugOperationContext
        {
            CameraModelIds = [ModelId.FromName(settings.Name)],
            OperationSettings = settings,
            RefreshReferencedModelsAfterOperation = true,
        });
        string hotkey = _services.Hotkey.DisplayName;
        StatusText.Text = $"Camera setup armed. Focus Roblox and press {hotkey}; zoom, pitch, and shift lock are prepared automatically.";
        AppendLog($"Setup armed. Start with shift lock off, put the player at the repeatable position and goal yaw, then press {hotkey}. The app will zoom out, look down, and enable shift lock temporarily.");
        UpdateBusyState();
    }

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedModel is null) return;
        CloseOverlay();
        Progress<MacroProgress> progress = new(UpdateProgress);
        _services.Coordinator.Arm("Camera alignment", async token =>
        {
            double score = await _services.Camera.AlignAsync(_selectedModel, progress: progress, cancellationToken: token);
            await Dispatcher.InvokeAsync(() => StatusText.Text = $"Alignment finished at {score:P0} confidence.");
        }, new DeepDebugOperationContext
        {
            CameraModelIds = [_selectedModel.Manifest.Id],
            OperationSettings = new { Model = _selectedModel.Manifest.Id },
        });
        string hotkey = _services.Hotkey.DisplayName;
        StatusText.Text = $"Alignment armed. Focus Roblox and press {hotkey} to begin.";
        AppendLog($"Auto align armed. Start with shift lock off, then press {hotkey} when zoom and pitch match the model. The app will enable it temporarily.");
        UpdateBusyState();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _services.Coordinator.Cancel();

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedModel is null)
        {
            StatusText.Text = "Select a camera model to rename.";
            return;
        }

        string name = ModelNameText.Text.Trim();
        if (name.Length == 0)
        {
            StatusText.Text = "Enter a camera model name.";
            return;
        }

        try
        {
            CameraModel renamed = _selectedModel with
            {
                Manifest = _selectedModel.Manifest with { Name = name },
            };
            await _services.CameraModels.SaveAsync(renamed);
            _selectedModel = renamed;
            await RefreshModelsAsync(renamed.Manifest.Id);
            StatusText.Text = $"Renamed camera model to '{name}'.";
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
    }

    private void Overlay_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay is not null)
        {
            CloseOverlay();
            return;
        }
        if (_selectedModel is null) return;
        try
        {
            _overlay = new GoalOverlayWindow(_selectedModel, _services.Automation);
            _overlay.Closed += (_, _) => { _overlay = null; OverlayButton.Content = "Show 30% overlay"; };
            _overlay.Show();
            OverlayButton.Content = "Hide overlay";
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (ModelsList.SelectedItem is not CameraModelManifest model) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Delete camera model '{model.Name}'?", "Delete model", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        CloseOverlay();
        await _services.CameraModels.DeleteAsync(model.Id);
        NewModel_Click(sender, e);
        await RefreshModelsAsync();
    }

    private CameraCalibrationSettings ReadSettings()
    {
        CameraCalibrationSettings settings = new()
        {
            Name = ModelNameText.Text.Trim(),
            CaptureCount = ParseInt(CaptureCountText, "Goal captures"),
            CaptureDuration = TimeSpan.FromSeconds(ParseDouble(CaptureSecondsText, "Capture time")),
            ArrowHoldMilliseconds = ParseInt(ArrowHoldText, "Arrow hold"),
            FineStepPixels = ParseInt(FinePixelsText, "Fine drag"),
            FineSearchPixels = 16,
            SettleMilliseconds = ParseInt(SettleText, "Settle time"),
            MaximumSamples = ParseInt(MaxSamplesText, "Maximum dense frames"),
        };
        settings.Validate();
        return settings;
    }

    private void UpdateProgress(MacroProgress progress)
    {
        _services.DeepDebug.RecordProgress(progress);
        OperationProgress.Value = progress.Percent;
        StatusText.Text = progress.Message;
        AppendLog(progress.Message);
    }

    private void UpdateBusyState()
    {
        bool busy = _services.Coordinator.IsBusy;
        RenameButton.IsEnabled = !busy && _selectedModel is not null;
        SetupButton.IsEnabled = !busy;
        AlignButton.IsEnabled = !busy && _selectedModel is not null;
        StopButton.IsEnabled = busy;
    }

    private void CalibrationToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = CalibrationPanel.Visibility != Visibility.Visible;
        CalibrationPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        CalibrationToggle.Content = show ? "Hide tuning" : "Show tuning";
    }

    private void AppendLog(string message)
    {
        _services.Log.Info($"Camera: {message}");
        LogText.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        LogText.ScrollToEnd();
    }

    private void CloseOverlay()
    {
        _overlay?.Close();
        _overlay = null;
        OverlayButton.Content = "Show 30% overlay";
    }

    private static int ParseInt(TextBox field, string label) => int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : throw new FormatException($"{label} must be a whole number.");

    private static double ParseDouble(TextBox field, string label) => double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : throw new FormatException($"{label} must be a number.");
}
