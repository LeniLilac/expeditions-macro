using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;

namespace ExpeditionsMacro.Automation.Camera;

public sealed partial class CameraAlignmentEngine
{
    private async Task<IReadOnlyList<FineYawReference>> LearnFineYawNeighborhoodAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        ImageFrame reference,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        int fineStep = settings.FineStepPixels;
        int sampleStride = fineStep;
        int radius = Math.Max(fineStep * 2, settings.FineSearchPixels);
        radius = (int)Math.Ceiling((double)radius / sampleStride) * sampleStride;
        int sampleCount = radius * 2 / sampleStride + 1;
        Dictionary<int, FineYawReference> references = new()
        {
            [0] = new FineYawReference(0, VisionScorer.MakeThumbnail(reference)),
        };
        int currentOffset = 0;
        double observedZeroScore = 0;
        progress?.Report(new MacroProgress(
            "Camera setup",
            22,
            $"Learning a fine goal neighborhood from {-radius} to +{radius} mouse pixels."));

        try
        {
            await MoveMouseAsync(window, -radius, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
            currentOffset = -radius;
            for (int index = 0; index < sampleCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index > 0)
                {
                    await MoveMouseAsync(window, sampleStride, settings.SettleMilliseconds, fineStep, cancellationToken).ConfigureAwait(false);
                    currentOffset += sampleStride;
                }

                (ImageFrame frame, double score) =
                    await StablePreparedScoreAsync(reference, window, regions, 1, cancellationToken).ConfigureAwait(false);
                references[currentOffset] = new FineYawReference(
                    currentOffset,
                    VisionScorer.MakeThumbnail(frame));
                if (currentOffset == 0)
                {
                    observedZeroScore = score;
                }

                progress?.Report(new MacroProgress(
                    "Camera setup",
                    22 + (int)Math.Round(7d * (index + 1) / sampleCount),
                    $"Fine goal view {currentOffset:+#;-#;0} px. Confidence: {score:P0}",
                    Confidence: score));
            }
        }
        finally
        {
            if (currentOffset != 0)
            {
                await MoveMouseAsync(
                    window,
                    -currentOffset,
                    settings.SettleMilliseconds,
                    fineStep,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        (_, double returnedScore) = await StablePreparedScoreAsync(
            reference,
            window,
            regions,
            2,
            cancellationToken).ConfigureAwait(false);
        if (observedZeroScore < FineInputConsistencyThreshold ||
            returnedScore < FineInputConsistencyThreshold)
        {
            throw new InvalidOperationException(
                $"Roblox did not apply fine camera input consistently (observed zero {observedZeroScore:P0}, returned zero {returnedScore:P0}). Increase Settle time, keep Roblox focused, and retry camera setup.");
        }

        FineYawReference[] result = references.Values.OrderBy(item => item.Offset).ToArray();
        progress?.Report(new MacroProgress(
            "Camera setup",
            30,
            $"Fine goal neighborhood ready with {result.Length} distinct yaw views; starting the coarse turn."));
        return result;
    }

    private static FineYawMatch BestFineYawMatch(
        IReadOnlyList<FineYawReference> references,
        ImageFrame currentThumbnail)
    {
        FineYawReference best = references[0];
        double bestScore = double.NegativeInfinity;
        foreach (FineYawReference candidate in references)
        {
            double score = VisionScorer.RobustSimilarity(candidate.Thumbnail, currentThumbnail);
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }

        return new FineYawMatch(best.Offset, bestScore);
    }
}
