using System.Globalization;
using System.Text.Json;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Inspection;

internal sealed record DetectorProbeMetric(
    string Key,
    string DisplayValue,
    double? NumericValue = null,
    bool? BooleanValue = null,
    double? GateValue = null,
    bool GateIsMaximum = false);

internal sealed record DetectorProbeResult(
    string State,
    double? Confidence,
    double? Threshold,
    bool? Passed,
    DetectorInspectionPoint? Action,
    IReadOnlyList<DetectorProbeMetric> Metrics)
{
    public static DetectorProbeResult Create(
        string state,
        double? confidence = null,
        double? threshold = null,
        bool? passed = null,
        int? actionX = null,
        int? actionY = null,
        bool actionIsLive = true,
        string actionProvenance =
            "Production detector result",
        IReadOnlyList<DetectorProbeMetric>? metrics = null) =>
        new(
            state,
            confidence,
            threshold,
            passed,
            actionX is int x &&
            actionY is int y
                ? new DetectorInspectionPoint(
                    x,
                    y,
                    actionIsLive,
                    actionProvenance)
                : null,
            metrics ?? []);
}

internal static class DetectorInspectionProbe
{
    private const int MaximumTraceMetrics = 160;
    private static readonly object TraceGate = new();

    public static DetectorInspectionReport Run(
        ImageFrame image,
        IReadOnlyList<Type> owners,
        DetectorInspectionDetailLevel detailLevel,
        string? limitation,
        Func<DetectorProbeResult> evaluate)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(evaluate);
        IReadOnlyList<DetectorInspectionRegion> regions =
            ProductionDetectorMetadata.ReadRegions(owners);
        IReadOnlyList<ProductionNumericGate> gates =
            ProductionDetectorMetadata.ReadNumericGates(owners);
        List<VisionDetectionTrace> traces = [];
        DetectorProbeResult result;
        lock (TraceGate)
        {
            void OnTrace(VisionDetectionTrace trace) =>
                traces.Add(trace);

            VisionTrace.Detected += OnTrace;
            try
            {
                result = evaluate();
            }
            finally
            {
                VisionTrace.Detected -= OnTrace;
            }
        }

        List<DetectorProbeMetric> metrics =
        [
            .. result.Metrics,
            .. FlattenTraces(traces),
        ];
        IReadOnlyList<DetectorInspectionCheck> checks =
            BuildChecks(
                image,
                metrics,
                regions,
                gates);
        List<string> notes = [];
        if (!string.IsNullOrWhiteSpace(limitation))
        {
            notes.Add(limitation);
        }
        if (detailLevel !=
            DetectorInspectionDetailLevel.Detailed)
        {
            notes.Add(
                "Only evidence emitted by the production result or diagnostic trace is graded. Static production geometry is shown exactly; local and translated geometry created inside an algorithm remains unreported.");
        }
        if (traces.Count == 0)
        {
            notes.Add(
                "This detector emitted no structured diagnostic trace for the selected frame.");
        }

