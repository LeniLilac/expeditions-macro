using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Packs;

public sealed partial class CompiledDetectorPack
{
    private static readonly (int X, int Y)[]
        DifficultyLayoutOffsets =
        Enumerable.Range(-8, 17)
            .Select(y => (0, y))
            .ToArray();

    public int? SelectedMap(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        if (UsesAnimeExpeditionsDetectors &&
            ExpeditionSelectorDetector.SelectedMap(
                clientImage) is int currentSelection)
        {
            return currentSelection;
        }
        if (UsesAnimeExpeditionsDetectors &&
            MapSelectionDetector.Detect(
                clientImage) is int markerSelection)
        {
            return markerSelection;
        }
        if (UsesAnimeExpeditionsDetectors &&
            !HasLegacySelectorOwner(clientImage))
        {
            return null;
        }

        int? selected = BestSelection(
            _maps,
            clientImage,
            0.90,
            0.10,
            [(0, 0)]);
        return selected ??
            BestAdaptiveSelection(
                _maps,
                clientImage,
                0.90,
                0.10);
    }

    public int? SelectedDifficulty(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        if (UsesAnimeExpeditionsDetectors &&
            ExpeditionSelectorDetector.SelectedDifficulty(
                clientImage) is int currentSelection)
        {
            return currentSelection;
        }
        if (UsesAnimeExpeditionsDetectors &&
            !HasLegacySelectorOwner(clientImage))
        {
            return null;
        }

        (bool observed, int? selected) =
            DifficultyFromHue(
                clientImage,
                Manifest.DifficultyHueRegion);
        if (selected is not null)
        {
            return selected;
        }

        (SelectionRuntime Runtime, AdaptiveRegionMatch Match)?
            adaptiveLayout =
                BestAdaptiveLayout(
                    _difficulties,
                    clientImage);
        if (adaptiveLayout is not null &&
            Manifest.DifficultyHueRegion is
                ScreenRegion configuredHue)
        {
            ScreenRegion mappedHue =
                adaptiveLayout.Value.Match.MapRegion(
                    configuredHue);
            (bool adaptiveObserved, int? adaptiveSelected) =
                DifficultyFromHue(
                    clientImage,
                    mappedHue);
            observed |= adaptiveObserved;
            if (adaptiveSelected is not null)
            {
                return adaptiveSelected;
            }
        }

        // GB-008: one shared layout offset keeps the active
        // center slot authoritative while the carousel labels move.
        int? templateSelected =
            BestSelectionAtSharedOffset(
                _difficulties,
                clientImage,
                0.97,
                0.01,
                DifficultyLayoutOffsets);
        if (templateSelected is not null)
        {
            return templateSelected;
        }
        return observed
            ? null
            : BestAdaptiveSelection(
                _difficulties,
                clientImage,
                0.91,
                0.015);
    }

