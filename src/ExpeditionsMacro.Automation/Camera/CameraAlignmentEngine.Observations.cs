using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision;
using ExpeditionsMacro.Vision.Camera;

namespace ExpeditionsMacro.Automation.Camera;

public sealed partial class CameraAlignmentEngine
{
    private async Task<AlignmentObservation> StableObservationAsync(
        CameraModel model,
        RobloxWindow window,
        int count,
        CancellationToken cancellationToken)
    {
        List<ImageFrame> composites = [];
        List<ImageFrame> denseThumbnails = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame fullClient = _automation.CaptureClient(window);
            composites.Add(CameraRegionAnalyzer.BuildComposite(
                fullClient,
                model.Manifest.Regions));
            if (model.Manifest.YawAtlasKind ==
                CameraYawAtlasKind.DenseSweep)
            {
                denseThumbnails.Add(
                    CameraDenseThumbnailBuilder.Build(
                        fullClient,
                        model.Manifest.Regions,
                        model.FineYawAtlas[0].Width));
            }
            if (index + 1 < count)
            {
                await Task.Delay(
                    60,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        ImageFrame stable = VisionScorer.Median(composites);
        ImageFrame thumbnail = denseThumbnails.Count > 0
            ? VisionScorer.Median(denseThumbnails)
            : VisionScorer.MakeThumbnail(
                stable,
                model.FineYawAtlas[0].Width);
        return new AlignmentObservation(
            CameraRegisteredScorer
                .ScoreComposite(model.Reference, stable)
                .Score,
            BestFineYawMatch(model, thumbnail));
    }

    private static FineYawMatch BestFineYawMatch(
        CameraModel model,
        ImageFrame currentThumbnail)
    {
        int bestIndex = 0;
        double bestScore = double.NegativeInfinity;
        for (int index = 0;
             index < model.FineYawAtlas.Count;
             index++)
        {
            double score = CameraRegisteredScorer.Score(
                model.FineYawAtlas[index],
                currentThumbnail).Score;
            if (score <= bestScore) continue;
            bestIndex = index;
            bestScore = score;
        }
        return new FineYawMatch(
            model.Manifest.FineYawOffsets[bestIndex],
            bestScore);
    }

    private static AtlasMatch BestAtlasMatch(
        CameraModel model,
        ImageFrame current)
    {
        IReadOnlyList<ImageFrame> atlas = model.YawAtlas;
        if (atlas.Count == 0)
        {
            throw new ArgumentException(
                "The yaw atlas is empty.",
                nameof(atlas));
        }
        if (model.Manifest.YawAtlasKind ==
            CameraYawAtlasKind.DenseSweep)
        {
            CameraYawAtlasMatch match =
                CameraYawAtlasIndex.For(atlas).FindBest(current);
            return new AtlasMatch(
                match.Index,
                match.Score,
                match.FingerprintScore,
                match.FingerprintIsolation);
        }

        double[] raw = atlas
            .Select(frame =>
                VisionScorer.RobustSimilarity(frame, current))
            .ToArray();
        int[] candidates = raw
            .Select((score, index) => (Score: score, Index: index))
            .OrderByDescending(item => item.Score)
            .Take(Math.Min(8, atlas.Count))
            .Select(item => item.Index)
            .ToArray();
        AtlasMatch best = new(
            candidates[0],
            double.NegativeInfinity);
        foreach (int index in candidates)
        {
            double score =
                CameraRegisteredScorer.Score(
                    atlas[index],
                    current).Score;
            if (score > best.Score)
            {
                best = new AtlasMatch(index, score);
            }
        }
        return best;
    }

    private async Task<ImageFrame> CurrentThumbnailAsync(
        CameraModel model,
        RobloxWindow window,
        int width,
        int count,
        CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(
                model.Manifest.YawAtlasKind ==
                CameraYawAtlasKind.DenseSweep
                    ? CameraDenseThumbnailBuilder.Build(
                        _automation.CaptureClient(window),
                        model.Manifest.Regions,
                        width)
                    : CaptureComposite(
                        window,
                        model.Manifest.Regions));
            if (index + 1 < count)
            {
                await Task.Delay(
                    60,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        ImageFrame stable = VisionScorer.Median(frames);
        return model.Manifest.YawAtlasKind ==
            CameraYawAtlasKind.DenseSweep
                ? stable
                : VisionScorer.MakeThumbnail(stable, width);
    }

    private async Task<double> StableScoreAsync(
        CameraModel model,
        RobloxWindow window,
        int count,
        CancellationToken cancellationToken)
    {
        List<double> scores = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scores.Add(Score(model, window));
            if (index + 1 < count)
            {
                await Task.Delay(
                    60,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        double[] sorted = scores.Order().ToArray();
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] +
               sorted[sorted.Length / 2]) / 2;
    }

    private async Task<(ImageFrame Frame, double Score)>
        StablePreparedScoreAsync(
            ImageFrame reference,
            RobloxWindow window,
            IReadOnlyList<ScreenRegion> regions,
            int count,
            CancellationToken cancellationToken)
    {
        List<ImageFrame> frames = [];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(CaptureComposite(window, regions));
            if (index + 1 < count)
            {
                await Task.Delay(
                    60,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        ImageFrame stable = VisionScorer.Median(frames);
        return (
            stable,
            VisionScorer.RobustSimilarity(reference, stable));
    }

    private ImageFrame CaptureComposite(
        RobloxWindow window,
        IReadOnlyList<ScreenRegion> regions) =>
        CameraRegionAnalyzer.BuildComposite(
            _automation.CaptureClient(window),
            regions);

    private double Score(
        CameraModel model,
        RobloxWindow window) =>
        GoalEvidence(model, _automation.CaptureClient(window));
}
