using System.Diagnostics;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

public sealed partial class CameraAlignmentEngine
{
    private const int DenseYawSampleIntervalMilliseconds = 16;
    private static readonly TimeSpan DenseYawMaximumDuration =
        TimeSpan.FromSeconds(7.25);

    private sealed record YawCalibrationResult(
        CameraYawAtlasKind Kind,
        int FullYawSteps,
        int TurnMilliseconds,
        IReadOnlyList<double> ScanScores,
        IReadOnlyList<ImageFrame> Atlas);

    private sealed record DenseYawSample(
        TimeSpan Elapsed,
        ImageFrame Thumbnail);

    private async Task<YawCalibrationResult> LearnDenseYawAtlasAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        ImageFrame denseReference,
        double baseline,
        IReadOnlyList<FineYawReference> goalNeighborhood,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ImageFrame goalThumbnail = denseReference;
        CameraYawAtlasIndex.CameraYawFingerprint goalFingerprint =
            CameraYawAtlasIndex.CameraYawFingerprint.Create(goalThumbnail);
        List<DenseYawSample> samples = [];
        bool departed = false;
        bool fingerprintDeparted = false;
        TimeSpan turnElapsed = TimeSpan.Zero;
        double exactReturnThreshold = Math.Max(0.86, baseline - 0.04);
        Stopwatch sweepTimer = Stopwatch.StartNew();

        progress?.Report(new MacroProgress(
            "Camera setup",
            32,
            "Holding Right Arrow once while sampling a dense visual yaw atlas."));
        int captureLimit = Math.Max(
            settings.MaximumSamples,
            (int)Math.Ceiling(
                DenseYawMaximumDuration.TotalMilliseconds /
                DenseYawSampleIntervalMilliseconds) + 1);
        await _automation.CaptureCameraYawSweepAsync(
            window,
            CameraYawDirection.Right,
            DenseYawMaximumDuration,
            captureLimit,
            DenseYawSampleIntervalMilliseconds,
            sample =>
            {
                ImageFrame thumbnail =
                    CameraDenseThumbnailBuilder.Build(
                        sample.Frame,
                        regions);
                samples.Add(new DenseYawSample(
                    sample.Elapsed,
                    thumbnail));
                CameraYawAtlasIndex.CameraYawFingerprint fingerprint =
                    CameraYawAtlasIndex.CameraYawFingerprint.Create(thumbnail);
                double fingerprintScore =
                    goalFingerprint.Similarity(fingerprint);
                if (fingerprintScore < 0.89)
                {
                    fingerprintDeparted = true;
                }

                if (!departed &&
                    (fingerprintScore < 0.89 ||
                     samples.Count % 6 == 0 &&
                     VisionScorer.RobustSimilarity(
                         goalThumbnail,
                         thumbnail) < baseline - 0.10))
                {
                    departed = true;
                }

                bool canReturn =
                    departed &&
                    sample.Elapsed >= TimeSpan.FromSeconds(1.6) &&
                    samples.Count >= 49;
                double evidence = fingerprintScore;
                bool fingerprintCandidate =
                    canReturn &&
                    fingerprintDeparted &&
                    fingerprintScore >=
                        DenseYawLoopPolicy.FingerprintThreshold;
                bool exactProbe =
                    canReturn &&
                    !fingerprintDeparted &&
                    samples.Count % 6 == 0;
                if (fingerprintCandidate || exactProbe)
                {
                    FineYawMatch fineMatch = fingerprintCandidate
                        ? DenseFineYawMatcher.FindBest(
                            goalNeighborhood,
                            thumbnail)
                        : new FineYawMatch(0, 0);
                    bool fineNeighborhoodReturn =
                        DenseYawLoopPolicy.IsFineNeighborhoodReturn(
                            fingerprintScore,
                            fineMatch.Score);
                    double direct =
                        !fineNeighborhoodReturn &&
                        (exactProbe ||
                         fingerprintScore >= 0.995)
                            ? CameraRegisteredScorer.Score(
                                goalThumbnail,
                                thumbnail).Score
                            : 0;
                    evidence = fineNeighborhoodReturn
                        ? Math.Min(
                            fingerprintScore,
                            fineMatch.Score)
                        : direct;
                    if (DenseYawLoopPolicy.IsReturn(
                            fingerprintScore,
                            direct,
                            fineMatch.Score,
                            exactReturnThreshold))
                    {
                        turnElapsed = sample.Elapsed;
                    }
                }

                if (samples.Count % 6 == 0 || turnElapsed > TimeSpan.Zero)
                {
                    int percent = 32 + (int)Math.Round(
                        42d * Math.Min(
                            1,
                            sample.Elapsed.TotalMilliseconds /
                            DenseYawMaximumDuration.TotalMilliseconds));
                    progress?.Report(new MacroProgress(
                        "Camera setup",
                        percent,
                        $"Dense yaw frame {samples.Count}; elapsed {sample.Elapsed.TotalSeconds:F1}s ({sweepTimer.Elapsed.TotalSeconds:F1}s processing); loop evidence {evidence:P0}.",
                        Confidence: evidence));
                }
                return turnElapsed == TimeSpan.Zero;
            },
            cancellationToken).ConfigureAwait(false);

