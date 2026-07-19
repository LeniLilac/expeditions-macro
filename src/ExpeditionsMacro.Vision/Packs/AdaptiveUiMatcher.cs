using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Packs;

internal readonly record struct AdaptiveRegionMatch(
    double Score,
    double Correlation,
    ScreenRegion SourceRegion,
    ScreenRegion MatchedRegion)
{
    public double ScaleX => (double)MatchedRegion.Width / SourceRegion.Width;

    public double ScaleY => (double)MatchedRegion.Height / SourceRegion.Height;

    public (int X, int Y) MapPoint(int x, int y) =>
        (
            MatchedRegion.X + (int)Math.Round((x - SourceRegion.X) * ScaleX, MidpointRounding.AwayFromZero),
            MatchedRegion.Y + (int)Math.Round((y - SourceRegion.Y) * ScaleY, MidpointRounding.AwayFromZero));

    public ScreenRegion MapRegion(ScreenRegion region)
    {
        (int x, int y) = MapPoint(region.X, region.Y);
        int width = Math.Max(1, (int)Math.Round(region.Width * ScaleX, MidpointRounding.AwayFromZero));
        int height = Math.Max(1, (int)Math.Round(region.Height * ScaleY, MidpointRounding.AwayFromZero));
        return new ScreenRegion(x, y, width, height);
    }
}

internal static class AdaptiveUiMatcher
{
    private static readonly double[] Scales = [0.85, 0.90, 0.95, 1.00, 1.05, 1.10, 1.15];

    private readonly record struct Candidate(double Correlation, ScreenRegion Region);

    public static AdaptiveRegionMatch Find(
        ImageFrame reference,
        ImageFrame image,
        ScreenRegion sourceRegion,
        int horizontalRadius = 48,
        int verticalRadius = 40,
        IReadOnlyList<double>? requestedScales = null)
    {
        if (reference.Format != PixelFormat.Gray8) throw new ArgumentException("Adaptive UI references must be grayscale.", nameof(reference));
        if (image.Format != PixelFormat.Rgb24) throw new ArgumentException("Adaptive UI input must be RGB.", nameof(image));
        IReadOnlyList<double> scales = requestedScales ?? Scales;
        if (scales.Count == 0 || scales.Any(scale => scale <= 0)) throw new ArgumentException("Adaptive UI scales must be positive.", nameof(requestedScales));

        List<Candidate> candidates = [];
        if (sourceRegion.FitsWithin(image.Width, image.Height)) candidates.Add(new Candidate(-1, sourceRegion));

        int maximumWidth = (int)Math.Ceiling(reference.Width * scales.Max());
        int maximumHeight = (int)Math.Ceiling(reference.Height * scales.Max());
        int searchLeft = Math.Max(0, sourceRegion.X - horizontalRadius - Math.Max(0, maximumWidth - sourceRegion.Width));
        int searchTop = Math.Max(0, sourceRegion.Y - verticalRadius - Math.Max(0, maximumHeight - sourceRegion.Height));
        int searchRight = Math.Min(image.Width, sourceRegion.Right + horizontalRadius + Math.Max(0, maximumWidth - sourceRegion.Width));
        int searchBottom = Math.Min(image.Height, sourceRegion.Bottom + verticalRadius + Math.Max(0, maximumHeight - sourceRegion.Height));
        ScreenRegion searchRegion = new(searchLeft, searchTop, searchRight - searchLeft, searchBottom - searchTop);

        ImageFrame preparedSearch = VisionScorer.PrepareGray(image.Crop(searchRegion), maximumWidth: int.MaxValue);
        using Mat search = ImageCodec.ToMat(preparedSearch);
        using Mat referenceMat = ImageCodec.ToMat(reference);
        foreach (double requestedScale in scales)
        {
            int width = Math.Max(8, (int)Math.Round(reference.Width * requestedScale));
            int height = Math.Max(8, (int)Math.Round(reference.Height * requestedScale));
            if (width > search.Width || height > search.Height) continue;

            using Mat template = new();
            Cv2.Resize(referenceMat, template, new Size(width, height), 0, 0, InterpolationFlags.Linear);
            using Mat result = new();
            Cv2.MatchTemplate(search, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double correlation, out _, out Point location);
            candidates.Add(new Candidate(
                Math.Clamp(correlation, -1, 1),
                new ScreenRegion(searchRegion.X + location.X, searchRegion.Y + location.Y, width, height)));
        }

        Candidate[] finalists = candidates
            .OrderByDescending(candidate => candidate.Correlation)
            .Take(2)
            .Concat(candidates.Where(candidate => candidate.Region == sourceRegion).Take(1))
            .DistinctBy(candidate => candidate.Region)
            .ToArray();
        AdaptiveRegionMatch? best = null;
        foreach (Candidate candidate in finalists)
        {
            if (!candidate.Region.FitsWithin(image.Width, image.Height)) continue;
            ImageFrame current = VisionScorer.PrepareGray(image.Crop(candidate.Region), reference.Width, reference.Height);
            double score = VisionScorer.RobustSimilarity(reference, current);
            AdaptiveRegionMatch match = new(score, candidate.Correlation, sourceRegion, candidate.Region);
            if (best is null || score > best.Value.Score + 1e-9 ||
                (Math.Abs(score - best.Value.Score) <= 1e-9 && candidate.Correlation > best.Value.Correlation))
            {
                best = match;
            }
        }

        return best ?? new AdaptiveRegionMatch(0, 0, sourceRegion, sourceRegion);
    }
}
