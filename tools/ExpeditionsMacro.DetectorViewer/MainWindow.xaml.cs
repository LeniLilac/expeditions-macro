using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.DetectorViewer.Services;
using ExpeditionsMacro.DetectorViewer.Controls;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer;

public partial class MainWindow : Window
{
    private readonly DetectorInspectionCatalogResult _catalog;
    private readonly IReadOnlyList<DetectorCatalogItem> _catalogItems;
    private readonly ViewerSourceSession _sourceSession = new();
    private readonly bool _shutdownApplicationOnClose;
    private DecodedViewerFrame? _currentFrame;
    private DetectorInspectionReport? _currentReport;
    private CancellationTokenSource? _workCancellation;
    private long _generation;
    private ViewerTheme _theme;

    public MainWindow(
        DetectorInspectionCatalogResult catalog,
        string? startupWarning = null,
        bool shutdownApplicationOnClose = false)
    {
        _catalog =
            catalog ??
            throw new ArgumentNullException(
                nameof(catalog));
        _shutdownApplicationOnClose =
            shutdownApplicationOnClose;
        InitializeComponent();
        _catalogItems =
            catalog.Definitions
                .Select(definition =>
                    new DetectorCatalogItem(
                        definition))
                .ToArray();
        CatalogPane.SetCatalog(_catalogItems);
        SetTheme(
            ViewerThemeManager.SystemTheme());
        SourceSummaryText.Text =
            startupWarning ??
            $"{catalog.Definitions.Count:N0} entries • {catalog.EvaluableDetectorCount:N0} evaluable • {catalog.UnavailableDetectorCount:N0} explicit limitations";
        if (!string.IsNullOrWhiteSpace(
                startupWarning))
        {
            StatusText.Text = startupWarning;
        }
    }

    internal async Task OpenInitialPathAsync(
        string path) =>
        await OpenPathAsync(path);

    internal async Task PrepareSnapshotAsync(
        DecodedViewerFrame frame,
        string label,
        SnapshotScenario scenario)
    {
        _currentFrame = frame;
        SourcePathText.Text = label;
        SourceSummaryText.Text =
            "Synthetic canonical frame • local snapshot fixture";
        Viewport.ShowFrame(
            frame.Bitmap,
            0,
            1,
            "canonical-snapshot.png",
            new DateTimeOffset(
                2026,
                7,
                31,
                12,
                0,
                0,
                TimeSpan.Zero));
        await EvaluateSelectedAsync(
            ++_generation,
            CancellationToken.None);
        ApplySnapshotScenario(scenario);
        UpdateLayout();
        Viewport.Fit();
        StatusText.Text =
            "Snapshot fixture ready";
    }

    internal void SetTheme(ViewerTheme theme)
    {
        _theme = theme;
        ViewerThemeManager.Apply(theme);
        ThemeButton.Content =
            theme.ToString();
        ViewerIcon.SetIcon(
            ThemeButton,
            theme == ViewerTheme.Dark
                ? ViewerIconKind.Moon
                : ViewerIconKind.Sun);
        Viewport.SetReport(
            _currentReport,
            EvidencePane.SelectedCheck?
                .RegionIds
                .ToHashSet(
                    StringComparer.Ordinal));
    }

    private void ApplySnapshotScenario(
        SnapshotScenario scenario)
    {
        SnapshotPresentation presentation =
            SnapshotFixture.Present(
                _currentReport,
                scenario);
        if (presentation.Error is not null)
        {
            EvidencePane.SetError(
                presentation.Error);
            StatusText.Text =
                presentation.Status;
            return;
        }
        if (presentation.Report is null)
        {
            return;
        }
        _currentReport = presentation.Report;
        EvidencePane.SetReport(
            _currentReport);
        Viewport.SetReport(
            _currentReport);
        StatusText.Text =
            presentation.Status;
    }

