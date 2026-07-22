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
    private const int MaximumStableSceneSamples = 24;

    public async Task<ImageFrame> WaitAsync(
        RobloxWindow window,
        CameraModel model,
        int attempt,
        int maximumAttempts,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        ImageFrame previous = CaptureThumbnail(window, model);
        int stable = 0;
        double similarity = 0;
        int delay = Math.Max(75, model.Manifest.SettleMilliseconds);
        for (int sample = 1; sample <= MaximumStableSceneSamples; sample++)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            ImageFrame current = CaptureThumbnail(window, model, previous.Width);
            similarity = VisionScorer.RobustSimilarity(previous, current);
            stable = similarity >= StableSceneSimilarity ? stable + 1 : 0;
            previous = current;
            if (stable < StableSceneFrames) continue;

            progress?.Report(new MacroProgress(
                "Camera alignment",
                12,
                $"Attempt {attempt}/{maximumAttempts}: rendered map is stable ({similarity:P0}).",
                Confidence: similarity));
            return previous;
        }

        progress?.Report(new MacroProgress(
            "Camera alignment",
            12,
            $"Attempt {attempt}/{maximumAttempts}: the scene remained animated ({similarity:P0}); continuing with median observations.",
            Confidence: similarity));
        return previous;
    }

    private ImageFrame CaptureThumbnail(RobloxWindow window, CameraModel model, int width = 160) =>
        VisionScorer.MakeThumbnail(
            CameraRegionAnalyzer.BuildComposite(automation.CaptureClient(window), model.Manifest.Regions),
            width);
}
