using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

internal sealed class CameraSceneStabilizer(IRobloxAutomation automation)
{
    private const double StableSceneSimilarity = 0.96;
    private const int StableSceneFrames = 5;
    private const int MaximumSceneWaitMilliseconds = 12000;

    public async Task<ImageFrame> WaitAsync(
        RobloxWindow window,
        CameraModel model,
        int attempt,
        int maximumAttempts,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ImageFrame previous = CaptureComposite(window, model);
        CameraWorldReadinessResult readiness =
            CameraWorldReadiness.Evaluate(model.Reference, previous);
        bool rendered = readiness.IsReady;
        int stable = 0;
        double similarity = 0;
        int delay = Math.Max(75, model.Manifest.SettleMilliseconds);
        int maximumSamples = Math.Max(
            3,
            (int)Math.Ceiling(
                (double)MaximumSceneWaitMilliseconds / delay));
        for (int sample = 1; sample <= maximumSamples; sample++)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            ImageFrame current = CaptureComposite(window, model);
            readiness = CameraWorldReadiness.Evaluate(
                model.Reference,
                current);
            if (!readiness.IsReady)
            {
                stable = 0;
                previous = current;
                continue;
            }

            if (!rendered)
            {
                rendered = true;
                stable = 0;
                previous = current;
                continue;
            }

            ImageFrame previousThumbnail =
                VisionScorer.MakeThumbnail(previous, 160);
            ImageFrame currentThumbnail =
                VisionScorer.MakeThumbnail(current, 160);
            similarity = VisionScorer.RobustSimilarity(
                previousThumbnail,
                currentThumbnail);
            stable = similarity >= StableSceneSimilarity ? stable + 1 : 0;
            previous = current;
            if (stable < StableSceneFrames) continue;

            progress?.Report(new MacroProgress(
                "Camera alignment",
                12,
                $"Attempt {attempt}/{maximumAttempts}: rendered map is stable ({similarity:P0}).",
                Confidence: similarity));
            return VisionScorer.MakeThumbnail(previous, 160);
        }

        if (!rendered)
        {
            progress?.Report(new MacroProgress(
                "Camera alignment",
                12,
                $"Attempt {attempt}/{maximumAttempts}: stage geometry is still missing; retrying the stage without moving the camera.",
                Confidence: readiness.CurrentTexture));
            throw new CameraWorldNotRenderedException(
                readiness.CurrentTexture,
                attempt);
        }

        progress?.Report(new MacroProgress(
            "Camera alignment",
            12,
            $"Attempt {attempt}/{maximumAttempts}: the scene remained animated ({similarity:P0}); continuing with median observations.",
            Confidence: similarity));
        return VisionScorer.MakeThumbnail(previous, 160);
    }

    private ImageFrame CaptureComposite(
        RobloxWindow window,
        CameraModel model) =>
        CameraRegionAnalyzer.BuildComposite(
            automation.CaptureClient(window),
            model.Manifest.Regions);
}
