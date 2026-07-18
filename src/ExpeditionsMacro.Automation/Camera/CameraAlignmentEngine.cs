using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision;

namespace ExpeditionsMacro.Automation.Camera;

public sealed class CameraAlignmentEngine
{
    private readonly IRobloxAutomation _automation;
    private readonly ICameraModelRepository _models;

    public CameraAlignmentEngine(IRobloxAutomation automation, ICameraModelRepository models)
    {
        _automation = automation;
        _models = models;
    }

    public async Task<CameraModel> CalibrateAsync(
        ScreenRegion selectedScreenRegion,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        settings.Validate();
        RobloxWindow window = RequireWindow();
        Focus(window);
        ClientBounds client = _automation.GetClientBounds(window);
        ScreenRegion relativeRegion = new(
            selectedScreenRegion.X - client.X,
            selectedScreenRegion.Y - client.Y,
            selectedScreenRegion.Width,
            selectedScreenRegion.Height);
        if (!relativeRegion.FitsWithin(client.Width, client.Height))
        {
            throw new InvalidOperationException("The comparison region must be completely inside the Roblox client area.");
        }

        ScreenRegion captureRegion = client.ToScreen(relativeRegion);
        progress?.Report(new MacroProgress("Camera setup", 3, $"Recorded Roblox client size {client.Width} × {client.Height} and relative region ({relativeRegion.X}, {relativeRegion.Y})."));
        List<ImageFrame> frames = [];
        int interval = settings.CaptureCount <= 1
            ? 0
            : (int)Math.Round(settings.CaptureDuration.TotalMilliseconds / (settings.CaptureCount - 1));
        for (int index = 0; index < settings.CaptureCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(_automation.CaptureScreen(captureRegion));
            progress?.Report(new MacroProgress("Camera setup", 5 + (int)Math.Round(15d * (index + 1) / settings.CaptureCount), $"Capturing goal example {index + 1} of {settings.CaptureCount}"));
            if (index + 1 < settings.CaptureCount) await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }

        (IReadOnlyList<ImageFrame> _, ImageFrame reference, double baseline) = VisionScorer.BuildReference(frames);
        progress?.Report(new MacroProgress("Camera setup", 21, $"Goal model ready. Baseline confidence: {baseline:P0}", Confidence: baseline));
        (int fullYawPixels, IReadOnlyList<double> scanScores, IReadOnlyList<ImageFrame> atlas) = await LearnFullTurnAsync(
            window,
            captureRegion,
            reference,
            baseline,
            settings,
            progress,
            cancellationToken).ConfigureAwait(false);
        double threshold = VisionScorer.ChooseSuccessThreshold(baseline, scanScores);
        string id = ModelId.FromName(settings.Name);
        CameraModelManifest manifest = new()
        {
            Id = id,
            Name = settings.Name.Trim(),
            Region = relativeRegion,
            ClientWidth = client.Width,
            ClientHeight = client.Height,
            BaselineScore = baseline,
            SuccessThreshold = threshold,
            CoarseStepPixels = settings.CoarseStepPixels,
            FineStepPixels = settings.FineStepPixels,
            FullYawPixels = fullYawPixels,
            SettleMilliseconds = settings.SettleMilliseconds,
            AtlasSampleCount = atlas.Count,
            ScanScores = scanScores,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        CameraModel model = new(manifest, reference, frames[0], atlas);
        await _models.SaveAsync(model, cancellationToken).ConfigureAwait(false);
        progress?.Report(new MacroProgress("Camera setup", 100, $"Setup complete. '{manifest.Name}' learned {fullYawPixels} mouse pixels per turn."));
        return model;
    }

    public async Task<double> AlignAsync(
        CameraModel model,
        RobloxWindow? existingWindow = null,
        bool restoreWindow = true,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        model.Manifest.Validate();
        RobloxWindow window = existingWindow ?? RequireWindow();
        Focus(window);
        WindowBounds original = _automation.GetWindowBounds(window);
        ClientBounds currentClient = _automation.GetClientBounds(window);
        bool resized = currentClient.Width != model.Manifest.ClientWidth || currentClient.Height != model.Manifest.ClientHeight;
        try
        {
            progress?.Report(new MacroProgress("Camera alignment", 2, "Starting camera alignment."));
            if (resized)
            {
                progress?.Report(new MacroProgress("Camera alignment", 4, $"Temporarily resizing Roblox to {model.Manifest.ClientWidth} × {model.Manifest.ClientHeight}."));
                await _automation.ResizeClientAsync(window, model.Manifest.ClientWidth, model.Manifest.ClientHeight, cancellationToken).ConfigureAwait(false);
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            ClientBounds client = _automation.GetClientBounds(window);
            if (client.Width != model.Manifest.ClientWidth || client.Height != model.Manifest.ClientHeight) throw new InvalidOperationException("Roblox does not match the client size stored by the camera model.");
            ScreenRegion region = client.ToScreen(model.Manifest.Region);
            double initial = await StableScoreAsync(model.Reference, region, 3, cancellationToken).ConfigureAwait(false);
            ImageFrame currentThumbnail = await CurrentThumbnailAsync(model.Reference, region, model.YawAtlas[0].Width, 2, cancellationToken).ConfigureAwait(false);
            double[] atlasScores = model.YawAtlas.Select(example => VisionScorer.RobustSimilarity(example, currentThumbnail)).ToArray();
            int bestIndex = Array.IndexOf(atlasScores, atlasScores.Max());
            int intervals = Math.Max(1, model.YawAtlas.Count - 1);
            int offset = (int)Math.Round((double)bestIndex * model.Manifest.FullYawPixels / intervals) % model.Manifest.FullYawPixels;
            int coarsePixels;
            int direction;
            string directionLabel;
            if (offset <= model.Manifest.FullYawPixels / 2)
            {
                direction = -1;
                directionLabel = "left";
                coarsePixels = offset;
            }
            else
            {
                direction = 1;
                directionLabel = "right";
                coarsePixels = model.Manifest.FullYawPixels - offset;
            }
            progress?.Report(new MacroProgress("Camera alignment", 35, $"Yaw atlas match: {atlasScores[bestIndex]:P0}. Coarse correction: {coarsePixels} px {directionLabel}.", Confidence: atlasScores[bestIndex]));
            if (coarsePixels > model.Manifest.FineStepPixels)
            {
                await MoveAsync(window, direction * coarsePixels, model.Manifest.SettleMilliseconds, model.Manifest.CoarseStepPixels, cancellationToken).ConfigureAwait(false);
            }
            double coarse = await StableScoreAsync(model.Reference, region, 2, cancellationToken).ConfigureAwait(false);
            double refined = await RefineAsync(window, model, region, coarse, progress, cancellationToken).ConfigureAwait(false);
            string message = refined >= model.Manifest.SuccessThreshold
                ? $"Aligned with {refined:P0} confidence."
                : $"Best view reached, but confidence is low ({refined:P0}). Recheck position, zoom, and pitch.";
            progress?.Report(new MacroProgress("Camera alignment", 100, message, Confidence: refined));
            return refined;
        }
        finally
        {
            if (resized && restoreWindow) _automation.RestoreWindowBounds(window, original);
        }
    }

    private async Task<(int FullYawPixels, IReadOnlyList<double> Scores, IReadOnlyList<ImageFrame> Atlas)> LearnFullTurnAsync(
        RobloxWindow window,
        ScreenRegion region,
        ImageFrame reference,
        double baseline,
        CameraCalibrationSettings settings,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<double> scores = [baseline];
        List<ImageFrame> atlas = [VisionScorer.MakeThumbnail(reference)];
        bool departed = false;
        double returnLevel = Math.Max(0.68, baseline - 0.075);
        int scanned = 0;
        progress?.Report(new MacroProgress("Camera setup", 23, "Learning one full yaw turn with right-mouse drags."));
        for (int step = 1; step <= settings.MaximumSamples; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MoveAsync(window, settings.CoarseStepPixels, settings.SettleMilliseconds, settings.CoarseStepPixels, cancellationToken).ConfigureAwait(false);
            scanned = step;
            ImageFrame prepared = VisionScorer.PrepareGray(_automation.CaptureScreen(region), reference.Width, reference.Height);
            double score = VisionScorer.RobustSimilarity(reference, prepared);
            atlas.Add(VisionScorer.MakeThumbnail(prepared));
            scores.Add(score);
            progress?.Report(new MacroProgress("Camera setup", 23 + (int)Math.Round(70d * step / settings.MaximumSamples), $"Learning yaw sample {step}. Confidence: {score:P0}", Confidence: score));
            if (score < baseline - 0.09) departed = true;
            int minimumSteps = Math.Max(12, (int)Math.Round(180d / Math.Max(1, settings.CoarseStepPixels)));
            if (departed && step >= minimumSteps && score >= returnLevel) return (step * settings.CoarseStepPixels, scores, atlas);
            if (departed && scores.Count >= 4 && step - 1 >= minimumSteps)
            {
                double previous = scores[^2];
                if (previous >= returnLevel - 0.02 && previous > scores[^3] && previous > score)
                {
                    await MoveAsync(window, -settings.CoarseStepPixels, settings.SettleMilliseconds, settings.CoarseStepPixels, cancellationToken).ConfigureAwait(false);
                    scores.RemoveAt(scores.Count - 1);
                    atlas.RemoveAt(atlas.Count - 1);
                    return ((step - 1) * settings.CoarseStepPixels, scores, atlas);
                }
            }
        }
        progress?.Report(new MacroProgress("Camera setup", 94, "A complete turn was not recognized. Returning toward the goal."));
        if (scanned > 0) await MoveAsync(window, -scanned * settings.CoarseStepPixels, settings.SettleMilliseconds, settings.CoarseStepPixels, cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException("Could not recognize a full yaw turn. Increase maximum samples, reduce coarse drag pixels, or increase settle time, then retry.");
    }

    private async Task<double> RefineAsync(
        RobloxWindow window,
        CameraModel model,
        ScreenRegion region,
        double startingScore,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MacroProgress("Camera alignment", 72, "Refining with micro mouse drags."));
        int fine = Math.Max(1, model.Manifest.FineStepPixels);
        double best = startingScore;
        await MoveAsync(window, -fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        double left = Score(model.Reference, region);
        await MoveAsync(window, fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        await MoveAsync(window, fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        double right = Score(model.Reference, region);
        await MoveAsync(window, -fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        int direction = 0;
        double neighbor = best;
        if (left > neighbor + 0.003) { direction = -1; neighbor = left; }
        if (right > neighbor + 0.003) { direction = 1; neighbor = right; }
        if (direction == 0) return best;
        await MoveAsync(window, direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
        best = neighbor;
        int maximum = Math.Max(8, model.Manifest.CoarseStepPixels * 2 / fine);
        for (int index = 0; index < maximum; index++)
        {
            await MoveAsync(window, direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
            double score = Score(model.Reference, region);
            if (score <= best + 0.0015)
            {
                await MoveAsync(window, -direction * fine, model.Manifest.SettleMilliseconds, fine, cancellationToken).ConfigureAwait(false);
                break;
            }
            best = score;
        }
        return best;
    }

    private async Task<ImageFrame> CurrentThumbnailAsync(ImageFrame reference, ScreenRegion region, int width, int count, CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(VisionScorer.PrepareGray(_automation.CaptureScreen(region), reference.Width, reference.Height));
            if (index + 1 < count) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        return VisionScorer.MakeThumbnail(VisionScorer.Median(frames), width);
    }

    private async Task<double> StableScoreAsync(ImageFrame reference, ScreenRegion region, int count, CancellationToken cancellationToken)
    {
        List<double> scores = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scores.Add(Score(reference, region));
            if (index + 1 < count) await Task.Delay(60, cancellationToken).ConfigureAwait(false);
        }
        double[] sorted = scores.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private double Score(ImageFrame reference, ScreenRegion region) => VisionScorer.ScoreFrame(reference, _automation.CaptureScreen(region));

    private async Task MoveAsync(RobloxWindow window, int horizontalPixels, int settleMilliseconds, int chunkPixels, CancellationToken cancellationToken)
    {
        await _automation.DragCameraAsync(window, horizontalPixels, 0, chunkPixels, cancellationToken).ConfigureAwait(false);
        await Task.Delay(settleMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    private RobloxWindow RequireWindow() => _automation.FindWindow() ?? throw new InvalidOperationException("No visible Roblox window was found.");

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException($"Found '{window.Title}', but Windows could not focus it. Restore Roblox and try again.");
    }
}
