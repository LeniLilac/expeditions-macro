using System.IO;
using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Services;

internal static class DetectorViewerSmoke
{
    public static async Task RunAsync(
        DetectorInspectionCatalogResult catalog,
        string? outputDirectory)
    {
        DecodedViewerFrame frame =
            SnapshotFixture.Create();
        DetectorCoverageReport report =
            DetectorViewerCoverageAudit.Run(
                catalog,
                frame.Image);
        if (report.DiscoveredProductionTypes <= 0 ||
            report.PublicProductionEntryPoints <= 0 ||
            report.CatalogItems <= 0 ||
            report.CheckCapableItems <= 0)
        {
            throw new InvalidDataException(
                "Detector Viewer smoke coverage is unexpectedly empty.");
        }
        if (outputDirectory is not null)
        {
            await DetectorViewerCoverageAudit
                .WriteAsync(
                    report,
                    outputDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(
                    outputDirectory,
                    "detector-viewer-smoke.txt"),
                $"PASS{Environment.NewLine}" +
                $"production={report.DiscoveredProductionTypes}{Environment.NewLine}" +
                $"public={report.PublicProductionEntryPoints}{Environment.NewLine}" +
                $"catalog={report.CatalogItems}{Environment.NewLine}" +
                $"geometry={report.GeometryCapableItems}{Environment.NewLine}" +
                $"checks={report.CheckCapableItems}{Environment.NewLine}" +
                $"thresholds={report.ThresholdBearingItems}{Environment.NewLine}" +
                $"unavailable={report.UnavailableDetailItems}{Environment.NewLine}");
        }
    }
}