    private async Task OpenPathAsync(string path)
    {
        long generation = StartWork();
        CancellationToken token =
            _workCancellation!.Token;
        try
        {
            StatusText.Text =
                "Opening image source…";
            Progress<string> progress =
                new(message =>
                    StatusText.Text = message);
            LoadedViewerFrame loaded =
                await _sourceSession.OpenAsync(
                    path,
                    progress,
                    token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation)
            {
                return;
            }
            _currentFrame = loaded.Frame;
            Viewport.SetFrameSet(
                loaded.FrameCount);
            ShowCurrentFrame(loaded);
            await EvaluateSelectedAsync(
                generation,
                token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            StatusText.Text =
                $"Open failed: {error.Message}";
            if (_currentFrame is null)
            {
                Viewport.ShowMessage(
                    "Source could not be opened",
                    error.Message);
            }
        }
    }

    private async Task LoadFrameAsync(int index)
    {
        long generation = StartWork();
        CancellationToken token =
            _workCancellation!.Token;
        try
        {
            StatusText.Text =
                $"Loading frame {index + 1:N0}…";
            LoadedViewerFrame loaded =
                await _sourceSession.LoadAsync(
                    index,
                    token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation)
            {
                return;
            }
            _currentFrame = loaded.Frame;
            ShowCurrentFrame(loaded);
            await EvaluateSelectedAsync(
                generation,
                token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            StatusText.Text =
                $"Frame failed: {error.Message}";
            EvidencePane.SetError(error.Message);
        }
    }

    private void ShowCurrentFrame(
        LoadedViewerFrame loaded)
    {
        SourcePathText.Text =
            loaded.SourcePath;
        SourceSummaryText.Text =
            $"{loaded.SourceKindLabel} • {loaded.FrameCount:N0} frame{(loaded.FrameCount == 1 ? string.Empty : "s")}";
        Viewport.ShowFrame(
            loaded.Frame.Bitmap,
            loaded.Index,
            loaded.FrameCount,
            loaded.Record.DisplayPath,
            loaded.Record.Timestamp);
        StatusText.Text =
            $"{loaded.Frame.Image.Width:N0} × {loaded.Frame.Image.Height:N0} • {loaded.Frame.Image.Format}";
    }

    private async Task EvaluateSelectedAsync(
        long generation,
        CancellationToken token)
    {
        DetectorCatalogItem? item =
            CatalogPane.SelectedItem;
        EvidencePane.SetDefinition(item);
        if (item is null ||
            _currentFrame is null)
        {
            _currentReport = null;
            Viewport.SetReport(null);
            return;
        }
        if (_currentFrame.Image.Width != 808 ||
            _currentFrame.Image.Height != 611)
        {
            string message =
                $"Production detectors require the canonical 808 × 611 Roblox client. This frame is {_currentFrame.Image.Width} × {_currentFrame.Image.Height}; it is displayed but not evaluated.";
            _currentReport =
                DetectorInspectionReport.Unavailable(
                    message,
                    item.Definition.Regions);
            EvidencePane.SetError(message);
            Viewport.SetReport(_currentReport);
            return;
        }

        EvidencePane.SetEvaluating();
        StatusText.Text =
            $"Evaluating {item.Name}…";
        try
        {
            DetectorInspectionReport report =
                await Task.Run(
                    () =>
                        item.Definition.Evaluate(
                            _currentFrame.Image),
                    token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation ||
                item != CatalogPane.SelectedItem)
            {
                return;
            }
            _currentReport = report;
            EvidencePane.SetReport(report);
            Viewport.SetReport(report);
            StatusText.Text =
                $"Evaluated {item.Name} • {report.FinalState}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            _currentReport = null;
            EvidencePane.SetError(error.Message);
            Viewport.SetReport(null);
            StatusText.Text =
                $"Evaluation failed: {error.Message}";
        }
    }

    private long StartWork()
    {
        _workCancellation?.Cancel();
        _workCancellation?.Dispose();
        _workCancellation =
            new CancellationTokenSource();
        return ++_generation;
    }

    private void OpenSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? path =
            ViewerSourcePicker.PickFile(this);
        if (path is not null)
        {
            _ = OpenPathAsync(path);
        }
    }

    private void OpenFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? path =
            ViewerSourcePicker.PickFolder(this);
        if (path is not null)
        {
            _ = OpenPathAsync(path);
        }
    }

    private void CatalogPane_SelectedDetectorChanged(
        object sender,
        EventArgs e)
    {
        EvidencePane.SetDefinition(
            CatalogPane.SelectedItem);
        if (_currentFrame is not null)
        {
            long generation = StartWork();
            _ = EvaluateSelectedAsync(
                generation,
                _workCancellation!.Token);
        }
    }

    private void Viewport_FrameIndexRequested(
        object? sender,
        int index) =>
        _ = LoadFrameAsync(index);

    private void EvidencePane_SelectedCheckChanged(
        object sender,
        EventArgs e)
    {
        HashSet<string> selected =
            EvidencePane.SelectedCheck?.RegionIds
                .ToHashSet(
                    StringComparer.Ordinal) ??
            new HashSet<string>();
        Viewport.SetReport(
            _currentReport,
            selected);
    }

    private void Viewport_PixelHovered(
        object? sender,
        Point point)
    {
        if (_currentFrame is null)
        {
            return;
        }
        PixelSample? sample =
            PixelInspector.Sample(
                _currentFrame.Image,
                (int)Math.Floor(point.X),
                (int)Math.Floor(point.Y));
        if (sample is null)
        {
            return;
        }
        PixelText.Text = sample.Summary;
        PixelSwatch.Background =
            new SolidColorBrush(
                Color.FromRgb(
                    sample.Red,
                    sample.Green,
                    sample.Blue));
    }

    private void Viewport_PixelExited(
        object? sender,
        EventArgs e)
    {
        PixelText.Text =
            "Move over the image for pixel details";
        PixelSwatch.Background =
            Brushes.Transparent;
    }

    private void ThemeButton_Click(
        object sender,
        RoutedEventArgs e) =>
        SetTheme(
            _theme == ViewerTheme.Dark
                ? ViewerTheme.Light
                : ViewerTheme.Dark);

    private void Window_DragOver(
        object sender,
        DragEventArgs e) =>
        ViewerWindowCommandRouter
            .ApplyDragOver(e);

    private void Window_Drop(
        object sender,
        DragEventArgs e)
    {
        string? path =
            ViewerWindowCommandRouter
                .DroppedPath(e);
        if (path is not null)
        {
            _ = OpenPathAsync(path);
        }
    }

    private void Window_KeyDown(
        object sender,
        KeyEventArgs e) =>
        ViewerWindowCommandRouter.HandleKey(
            e,
            () => OpenSource_Click(this, e),
            () => OpenFolder_Click(this, e),
            index => _ = LoadFrameAsync(index),
            Viewport.FrameIndex,
            Viewport.Fit);

    private void Window_Closed(
        object? sender,
        EventArgs e) =>
        ViewerWindowLifecycle.Close(
            _workCancellation,
            _sourceSession,
            _shutdownApplicationOnClose);
}