    private bool UsesAnimeExpeditionsDetectors =>
        Manifest.PackId.Equals(
            AnimeExpeditionsDetectorSpec.PackId,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasLegacySelectorOwner(
        ImageFrame image) =>
        ActionButtonDetector.Score(
            image,
            "map_select") > 0;

    private int? BestSelection(
        IReadOnlyDictionary<int, SelectionRuntime> selections,
        ImageFrame image,
        double minimumScore,
        double minimumGap,
        IReadOnlyList<(int X, int Y)> offsets)
    {
        ValidateClient(image);
        (double Score, int Value)[] ranked =
            selections
                .Select(pair =>
                    (ScoreSelection(
                         pair.Value,
                         image,
                         offsets),
                     pair.Key))
                .OrderByDescending(value => value.Item1)
                .ToArray();
        return IsUnambiguous(
                ranked,
                minimumScore,
                minimumGap)
            ? ranked[0].Value
            : null;
    }

    private int? BestSelectionAtSharedOffset(
        IReadOnlyDictionary<int, SelectionRuntime> selections,
        ImageFrame image,
        double minimumScore,
        double minimumGap,
        IReadOnlyList<(int X, int Y)> offsets)
    {
        ValidateClient(image);
        (double LayoutScore,
         (double Score, int Value)[] Scores) bestLayout =
            offsets
                .Select(offset =>
                {
                    (double Score, int Value)[] scores =
                        selections
                            .Select(pair =>
                                (ScoreSelection(
                                     pair.Value,
                                     image,
                                     [offset]),
                                 pair.Key))
                            .OrderByDescending(
                                value => value.Item1)
                            .ToArray();
                    return (
                        LayoutScore: Median(
                            scores
                                .Select(value => value.Score)
                                .ToArray()),
                        Scores: scores);
                })
                .OrderByDescending(
                    candidate =>
                        candidate.LayoutScore)
                .ThenByDescending(
                    candidate =>
                        candidate.Scores[0].Score)
                .First();
        return IsUnambiguous(
                bestLayout.Scores,
                minimumScore,
                minimumGap)
            ? bestLayout.Scores[0].Value
            : null;
    }

    private int? BestAdaptiveSelection(
        IReadOnlyDictionary<int, SelectionRuntime> selections,
        ImageFrame image,
        double minimumScore,
        double minimumGap)
    {
        ValidateClient(image);
        (double Score, int Value)[] ranked =
            selections
                .Select(pair =>
                    (AdaptiveUiMatcher.Find(
                         pair.Value.Reference,
                         image,
                         pair.Value.Definition.Region).Score,
                     pair.Key))
                .OrderByDescending(value => value.Item1)
                .ToArray();
        return IsUnambiguous(
                ranked,
                minimumScore,
                minimumGap)
            ? ranked[0].Value
            : null;
    }

    private static (
        SelectionRuntime Runtime,
        AdaptiveRegionMatch Match)?
        BestAdaptiveLayout(
            IReadOnlyDictionary<int, SelectionRuntime>
                selections,
            ImageFrame image)
    {
        (SelectionRuntime Runtime,
         AdaptiveRegionMatch Match)[] ranked =
            selections.Values
                .Select(runtime =>
                    (runtime,
                     AdaptiveUiMatcher.Find(
                         runtime.Reference,
                         image,
                         runtime.Definition.Region)))
                .OrderByDescending(
                    value => value.Item2.Score)
                .ToArray();
        return ranked.Length == 0 ||
            ranked[0].Match.Score < 0.35
                ? null
                : ranked[0];
    }

    private static double ScoreSelection(
        SelectionRuntime runtime,
        ImageFrame image,
        IReadOnlyList<(int X, int Y)> offsets)
    {
        double best = 0;
        foreach ((int x, int y) in offsets)
        {
            ScreenRegion region =
                runtime.Definition.Region.Translate(x, y);
            if (!region.FitsWithin(
                    image.Width,
                    image.Height))
            {
                continue;
            }
            ImageFrame current =
                VisionScorer.PrepareGray(
                    image.Crop(region),
                    runtime.Reference.Width,
                    runtime.Reference.Height);
            best = Math.Max(
                best,
                VisionScorer.RobustSimilarity(
                    runtime.Reference,
                    current));
        }
        return best;
    }

    private (bool Observed, int? Selected)
        DifficultyFromHue(
        ImageFrame image,
        ScreenRegion? candidateRegion)
    {
        IReadOnlyDictionary<int, double>? prototypes =
            Manifest.DifficultyHuePrototypes;
        ScreenRegion? configuredRegion =
            candidateRegion;
        if ((prototypes is null ||
             configuredRegion is null) &&
            UsesAnimeExpeditionsDetectors)
        {
            prototypes =
                AnimeExpeditionsDetectorSpec
                    .DifficultyHuePrototypes;
            configuredRegion =
                AnimeExpeditionsDetectorSpec
                    .DifficultyHueRegion;
        }
        if (prototypes is null ||
            configuredRegion is not ScreenRegion region ||
            !region.FitsWithin(
                image.Width,
                image.Height))
        {
            return (false, null);
        }

        using Mat rgb =
            ImageCodec.ToMat(image.Crop(region));
        using Mat hsv = new();
        Cv2.CvtColor(
            rgb,
            hsv,
            ColorConversionCodes.RGB2HSV);
        Dictionary<int, int> counts =
            prototypes.Keys.ToDictionary(
                value => value,
                _ => 0);
        int coloredPixels = 0;
        int rows = hsv.Rows;
        int columns = hsv.Cols;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vec3b pixel = hsv.At<Vec3b>(y, x);
                if (pixel.Item1 < 140 ||
                    pixel.Item2 < 90)
                {
                    continue;
                }
                coloredPixels++;
                (double Distance, int Value) nearest =
                    prototypes
                        .Select(pair =>
                            (HueDistance(
                                 pixel.Item0,
                                 pair.Value),
                             pair.Key))
                        .OrderBy(value => value.Item1)
                        .First();
                if (nearest.Distance <= 18)
                {
                    counts[nearest.Value]++;
                }
            }
        }

        if (coloredPixels < 50)
        {
            return (false, null);
        }
        (int Count, int Value)[] ranked =
            counts
                .Select(pair =>
                    (pair.Value, pair.Key))
                .OrderByDescending(
                    value => value.Item1)
                .ToArray();
        return IsUnambiguous(ranked, 50, 30)
            ? (true, ranked[0].Value)
            : (true, null);
    }

    private static bool IsUnambiguous(
        IReadOnlyList<(double Score, int Value)> ranked,
        double minimumScore,
        double minimumGap) =>
        ranked.Count >= 2 &&
        ranked[0].Score >= minimumScore &&
        ranked[0].Score - ranked[1].Score >=
            minimumGap;

    private static bool IsUnambiguous(
        IReadOnlyList<(int Count, int Value)> ranked,
        int minimumCount,
        int minimumGap) =>
        ranked.Count >= 2 &&
        ranked[0].Count >= minimumCount &&
        ranked[0].Count - ranked[1].Count >=
            minimumGap;
}
