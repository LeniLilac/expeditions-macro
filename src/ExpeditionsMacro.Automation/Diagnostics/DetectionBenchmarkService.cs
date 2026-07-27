using System.Diagnostics;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Diagnostics;

public enum DetectionBenchmarkMode
{
    ExpeditionNavigation,
    ExpeditionMatch,
    ChallengeMatch,
    StoryRaidMatch,
    EventMatch,
}

public sealed record DetectionBenchmarkMetric(
    double AverageMilliseconds,
    double P95Milliseconds);

public sealed record DetectionBenchmarkResult(
    DetectionBenchmarkMode Mode,
    int Samples,
    DetectionBenchmarkMetric Capture,
    DetectionBenchmarkMetric ModeDetection,
    DetectionBenchmarkMetric RootRecovery,
    DetectionBenchmarkMetric TotalWork,
    double WorkChecksPerSecond,
    double ProductionChecksPerSecond,
    string LastModeState,
    string LastRecoveryState)
{
    public double DetectionWorkPercent =>
        TotalWork.AverageMilliseconds <= 0
            ? 0
            : Math.Clamp(
                (ModeDetection.AverageMilliseconds +
                 RootRecovery.AverageMilliseconds) /
                TotalWork.AverageMilliseconds *
                100,
                0,
                100);
}

public sealed class DetectionBenchmarkService
{
    public const int DefaultSamples = 12;
    public const int ProductionPollMilliseconds = 450;

    private static readonly string[] ExpeditionActiveStates =
    [
        "defeat",
        "victory",
        "extract_confirm",
        "confirm",
        "checkpoint",
        "continue",
        "start",
        "reward",
    ];

    private readonly IRobloxAutomation _automation;

    public DetectionBenchmarkService(
        IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public async Task<DetectionBenchmarkResult> RunAsync(
        RobloxWindow window,
        IDetectorPack detector,
        DetectionBenchmarkMode mode,
        int samples = DefaultSamples,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detector);
        if (samples is < 3 or > 40)
        {
            throw new ArgumentOutOfRangeException(
                nameof(samples),
                "Benchmark samples must be between 3 and 40.");
        }

        ImageFrame warmup =
            _automation.CaptureClient(window);
        _ = DetectMode(detector, mode, warmup);
        _ = detector.RootRecoveryState(warmup);

        List<double> captureTimes = new(samples);
        List<double> modeTimes = new(samples);
        List<double> recoveryTimes = new(samples);
        List<double> totalTimes = new(samples);
        string lastModeState = "None";
        string lastRecoveryState = "None";

        for (int index = 0; index < samples; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch total = Stopwatch.StartNew();

            Stopwatch capture = Stopwatch.StartNew();
            ImageFrame frame =
                _automation.CaptureClient(window);
            capture.Stop();

            Stopwatch modeDetection = Stopwatch.StartNew();
            lastModeState =
                DetectMode(detector, mode, frame);
            modeDetection.Stop();

            Stopwatch recovery = Stopwatch.StartNew();
            lastRecoveryState =
                detector.RootRecoveryState(frame) ??
                "None";
            recovery.Stop();
            total.Stop();

            captureTimes.Add(
                capture.Elapsed.TotalMilliseconds);
            modeTimes.Add(
                modeDetection.Elapsed.TotalMilliseconds);
            recoveryTimes.Add(
                recovery.Elapsed.TotalMilliseconds);
            totalTimes.Add(
                total.Elapsed.TotalMilliseconds);

            if (index + 1 < samples)
            {
                await Task.Delay(
                    20,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        DetectionBenchmarkMetric totalMetric =
            Metric(totalTimes);
        double workRate =
            Rate(totalMetric.AverageMilliseconds);
        double productionRate =
            Rate(
                totalMetric.AverageMilliseconds +
                ProductionPollMilliseconds);
        return new DetectionBenchmarkResult(
            mode,
            samples,
            Metric(captureTimes),
            Metric(modeTimes),
            Metric(recoveryTimes),
            totalMetric,
            workRate,
            productionRate,
            lastModeState,
            lastRecoveryState);
    }

    private static string DetectMode(
        IDetectorPack detector,
        DetectionBenchmarkMode mode,
        ImageFrame frame) =>
        mode switch
        {
            DetectionBenchmarkMode.ExpeditionNavigation =>
                DetectExpedition(detector, frame),
            DetectionBenchmarkMode.ExpeditionMatch =>
                DetectExpeditionMatch(
                    detector,
                    frame),
            DetectionBenchmarkMode.ChallengeMatch =>
                ChallengeScreenDetector
                    .DetectMatchState(frame)
                    .State.ToString(),
            DetectionBenchmarkMode.StoryRaidMatch =>
                StageScreenDetector
                    .DetectMatchState(frame)
                    .State.ToString(),
            DetectionBenchmarkMode.EventMatch =>
                EventScreenDetector
                    .DetectMatchState(frame)
                    .State.ToString(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode)),
        };

    private static string DetectExpedition(
        IDetectorPack detector,
        ImageFrame frame)
    {
        IReadOnlyDictionary<string, double> scores =
            detector.ScoreStates(frame);
        return ExpeditionRunPolicy.PreferActiveState(
                detector.Manifest,
                scores,
                detector.Classify(scores)) ??
            "None";
    }

    private static string DetectExpeditionMatch(
        IDetectorPack detector,
        ImageFrame frame)
    {
        IReadOnlyDictionary<string, double> scores =
            detector.ScoreStates(
                frame,
                ExpeditionActiveStates);
        return ExpeditionRunPolicy.PreferActiveState(
                detector.Manifest,
                scores,
                detector.Classify(scores)) ??
            "None";
    }

    private static DetectionBenchmarkMetric Metric(
        IReadOnlyList<double> values)
    {
        double[] ordered =
            values.OrderBy(value => value).ToArray();
        int p95Index = Math.Clamp(
            (int)Math.Ceiling(ordered.Length * 0.95) - 1,
            0,
            ordered.Length - 1);
        return new DetectionBenchmarkMetric(
            ordered.Average(),
            ordered[p95Index]);
    }

    private static double Rate(
        double milliseconds) =>
        milliseconds <= 0
            ? 0
            : 1000 / milliseconds;
}
