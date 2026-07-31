using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Inspection;
using CorePixelFormat =
    ExpeditionsMacro.Core.Imaging.PixelFormat;

namespace ExpeditionsMacro.DetectorViewer.Services;

internal enum SnapshotScenario
{
    Matched,
    Negative,
    Error,
}

internal sealed record SnapshotPresentation(
    DetectorInspectionReport? Report,
    string? Error,
    string Status);

internal static class SnapshotFixture
{
    public static SnapshotPresentation Present(
        DetectorInspectionReport? report,
        SnapshotScenario scenario)
    {
        if (scenario == SnapshotScenario.Error)
        {
            return new SnapshotPresentation(
                report,
                "Fixture decode interrupted: the selected capture is truncated. The prior source remains available.",
                "Frame failed: fixture decode interrupted");
        }
        if (report is null)
        {
            return new SnapshotPresentation(
                null,
                null,
                "Snapshot fixture has no report");
        }
        bool matched =
            scenario == SnapshotScenario.Matched;
        DetectorInspectionCheck[] checks =
            report.Checks
                .Select(check =>
                    check.Status ==
                    DetectorInspectionCheckStatus
                        .NotExposed
                        ? check
                        : check with
                        {
                            Status = matched
                                ? DetectorInspectionCheckStatus
                                    .Passed
                                : DetectorInspectionCheckStatus
                                    .Failed,
                        })
                .ToArray();
        DetectorInspectionReport presented =
            report with
            {
                FinalState = matched
                    ? "Matched"
                    : "No match",
                Confidence = matched
                    ? 0.941
                    : 0.184,
                DecisionThreshold = ">= 0.820",
                Passed = matched,
                Action = matched
                    ? new DetectorInspectionPoint(
                        612,
                        563,
                        false,
                        "Snapshot fixture advisory; interactive reports retain production action provenance.")
                    : null,
                Checks = checks,
            };
        return new SnapshotPresentation(
            presented,
            null,
            matched
                ? "Snapshot matched-state fixture ready"
                : "Snapshot negative-state fixture ready");
    }

    public static DecodedViewerFrame Create()
    {
        const int width = 808;
        const int height = 611;
        byte[] pixels =
            new byte[width * height * 3];
        Fill(
            pixels,
            width,
            0,
            0,
            width,
            height,
            18,
            21,
            29);
        Fill(
            pixels,
            width,
            0,
            0,
            width,
            56,
            28,
            32,
            42);
        Fill(
            pixels,
            width,
            34,
            86,
            234,
            430,
            31,
            36,
            47);
        Fill(
            pixels,
            width,
            272,
            86,
            502,
            430,
            24,
            28,
            37);
        Fill(
            pixels,
            width,
            300,
            132,
            446,
            56,
            44,
            50,
            65);
        Fill(
            pixels,
            width,
            314,
            145,
            10,
            30,
            94,
            220,
            228);
        Fill(
            pixels,
            width,
            325,
            219,
            396,
            62,
            56,
            67,
            87);
        Fill(
            pixels,
            width,
            325,
            297,
            396,
            62,
            68,
            73,
            92);
        Fill(
            pixels,
            width,
            325,
            375,
            396,
            62,
            48,
            57,
            74);
        Fill(
            pixels,
            width,
            260,
            544,
            288,
            42,
            103,
            111,
            231);
        Fill(
            pixels,
            width,
            576,
            544,
            164,
            42,
            38,
            43,
            56);

        ImageFrame image = new(
            width,
            height,
            CorePixelFormat.Rgb24,
            pixels,
            takeOwnership: false);
        BitmapSource bitmap =
            BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Rgb24,
                null,
                pixels,
                width * 3);
        bitmap.Freeze();
        return new DecodedViewerFrame(
            image,
            bitmap);
    }

    private static void Fill(
        byte[] pixels,
        int strideWidth,
        int x,
        int y,
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        for (int row = y;
             row < y + height;
             row++)
        {
            for (int column = x;
                 column < x + width;
                 column++)
            {
                int offset =
                    (row * strideWidth + column) * 3;
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
            }
        }
    }
}
