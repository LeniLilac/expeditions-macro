using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.App.Windows;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Pages;

public partial class CameraModelsPage : UserControl, IAppPage
{
    private readonly AppServices _services;
    private readonly ObservableCollection<CameraModelManifest> _models = [];
    private ScreenRegion? _selectedScreenRegion;
    private CameraModel? _selectedModel;
    private GoalOverlayWindow? _overlay;

    public CameraModelsPage(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ModelsList.ItemsSource = _models;
        _services.Coordinator.StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateBusyState);
    }

    public Func<Task>? IdleF6Action => null;

    public async Task OnShownAsync() => await RefreshModelsAsync(_selectedModel?.Manifest.Id);

    private async Task RefreshModelsAsync(string? selectedId = null)
    {
        IReadOnlyList<CameraModelManifest> models = await _services.CameraModels.ListAsync();
        _models.Clear();
        foreach (CameraModelManifest model in models) _models.Add(model);
        if (selectedId is not null) ModelsList.SelectedItem = _models.FirstOrDefault(model => model.Id == selectedId);
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
        RegionText.Text = $"Relative region: ({manifest.Region.X}, {manifest.Region.Y}), {manifest.Region.Width} × {manifest.Region.Height} · client {manifest.ClientWidth} × {manifest.ClientHeight}";
        PreviewImage.Source = BitmapSourceFactory.Create(_selectedModel.GoalOverlay);
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        AlignButton.IsEnabled = !_services.Coordinator.IsBusy;
        OverlayButton.IsEnabled = true;
        StatusText.Text = $"Ready. Baseline {manifest.BaselineScore:P0}, alignment target {manifest.SuccessThreshold:P0}.";
    }

    private void NewModel_Click(object sender, RoutedEventArgs e)
    {
        ModelsList.SelectedItem = null;
        _selectedModel = null;
        _selectedScreenRegion = null;
        ModelNameText.Text = "Camera model";
        RegionText.Text = "Region: not selected";
        PreviewImage.Source = null;
        PreviewPlaceholder.Visibility = Visibility.Visible;
        AlignButton.IsEnabled = false;
        OverlayButton.IsEnabled = false;
        StatusText.Text = "Select a comparison region to create a model.";
        CloseOverlay();
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
        RegionSelectionWindow selection = new() { Owner = Window.GetWindow(this) };
        if (selection.ShowDialog() != true || selection.SelectedRegion is not { } region) return;
        _selectedScreenRegion = region;
        RegionText.Text = $"Screen selection: ({region.X}, {region.Y}), {region.Width} × {region.Height}";
        try
        {
            PreviewImage.Source = BitmapSourceFactory.Create(_services.Automation.CaptureScreen(region));
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            StatusText.Text = "Region selected. Click Setup model, then press F6 while Roblox is ready.";
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
        }
    }

    private void Setup_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedScreenRegion is not { } region)
        {
            StatusText.Text = "Select a comparison region first.";
            return;
        }
        CameraCalibrationSettings settings;
        try { settings = ReadSettings(); }
        catch (Exception error) { StatusText.Text = error.Message; return; }
        CloseOverlay();
        Progress<MacroProgress> progress = new(UpdateProgress);
        _services.Coordinator.Arm("Camera setup", async token =>
        {
            CameraModel model = await _services.Camera.CalibrateAsync(region, settings, progress, token);
            await Dispatcher.InvokeAsync(() => _selectedModel = model);
            await RefreshModelsAsync(model.Manifest.Id);
        });
        StatusText.Text = "Camera setup armed. Focus Roblox and press F6 to begin.";
        AppendLog("Setup armed. Press F6 when the goal view is ready.");
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
        });
        StatusText.Text = "Alignment armed. Focus Roblox and press F6 to begin.";
        AppendLog("Auto align armed. Press F6 when zoom and pitch match the model.");
        UpdateBusyState();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _services.Coordinator.Cancel();

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
            CoarseStepPixels = ParseInt(CoarsePixelsText, "Coarse drag"),
            FineStepPixels = ParseInt(FinePixelsText, "Fine drag"),
            SettleMilliseconds = ParseInt(SettleText, "Settle time"),
            MaximumSamples = ParseInt(MaxSamplesText, "Maximum yaw samples"),
        };
        settings.Validate();
        return settings;
    }

    private void UpdateProgress(MacroProgress progress)
    {
        OperationProgress.Value = progress.Percent;
        StatusText.Text = progress.Message;
        AppendLog(progress.Message);
    }

    private void UpdateBusyState()
    {
        bool busy = _services.Coordinator.IsBusy;
        SetupButton.IsEnabled = !busy;
        AlignButton.IsEnabled = !busy && _selectedModel is not null;
        StopButton.IsEnabled = busy;
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
