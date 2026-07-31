using System.Windows.Controls;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.DetectorViewer.Models;
using ExpeditionsMacro.DetectorViewer.Services;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Controls;

public sealed class DetectorEvaluationController
{
    private readonly IReadOnlyList<DetectorCatalogItem> _items;
    private readonly DetectorCatalogPane _catalog;
    private readonly EvidencePane _evidence;
    private readonly FrameViewport _viewport;
    private readonly TextBlock _status;

    public DetectorEvaluationController(
        IReadOnlyList<DetectorCatalogItem> items,
        DetectorCatalogPane catalog,
        EvidencePane evidence,
        FrameViewport viewport,
        TextBlock status)
    {
        _items = items;
        _catalog = catalog;
        _evidence = evidence;
        _viewport = viewport;
        _status = status;
    }

    public DetectorInspectionReport? CurrentReport
    {
        get;
        private set;
    }

    public event Action<DetectorCatalogItem?>?
        DetectorPresented;

    public bool IsSelectingDetector
    {
        get;
        private set;
    }

    public async Task AutoSelectAsync(
        ImageFrame image,
        string displayPath,
        CancellationToken cancellationToken)
    {
        _status.Text =
            "Finding the detector for this frame…";
        DetectorFrameEvaluation? match =
            await DetectorFrameEvaluator
                .SelectAutomaticAsync(
                    _items,
                    image,
                    displayPath,
                    cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (match is null)
        {
            CurrentReport = null;
            DetectorPresented?.Invoke(null);
            _evidence.SetError(
                DetectorFrameEvaluator.IsCanonical(image)
                    ? "No production detector matched this frame automatically. Select one to inspect it."
                    : DetectorFrameEvaluator
                        .CanonicalSizeMessage(image));
            _viewport.SetReport(null);
            _status.Text =
                "No detector matched automatically";
            return;
        }

        IsSelectingDetector = true;
        try
        {
            _catalog.SelectDetector(match.Item.Id);
        }
        finally
        {
            IsSelectingDetector = false;
        }
        Present(
            match.Item,
            match.Report);
        _status.Text =
            $"Auto-selected {match.Item.Name} • {match.Report.FinalState}";
    }

    public async Task EvaluateSelectedAsync(
        ImageFrame? image,
        CancellationToken cancellationToken)
    {
        DetectorCatalogItem? item =
            _catalog.SelectedItem;
        _evidence.SetDefinition(item);
        if (item is null || image is null)
        {
            CurrentReport = null;
            _viewport.SetReport(null);
            return;
        }

        _evidence.SetEvaluating();
        _status.Text =
            $"Evaluating {item.Name}…";
        try
        {
            DetectorInspectionReport report =
                await DetectorFrameEvaluator.EvaluateAsync(
                    item,
                    image,
                    cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (item != _catalog.SelectedItem)
            {
                return;
            }
            Present(item, report);
            _status.Text =
                $"Evaluated {item.Name} • {report.FinalState}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            CurrentReport = null;
            _evidence.SetError(error.Message);
            _viewport.SetReport(null);
            _status.Text =
                $"Evaluation failed: {error.Message}";
        }
    }

    public void Present(
        DetectorCatalogItem? item,
        DetectorInspectionReport report)
    {
        CurrentReport = report;
        _evidence.SetDefinition(item);
        _evidence.SetReport(report);
        _viewport.SetReport(report);
        DetectorPresented?.Invoke(item);
    }

    public void SetSelectedRegions(
        IReadOnlySet<string> selectedRegions) =>
        _viewport.SetReport(
            CurrentReport,
            selectedRegions);
}
