using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ExpeditionsMacro.DetectorViewer.Controls;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.DetectorViewer.Services;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer;

public partial class MainWindow : Window
{
    private readonly DetectorInspectionCatalogResult _catalog;
    private readonly IReadOnlyList<DetectorCatalogItem> _catalogItems;
    private readonly ViewerSourceSession _sourceSession = new();
    private readonly RepositoryDatasetLocation?
        _repositoryDatasets;
    private readonly bool _shutdownApplicationOnClose;
    private readonly DetectorEvaluationController _evaluation;
    private readonly DetectorAnnotationController _annotations;
    private DecodedViewerFrame? _currentFrame;
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
        _repositoryDatasets =
            RepositoryDatasetLocator.Find();
        _catalogItems =
            catalog.Definitions
                .Select(definition =>
                    new DetectorCatalogItem(
                        definition))
                .ToArray();
        _evaluation = new DetectorEvaluationController(
            _catalogItems,
            CatalogPane,
            EvidencePane,
            Viewport,
            StatusText);
        _annotations = new DetectorAnnotationController(
            Viewport,
            AnnotationPane,
            message => StatusText.Text = message);
        _evaluation.DetectorPresented +=
            _annotations.SetDetector;
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
        OpenRepositoryDatasetsButton.ToolTip =
            _repositoryDatasets is null
                ? "Repository checkout not found. Use Open folder and choose its datasets folder."
                : $"Open every image under {_repositoryDatasets.DatasetRoot} (Ctrl+D)";
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
        Viewport.SetFrameSet(
            ["canonical-snapshot.png"]);
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
        await _evaluation.EvaluateSelectedAsync(
            frame.Image,
            CancellationToken.None);
        ApplySnapshotScenario(scenario);
        if (scenario == SnapshotScenario.Annotation)
        {
            PrepareAnnotationSnapshot();
        }
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
            _evaluation.CurrentReport,
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
                _evaluation.CurrentReport,
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
        _evaluation.Present(
            CatalogPane.SelectedItem,
            presentation.Report);
        StatusText.Text =
            presentation.Status;
    }

    private void PrepareAnnotationSnapshot()
    {
        DetectorImageAnnotation annotation =
            SnapshotFixture.CreateAnnotation(
                CatalogPane.SelectedItem?.Id ??
                "bounty-board");
        SetInspectorMode(annotations: true);
        AnnotationPane.SetAnnotation(
            "Bounty Board",
            annotation,
            "datasets/detector-annotations.json");
        Viewport.SetAnnotations(
            annotation.Regions,
            annotation.Regions[0].Id);
    }

    private async Task OpenPathAsync(
        string path,
        bool repositoryDatasets = false)
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
                repositoryDatasets
                    ? await _sourceSession
                        .OpenRepositoryDatasetsAsync(
                            path,
                            progress,
                            token)
                    : await _sourceSession.OpenAsync(
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
                loaded.Frames
                    .Select(frame =>
                        frame.DisplayPath)
                    .ToArray());
            ShowCurrentFrame(loaded);
            await _evaluation.AutoSelectAsync(
                loaded.Frame.Image,
                loaded.Record.DisplayPath,
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
            await _evaluation.AutoSelectAsync(
                loaded.Frame.Image,
                loaded.Record.DisplayPath,
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
        _annotations.SetFrame(loaded);
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

    private void OpenRepositoryDatasets_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_repositoryDatasets is null)
        {
            StatusText.Text =
                "Repository checkout not found. Use Open folder and choose the datasets folder.";
            return;
        }
        _ = OpenPathAsync(
            _repositoryDatasets.DatasetRoot,
            repositoryDatasets: true);
    }

    private void CatalogPane_SelectedDetectorChanged(
        object sender,
        EventArgs e)
    {
        if (_evaluation.IsSelectingDetector)
        {
            return;
        }
        EvidencePane.SetDefinition(
            CatalogPane.SelectedItem);
        if (_currentFrame is not null)
        {
            StartWork();
            _ = _evaluation.EvaluateSelectedAsync(
                _currentFrame.Image,
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
        _evaluation.SetSelectedRegions(selected);
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

    private void EvidenceMode_Click(
        object sender,
        RoutedEventArgs e) =>
        SetInspectorMode(annotations: false);

    private void AnnotationMode_Click(
        object sender,
        RoutedEventArgs e) =>
        SetInspectorMode(annotations: true);

    private void SetInspectorMode(bool annotations)
    {
        EvidencePane.Visibility = annotations
            ? Visibility.Collapsed
            : Visibility.Visible;
        AnnotationPane.Visibility = annotations
            ? Visibility.Visible
            : Visibility.Collapsed;
        EvidenceModeButton.Tag = annotations
            ? null
            : "Active";
        AnnotationModeButton.Tag = annotations
            ? "Active"
            : null;
        _annotations.SetAnnotationMode(annotations);
    }

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
            () => OpenRepositoryDatasets_Click(
                this,
                e),
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
