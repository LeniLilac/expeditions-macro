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
    private async Task<IReadOnlyList<FineYawReference>>
        LearnDenseFineYawNeighborhoodAsync(
            RobloxWindow window,
            IReadOnlyList<ScreenRegion> regions,
            ImageFrame reference,
            ImageFrame denseReference,
            CameraCalibrationSettings settings,
            IProgress<MacroProgress>? progress,
            CancellationToken cancellationToken)
    {
        int stride = settings.FineStepPixels;
        int radius = Math.Max(stride * 2, settings.FineSearchPixels);
        radius = (int)Math.Ceiling((double)radius / stride) * stride;
        int expectedCount = radius * 2 / stride + 1;
        Dictionary<int, FineYawReference> references = [];
        double observedZeroScore = 0;
        CameraYawAtlasIndex.CameraYawFingerprint referenceFingerprint =
            CameraYawAtlasIndex.CameraYawFingerprint.Create(
                denseReference);

        progress?.Report(new MacroProgress(
            "Camera setup",
            22,
            $"Sweeping the fine goal neighborhood from {-radius} to +{radius} mouse pixels."));
        await _automation.CaptureCameraFineYawSweepAsync(
            window,
            radius,
            stride,
            sample =>
            {
                ImageFrame thumbnail = CameraDenseThumbnailBuilder.Build(
                    sample.Frame,
                    regions);
                double score = sample.Offset == 0
                    ? VisionScorer.RobustSimilarity(
                        denseReference,
                        thumbnail)
                    : referenceFingerprint.Similarity(
                        CameraYawAtlasIndex.CameraYawFingerprint.Create(
                            thumbnail));
                references[sample.Offset] = new FineYawReference(
                    sample.Offset,
                    thumbnail);
                if (sample.Offset == 0) observedZeroScore = score;
                progress?.Report(new MacroProgress(
                    "Camera setup",
                    22 + (int)Math.Round(
                        8d * references.Count / expectedCount),
                    $"Fine yaw {sample.Offset:+#;-#;0} px: {score:P0}.",
                    Confidence: score));
            },
            cancellationToken).ConfigureAwait(false);

        if (references.Count != expectedCount ||
            !references.ContainsKey(0))
        {
            throw new InvalidOperationException(
                $"Fine camera sweep returned {references.Count} of {expectedCount} required yaw positions.");
        }

        (_, double returnedScore) = await StablePreparedScoreAsync(
            reference,
            window,
            regions,
            2,
            cancellationToken).ConfigureAwait(false);
        ImageFrame returnedZero = await StableDenseThumbnailAsync(
            window,
            regions,
            denseReference.Width,
            cancellationToken).ConfigureAwait(false);
        double returnedFingerprint =
            referenceFingerprint.Similarity(
                CameraYawAtlasIndex.CameraYawFingerprint.Create(
                    returnedZero));
        if (returnedScore < FineInputConsistencyThreshold ||
            returnedFingerprint <
                DenseYawLoopPolicy.FingerprintThreshold)
        {
            throw new InvalidOperationException(
                $"Roblox did not return the fine camera sweep to zero consistently (goal {returnedScore:P0}, fingerprint {returnedFingerprint:P0}). Keep Roblox focused and retry camera setup.");
        }

        references[0] = new FineYawReference(0, returnedZero);
        if (observedZeroScore < FineInputConsistencyThreshold)
        {
            progress?.Report(new MacroProgress(
                "Camera setup",
                30,
                $"Ignored one transient moving zero frame ({observedZeroScore:P0}); the stationary return verified at {returnedScore:P0}.",
                Confidence: returnedScore));
        }

        FineYawReference[] result = references
            .Values
            .OrderBy(item => item.Offset)
            .ToArray();
        progress?.Report(new MacroProgress(
            "Camera setup",
            31,
            $"Fine neighborhood ready with {result.Length} positions; starting the dense yaw sweep."));
        return result;
    }

    private async Task<ImageFrame> StableDenseThumbnailAsync(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions,
        int width,
        CancellationToken cancellationToken)
    {
        ImageFrame[] frames = new ImageFrame[2];
        for (int index = 0; index < frames.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames[index] = CameraDenseThumbnailBuilder.Build(
                _automation.CaptureClient(window),
                regions,
                width);
            if (index + 1 < frames.Length)
            {
                await Task.Delay(
                    60,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        return VisionScorer.Median(frames);
    }
}