        if (turnElapsed == TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The dense yaw sweep did not recognize a complete turn within 7.25 seconds. Keep Roblox focused and retry setup.");
        }

        ImageFrame[] atlas = ResampleDenseAtlas(
            samples,
            goalThumbnail,
            turnElapsed,
            settings.MaximumSamples);
        int provisionalFullYawSteps = Math.Clamp(
            (int)Math.Round(
                turnElapsed.TotalMilliseconds /
                settings.ArrowHoldMilliseconds),
            12,
            500);
        await RestoreDenseGoalAsync(
            window,
            regions,
            reference,
            goalNeighborhood,
            atlas,
            provisionalFullYawSteps,
            settings,
            progress,
            "dense sweep release",
            progressPercent: 76,
            cancellationToken).ConfigureAwait(false);

        double[] scores = atlas
            .Select(frame =>
            {
                double fingerprint = goalFingerprint.Similarity(
                    CameraYawAtlasIndex.CameraYawFingerprint.Create(
                        frame));
                return baseline * Math.Pow(fingerprint, 4);
            })
            .ToArray();
        scores[0] = baseline;
        scores[^1] = baseline;
        int fullYawSteps = await CalibrateDensePulseScaleAsync(
            window,
            regions,
            reference,
            goalNeighborhood,
            atlas,
            turnElapsed,
            settings,
            progress,
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new MacroProgress(
            "Camera setup",
            93,
            $"Dense atlas ready: {atlas.Length - 1} positions over {turnElapsed.TotalSeconds:F2}s; one turn equals {fullYawSteps} arrow pulses."));
        return new YawCalibrationResult(
            CameraYawAtlasKind.DenseSweep,
            fullYawSteps,
            (int)Math.Round(turnElapsed.TotalMilliseconds),
            scores,
            atlas);
    }

    private static ImageFrame[] ResampleDenseAtlas(
        IReadOnlyList<DenseYawSample> samples,
        ImageFrame goalThumbnail,
        TimeSpan turnElapsed,
        int maximumSamples)
    {
        DenseYawSample[] withinTurn = samples
            .Where(sample => sample.Elapsed <= turnElapsed)
            .OrderBy(sample => sample.Elapsed)
            .ToArray();
        if (withinTurn.Length < 49)
        {
            throw new InvalidOperationException(
                $"The dense yaw sweep captured only {withinTurn.Length} usable frames; at least 49 are required.");
        }
        int targetCount = Math.Min(
            withinTurn.Length + 1,
            maximumSamples);
        ImageFrame[] atlas = new ImageFrame[targetCount];
        atlas[0] = goalThumbnail.Clone();
        int sourceIndex = 0;
        for (int index = 1; index < targetCount - 1; index++)
        {
            double targetMilliseconds =
                turnElapsed.TotalMilliseconds * index /
                (targetCount - 1);
            while (sourceIndex + 1 < withinTurn.Length &&
                   Math.Abs(
                       withinTurn[sourceIndex + 1]
                           .Elapsed.TotalMilliseconds -
                       targetMilliseconds) <=
                   Math.Abs(
                       withinTurn[sourceIndex]
                           .Elapsed.TotalMilliseconds -
                       targetMilliseconds))
            {
                sourceIndex++;
            }
            atlas[index] = withinTurn[sourceIndex].Thumbnail.Clone();
        }
        atlas[^1] = goalThumbnail.Clone();
        return atlas;
    }
}