        return new DetectorInspectionReport(
            result.State,
            result.Confidence,
            FormatThreshold(result.Threshold),
            result.Passed,
            result.Action,
            regions,
            checks,
            notes);
    }

    private static IReadOnlyList<DetectorInspectionCheck>
        BuildChecks(
        ImageFrame image,
        IReadOnlyList<DetectorProbeMetric> metrics,
        IReadOnlyList<DetectorInspectionRegion> regions,
        IReadOnlyList<ProductionNumericGate> gates)
    {
        List<DetectorInspectionCheck> checks = [];
        HashSet<string> referencedRegions =
            new(StringComparer.Ordinal);
        int index = 0;
        foreach (DetectorProbeMetric metric in metrics
                     .DistinctBy(item =>
                         $"{item.Key}\0{item.DisplayValue}")
                     .Take(MaximumTraceMetrics))
        {
            IReadOnlyList<string> regionIds =
                ProductionDetectorMetadata.FindRegions(
                    metric.Key,
                    regions);
            referencedRegions.UnionWith(regionIds);
            ProductionNumericGate? gate =
                metric.GateValue is double explicitGate
                    ? new ProductionNumericGate(
                        "Explicit production gate",
                        explicitGate,
                        metric.GateIsMaximum)
                    : null;
            DetectorInspectionCheckStatus status =
                Status(metric, gate);
            string threshold = gate is null
                ? metric.BooleanValue is not null
                    ? "Boolean result"
                    : "Detector-owned"
                : $"{(gate.IsMaximum ? "<=" : ">=")} {FormatNumber(gate.Value)}";
            checks.Add(new DetectorInspectionCheck(
                $"metric:{index++}:{NormalizeId(metric.Key)}",
                ProductionDetectorMetadata.FriendlyName(
                    metric.Key),
                ProductionDetectorMetadata.ExpectedEvidence(
                    metric.Key),
                metric.DisplayValue,
                threshold,
                status,
                regionIds));
        }

        foreach (ProductionNumericGate gate in gates)
        {
            checks.Add(new DetectorInspectionCheck(
                $"constant:{NormalizeId(gate.Name)}",
                $"Named constant: {ProductionDetectorMetadata.FriendlyName(gate.Name)}",
                "Advisory reference reflected exactly from a named production constant.",
                FormatNumber(gate.Value),
                "Metric association not exposed",
                DetectorInspectionCheckStatus.Observed,
                []));
        }

        foreach (DetectorInspectionRegion region in regions
                     .Where(region =>
                         !referencedRegions.Contains(region.Id)))
        {
            checks.Add(new DetectorInspectionCheck(
                $"region:{region.Id}",
                region.Label,
                region.Expected,
                MeasureRegion(image, region),
                "Path metric not exposed",
                DetectorInspectionCheckStatus.NotExposed,
                [region.Id]));
        }

        if (checks.Count == 0)
        {
            checks.Add(new DetectorInspectionCheck(
                "result",
                "Production result",
                "A production detector result.",
                "No component metrics were exposed.",
                "Detector-owned",
                DetectorInspectionCheckStatus.NotExposed,
                []));
        }
        return checks;
    }

    private static DetectorInspectionCheckStatus Status(
        DetectorProbeMetric metric,
        ProductionNumericGate? gate)
    {
        if (metric.BooleanValue is bool flag)
        {
            return flag
                ? DetectorInspectionCheckStatus.Passed
                : DetectorInspectionCheckStatus.Failed;
        }
        if (metric.NumericValue is not double number ||
            gate is null)
        {
            return DetectorInspectionCheckStatus.Observed;
        }
        bool passed = gate.IsMaximum
            ? number <= gate.Value
            : number >= gate.Value;
        return passed
            ? DetectorInspectionCheckStatus.Passed
            : DetectorInspectionCheckStatus.Failed;
    }

    private static IReadOnlyList<DetectorProbeMetric>
        FlattenTraces(
        IReadOnlyList<VisionDetectionTrace> traces)
    {
        List<DetectorProbeMetric> metrics = [];
        foreach (VisionDetectionTrace trace in traces)
        {
            if (trace.Data is null)
            {
                continue;
            }
            try
            {
                JsonElement element =
                    JsonSerializer.SerializeToElement(
                        trace.Data,
                        trace.Data.GetType());
                FlattenElement(
                    element,
                    trace.Detector,
                    metrics,
                    depth: 0);
            }
            catch (Exception error) when (
                error is JsonException or
                    NotSupportedException)
            {
                metrics.Add(new DetectorProbeMetric(
                    $"{trace.Detector}.trace",
                    "Diagnostic payload could not be flattened."));
            }
            if (metrics.Count >= MaximumTraceMetrics)
            {
                break;
            }
        }
        return metrics
            .Take(MaximumTraceMetrics)
            .ToArray();
    }

    private static void FlattenElement(
        JsonElement element,
        string path,
        ICollection<DetectorProbeMetric> output,
        int depth)
    {
        if (output.Count >= MaximumTraceMetrics ||
            depth > 4)
        {
            return;
        }
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in
                         element.EnumerateObject())
                {
                    if (IsSummaryProperty(property.Name))
                    {
                        continue;
                    }
                    FlattenElement(
                        property.Value,
                        $"{path}.{property.Name}",
                        output,
                        depth + 1);
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in
                         element.EnumerateArray())
                {
                    FlattenElement(
                        item,
                        $"{path}.{index++}",
                        output,
                        depth + 1);
                    if (index >= 24)
                    {
                        break;
                    }
                }
                break;
            case JsonValueKind.Number:
                if (element.TryGetDouble(out double number))
                {
                    output.Add(new DetectorProbeMetric(
                        path,
                        FormatNumber(number),
                        number));
                }
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                bool flag = element.GetBoolean();
                output.Add(new DetectorProbeMetric(
                    path,
                    flag ? "True" : "False",
                    BooleanValue: flag));
                break;
            case JsonValueKind.String:
                output.Add(new DetectorProbeMetric(
                    path,
                    element.GetString() ?? string.Empty));
                break;
        }
    }

    private static bool IsSummaryProperty(string name) =>
        name.Equals(
            "ActionX",
            StringComparison.OrdinalIgnoreCase) ||
        name.Equals(
            "ActionY",
            StringComparison.OrdinalIgnoreCase) ||
        name.Equals(
            "Confidence",
            StringComparison.OrdinalIgnoreCase) ||
        name.Equals(
            "State",
            StringComparison.OrdinalIgnoreCase);

    private static string MeasureRegion(
        ImageFrame image,
        DetectorInspectionRegion item)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            !item.Region.FitsWithin(
                image.Width,
                image.Height))
        {
            return "Region falls outside this frame.";
        }
        long red = 0;
        long green = 0;
        long blue = 0;
        for (int y = item.Region.Y;
             y < item.Region.Bottom;
             y++)
        {
            for (int x = item.Region.X;
                 x < item.Region.Right;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                red += image.Pixels[pixel];
                green += image.Pixels[pixel + 1];
                blue += image.Pixels[pixel + 2];
            }
        }
        double pixels =
            item.Region.Width * item.Region.Height;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Mean RGB {red / pixels:0}, {green / pixels:0}, {blue / pixels:0}; production path metric not exposed.");
    }

    private static string FormatThreshold(
        double? value) =>
        value is null
            ? "Composite detector gate"
            : $">= {FormatNumber(value.Value)}";

    private static string FormatNumber(double value) =>
        Math.Abs(value) >= 100
            ? value.ToString(
                "0",
                CultureInfo.InvariantCulture)
            : value.ToString(
                "0.###",
                CultureInfo.InvariantCulture);

    private static string NormalizeId(string value) =>
        string.Concat(
            value.Select(character =>
                char.IsLetterOrDigit(character)
                    ? char.ToLowerInvariant(character)
                    : '-'));
}
