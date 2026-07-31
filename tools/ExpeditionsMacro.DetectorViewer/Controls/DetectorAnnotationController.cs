using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.DetectorViewer.Services;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public sealed class DetectorAnnotationController
{
    private readonly FrameViewport _viewport;
    private readonly AnnotationPane _pane;
    private readonly Action<string> _setStatus;
    private DetectorAnnotationStore? _store;
    private DetectorImageAnnotation? _annotation;
    private string? _imagePath;
    private string? _datasetRoot;
    private bool _annotationModeRequested;

    public DetectorAnnotationController(
        FrameViewport viewport,
        AnnotationPane pane,
        Action<string> setStatus)
    {
        _viewport = viewport;
        _pane = pane;
        _setStatus = setStatus;
        viewport.AnnotationRegionCreated +=
            OnRegionCreated;
        pane.AnnotationChanged +=
            Pane_AnnotationChanged;
        pane.SelectedRegionChanged +=
            Pane_SelectedRegionChanged;
    }

    public void SetFrame(LoadedViewerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _annotation = null;
        _imagePath = null;
        _viewport.SetAnnotationMode(false);
        _viewport.SetAnnotations([]);
        if (frame.SourceKind !=
            FrameSourceKind.RepositoryDatasets)
        {
            _pane.SetUnavailable(
                "Annotations are available for repository datasets. Open Repo datasets to begin.");
            return;
        }
        try
        {
            if (_store is null ||
                !string.Equals(
                    _datasetRoot,
                    frame.SourcePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                _store = DetectorAnnotationStore.Open(
                    frame.SourcePath);
                _datasetRoot = frame.SourcePath;
            }
            _imagePath = frame.Record.DisplayPath;
            _pane.SetUnavailable(
                "Selecting the detector for this fixture…");
        }
        catch (Exception error)
        {
            _store = null;
            _datasetRoot = null;
            _pane.SetUnavailable(error.Message);
            _setStatus(
                $"Annotations unavailable: {error.Message}");
        }
    }

    public void SetDetector(DetectorCatalogItem? item)
    {
        if (_store is null ||
            _imagePath is null ||
            item is null)
        {
            if (_imagePath is not null)
            {
                _pane.SetUnavailable(
                    "Select a detector to annotate this fixture.");
            }
            _annotation = null;
            _viewport.SetAnnotations([]);
            _viewport.SetAnnotationMode(false);
            return;
        }
        _annotation = _store.GetOrCreate(
            _imagePath,
            item.Id);
        _pane.SetAnnotation(
            item.Name,
            _annotation,
            _store.Path);
        _viewport.SetAnnotations(
            _annotation.Regions,
            _pane.SelectedRegionId);
        _viewport.SetAnnotationMode(
            _annotationModeRequested);
    }

    public void SetAnnotationMode(bool enabled)
    {
        _annotationModeRequested = enabled;
        _viewport.SetAnnotationMode(
            enabled &&
            _annotation is not null);
    }

    private void OnRegionCreated(
        DetectorAnnotationRegion region)
    {
        if (_annotation is null)
        {
            return;
        }
        region.Label =
            $"Detection area {_annotation.Regions.Count + 1:N0}";
        _pane.AddRegion(region);
    }

    private void Pane_AnnotationChanged(
        object? sender,
        EventArgs e)
    {
        if (_store is null ||
            _annotation is null)
        {
            return;
        }
        try
        {
            _store.Save();
            _viewport.SetAnnotations(
                _annotation.Regions,
                _pane.SelectedRegionId);
            _setStatus(
                $"Annotation saved • {_annotation.ImagePath}");
        }
        catch (Exception error)
        {
            _setStatus(
                $"Annotation save failed: {error.Message}");
        }
    }

    private void Pane_SelectedRegionChanged(
        object? sender,
        EventArgs e)
    {
        if (_annotation is not null)
        {
            _viewport.SetAnnotations(
                _annotation.Regions,
                _pane.SelectedRegionId);
        }
    }
}
