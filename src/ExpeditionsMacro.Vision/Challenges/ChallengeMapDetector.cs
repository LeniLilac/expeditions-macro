using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Challenges;

public sealed record ChallengeMapMatch(ChallengeMapId? Map, double Confidence, double RunnerUpConfidence);

public sealed class ChallengeMapDetector
{
    private static readonly IReadOnlyDictionary<ChallengeType, ScreenRegion> TypeRegions =
        new Dictionary<ChallengeType, ScreenRegion>
        {
            [ChallengeType.Trait] = new(276, 203, 78, 75),
            [ChallengeType.Stat] = new(276, 292, 78, 75),
            [ChallengeType.Sprite] = new(276, 381, 78, 75),
        };

    private readonly IReadOnlyDictionary<ChallengeMapId, ImageFrame> _references;

    public ChallengeMapDetector(IReadOnlyDictionary<ChallengeMapId, ImageFrame> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        _references = references.ToDictionary(
            pair => pair.Key,
            pair => VisionScorer.PrepareGray(pair.Value, maximumWidth: int.MaxValue));
    }

    public bool IsComplete => _references.Count == Enum.GetValues<ChallengeMapId>().Length;

    public ChallengeMapMatch Detect(ImageFrame image, ChallengeType type)
    {
        IReadOnlyDictionary<ChallengeMapId, double> scores = ScoreMaps(image, type);
        (ChallengeMapId Map, double Score)[] ranked = scores
            .Select(pair => (pair.Key, pair.Value))
            .OrderByDescending(pair => pair.Value)
            .ToArray();
        if (ranked.Length == 0) return new ChallengeMapMatch(null, 0, 0);
        double runnerUp = ranked.Length > 1 ? ranked[1].Score : 0;
        bool accepted = ranked[0].Score >= 0.67 && ranked[0].Score - runnerUp >= 0.035;
        return new ChallengeMapMatch(accepted ? ranked[0].Map : null, ranked[0].Score, runnerUp);
    }

    public IReadOnlyDictionary<ChallengeMapId, double> ScoreMaps(ImageFrame image, ChallengeType type)
    {
        ValidateClient(image);
        if (!TypeRegions.TryGetValue(type, out ScreenRegion region)) throw new ArgumentOutOfRangeException(nameof(type));
        return _references.ToDictionary(
            pair => pair.Key,
            pair => AdaptiveUiMatcher.Find(pair.Value, image, region, 24, 22, [0.95, 1.0, 1.05, 1.10]).Score);
    }

    private static void ValidateClient(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || image.Width != ChallengeScreenDetector.ClientWidth || image.Height != ChallengeScreenDetector.ClientHeight)
        {
            throw new InvalidDataException($"Challenge map detector input must be an RGB {ChallengeScreenDetector.ClientWidth} by {ChallengeScreenDetector.ClientHeight} client image.");
        }
    }
}
