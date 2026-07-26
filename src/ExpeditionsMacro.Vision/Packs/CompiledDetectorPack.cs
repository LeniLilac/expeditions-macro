using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Packs;

public sealed partial class CompiledDetectorPack : IDetectorPack
{
    private static readonly string[] RecoveryStates = ["afk", "disconnect", "lobby", "play", "map_select", "map_preview"];
    private static readonly ScreenRegion HotbarAnchorRegion = new(154, 536, 500, 62);
    private static readonly ScreenRegion NodeBarAnchorRegion = new(220, 53, 370, 32);
    private readonly IReadOnlyDictionary<string, StateRuntime> _states;
    private readonly IReadOnlyDictionary<int, SelectionRuntime> _maps;
    private readonly IReadOnlyDictionary<int, SelectionRuntime> _difficulties;
    private readonly ImageFrame _emptyHotbar;
    private readonly ImageFrame _hotbarReference;
    private readonly ImageFrame _nodeBarReference;
    private readonly ChallengeMapDetector? _challengeMaps;

    private sealed record StateRuntime(DetectorStateDefinition Definition, IReadOnlyList<ImageFrame> References);
    private sealed record SelectionRuntime(SelectionDetectorDefinition Definition, ImageFrame Reference);
    private sealed record AdaptiveStateMatch(
        double Score,
        IReadOnlyList<(DetectorRegionReference Definition, AdaptiveRegionMatch Match)> Regions)
    {
        public (int X, int Y) MapNearestPoint(int x, int y)
        {
            (DetectorRegionReference Definition, AdaptiveRegionMatch Match) nearest = Regions
                .OrderBy(region => DistanceSquared(region.Definition.Region, x, y))
                .First();
            return nearest.Match.MapPoint(x, y);
        }

        public (int X, int Y) MapGlobalPoint(int x, int y)
        {
            double scaleX = Median(Regions.Select(region => region.Match.ScaleX).ToArray());
            double scaleY = Median(Regions.Select(region => region.Match.ScaleY).ToArray());
            double translateX = Median(Regions.Select(region => region.Match.MatchedRegion.X - region.Definition.Region.X * scaleX).ToArray());
            double translateY = Median(Regions.Select(region => region.Match.MatchedRegion.Y - region.Definition.Region.Y * scaleY).ToArray());
            return (
                (int)Math.Round(x * scaleX + translateX, MidpointRounding.AwayFromZero),
                (int)Math.Round(y * scaleY + translateY, MidpointRounding.AwayFromZero));
        }

        private static double DistanceSquared(ScreenRegion region, int x, int y)
        {
            double nearestX = Math.Clamp(x, region.X, region.Right - 1);
            double nearestY = Math.Clamp(y, region.Y, region.Bottom - 1);
            return Math.Pow(x - nearestX, 2) + Math.Pow(y - nearestY, 2);
        }
    }

    public CompiledDetectorPack(string directory, DetectorPackManifest manifest)
    {
        manifest.Validate();
        Directory = directory;
        Manifest = manifest;
        _states = manifest.States.ToDictionary(
            state => state.Name,
            state => new StateRuntime(
                state,
                state.Regions.Select(reference => ImageCodec.Load(Resolve(reference.File), PixelFormat.Gray8)).ToArray()),
            StringComparer.OrdinalIgnoreCase);
        _maps = manifest.MapSelections.ToDictionary(
            selection => selection.Value,
            selection => new SelectionRuntime(selection, ImageCodec.Load(Resolve(selection.File), PixelFormat.Gray8)));
        _difficulties = manifest.DifficultySelections.ToDictionary(
            selection => selection.Value,
            selection => new SelectionRuntime(selection, ImageCodec.Load(Resolve(selection.File), PixelFormat.Gray8)));
        _emptyHotbar = ImageCodec.Load(Resolve(manifest.EmptyHotbarReferenceFile), PixelFormat.Rgb24);
        _hotbarReference = VisionScorer.PrepareGray(_emptyHotbar.Crop(HotbarAnchorRegion));
        _nodeBarReference = VisionScorer.PrepareGray(_emptyHotbar.Crop(NodeBarAnchorRegion));
        _challengeMaps = LoadChallengeMapDetector();
    }

    public string Directory { get; }

    public DetectorPackManifest Manifest { get; }

    public bool SupportsChallengeMaps => _challengeMaps?.IsComplete == true;

    public IReadOnlyDictionary<string, double> ScoreStates(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        bool useSpecializedDetectors = Manifest.PackId.Equals(
            AnimeExpeditionsDetectorSpec.PackId,
            StringComparison.OrdinalIgnoreCase);
        Dictionary<string, double> scores = _states.ToDictionary(
            pair => pair.Key,
            pair => ScoreConfiguredState(pair.Key, pair.Value, clientImage, useSpecializedDetectors),
            StringComparer.OrdinalIgnoreCase);
        if (useSpecializedDetectors) scores["afk"] = AfkChamberDetector.Score(clientImage);
        if (useSpecializedDetectors)
        {
            scores["map_select"] = Math.Max(
                scores.GetValueOrDefault("map_select"),
                ExpeditionSelectorDetector.Score(clientImage));
        }
        if (useSpecializedDetectors && Classify(scores) is null)
        {
            foreach ((string name, StateRuntime runtime) in _states
                         .Where(pair => pair.Key is "lobby" or "play"))
            {
                scores[name] = Math.Max(scores[name], ScoreAdaptiveState(name, runtime, clientImage));
            }
        }
        return scores;
    }

    public string? Classify(IReadOnlyDictionary<string, double> scores)
    {
        if (Manifest.PackId.Equals(AnimeExpeditionsDetectorSpec.PackId, StringComparison.OrdinalIgnoreCase) &&
            scores.TryGetValue("afk", out double afkScore) &&
            afkScore >= AfkChamberDetector.Threshold) return "afk";
        foreach (DetectorStateDefinition definition in Manifest.States)
        {
            if (scores.TryGetValue(definition.Name, out double score) && score >= definition.Threshold) return definition.Name;
        }
        return null;
    }

    public string? RecoveryState(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        bool useSpecializedDetectors = Manifest.PackId.Equals(AnimeExpeditionsDetectorSpec.PackId, StringComparison.OrdinalIgnoreCase);
        if (useSpecializedDetectors && AfkChamberDetector.Score(clientImage) >= AfkChamberDetector.Threshold) return "afk";
        if (useSpecializedDetectors &&
            ExpeditionSelectorDetector.Score(clientImage) >= 0.90)
        {
            return "map_select";
        }
        Dictionary<string, double> scores = [];
        foreach (string name in RecoveryStates)
        {
            if (!_states.TryGetValue(name, out StateRuntime? state)) continue;
            double score = ScoreConfiguredState(name, state, clientImage, useSpecializedDetectors);
            scores[name] = score;
            if (score >= state.Definition.Threshold) return name;
        }
        if (useSpecializedDetectors)
        {
            foreach (string name in RecoveryStates.Where(name => name is "lobby" or "play"))
            {
                StateRuntime state = _states[name];
                scores[name] = Math.Max(scores.GetValueOrDefault(name), ScoreAdaptiveState(name, state, clientImage));
            }
            foreach (string name in RecoveryStates.Where(_states.ContainsKey))
            {
                if (scores.GetValueOrDefault(name) >= _states[name].Definition.Threshold) return name;
            }
        }
        return null;
    }

    public string? CurrentNodeType(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        ScreenRegion hueRegion = Manifest.NodeHueRegion;
        if (Manifest.PackId.Equals(AnimeExpeditionsDetectorSpec.PackId, StringComparison.OrdinalIgnoreCase))
        {
            AdaptiveRegionMatch anchor = AdaptiveUiMatcher.Find(_nodeBarReference, clientImage, NodeBarAnchorRegion, 42, 32);
            ScreenRegion mapped = anchor.MapRegion(Manifest.NodeHueRegion);
            if (anchor.Score >= 0.28 && mapped.FitsWithin(clientImage.Width, clientImage.Height)) hueRegion = mapped;
        }
        double? hue = NodeHue(clientImage, hueRegion);
        if (hue is null) return null;
        (double Distance, string Name)[] ranked = Manifest.NodeHuePrototypes
            .Select(pair => (HueDistance(hue.Value, pair.Value), pair.Key))
            .OrderBy(value => value.Item1)
            .ToArray();
        if (ranked.Length < 2 || ranked[0].Distance > 6 || ranked[1].Distance - ranked[0].Distance < 1.5) return null;
        return ranked[0].Name;
    }

    public IReadOnlyList<int> RemainingUnitKeys(ImageFrame clientImage, IReadOnlySet<int> unitKeys)
    {
        ValidateClient(clientImage);
        AdaptiveRegionMatch hotbar = AdaptiveUiMatcher.Find(_hotbarReference, clientImage, HotbarAnchorRegion, 36, 42);
        bool useAdaptiveHotbar = hotbar.Score >= 0.32;
        List<int> remaining = [];
        foreach (int unitKey in unitKeys.OrderBy(key => key == 0 ? 10 : key))
        {
            int slotIndex = unitKey == 0 ? 9 : unitKey - 1;
            ScreenRegion region = new(161 + slotIndex * 49, 542, 46, 50);
            ScreenRegion currentRegion = useAdaptiveHotbar ? hotbar.MapRegion(region) : region;
            if (!currentRegion.FitsWithin(clientImage.Width, clientImage.Height)) currentRegion = region;
            ImageFrame current = ResizeRgb(clientImage.Crop(currentRegion), region.Width, region.Height);
            ImageFrame empty = _emptyHotbar.Crop(region);
            double difference = 0;
            for (int index = 0; index < current.Pixels.Length; index++) difference += Math.Abs(current.Pixels[index] - empty.Pixels[index]);
            difference /= current.Pixels.Length * 255d;
            if (difference >= 0.07) remaining.Add(unitKey);
        }
        return remaining;
    }

    public ChallengeMapId? ChallengeMapForType(ImageFrame clientImage, ChallengeType type)
    {
        if (_challengeMaps is null) throw new InvalidOperationException(DetectorPackCapabilities.ChallengeMapsUnavailableMessage(Manifest));
        return _challengeMaps.Detect(clientImage, type).Map;
    }

    public (int X, int Y) ActionFor(string state, ImageFrame? clientImage = null)
    {
        bool useSpecializedDetectors = clientImage is not null &&
            Manifest.PackId.Equals(AnimeExpeditionsDetectorSpec.PackId, StringComparison.OrdinalIgnoreCase);
        if (useSpecializedDetectors)
        {
            ValidateClient(clientImage!);
            if (ExpeditionSelectorDetector.ActionFor(
                    clientImage!,
                    state) is { } expeditionAction)
            {
                return expeditionAction;
            }
            if (state.Equals("afk", StringComparison.OrdinalIgnoreCase) && AfkChamberDetector.ActionFor(clientImage!) is { } afkAction) return afkAction;
            (int X, int Y)? startAction = state.Equals("start", StringComparison.OrdinalIgnoreCase) ? StartDialogDetector.ActionFor(clientImage!) : null;
            if (startAction is not null) return startAction.Value;
            if (state.Equals("play", StringComparison.OrdinalIgnoreCase) && _states.TryGetValue(state, out StateRuntime? playState))
            {
                (ScreenRegion Region, ImageFrame Reference) title = PlayTitleReference(playState);
                (int X, int Y)? playAction = PlayScreenDetector.ActionFor(
                    title.Reference,
                    title.Region,
                    clientImage!,
                    playState.Definition.ActionX,
                    playState.Definition.ActionY);
                if (playAction is not null) return playAction.Value;
            }
            (int X, int Y)? pauseAction = PauseButtonDetector.ActionFor(clientImage!, state);
            if (pauseAction is not null) return pauseAction.Value;
            (int X, int Y)? buttonAction = ActionButtonDetector.ActionFor(clientImage!, state);
            if (buttonAction is not null) return buttonAction.Value;
            if (state.Equals("reward", StringComparison.OrdinalIgnoreCase) && RewardScreenDetector.ActionFor(clientImage!) is (int X, int Y) rewardAction) return rewardAction;
            if (_states.TryGetValue(state, out StateRuntime? adaptiveState))
            {
                AdaptiveStateMatch match = MatchState(adaptiveState, clientImage!);
                if (match.Score >= adaptiveState.Definition.Threshold * 0.70)
                {
                    return match.MapNearestPoint(adaptiveState.Definition.ActionX, adaptiveState.Definition.ActionY);
                }
            }
            if (Manifest.ExtraActions.TryGetValue(state, out int[]? adaptivePoint) && adaptivePoint.Length >= 2)
            {
                if (state is "difficulty_minus" or "difficulty_plus" && BestAdaptiveLayout(_difficulties, clientImage!) is { } difficulty)
                {
                    return difficulty.Match.MapPoint(adaptivePoint[0], adaptivePoint[1]);
                }
                if (_states.TryGetValue("map_select", out StateRuntime? mapSelect))
                {
                    AdaptiveStateMatch match = MatchState(mapSelect, clientImage!);
                    if (match.Score >= mapSelect.Definition.Threshold * 0.60) return match.MapGlobalPoint(adaptivePoint[0], adaptivePoint[1]);
                }
            }
        }
        if (Manifest.ExtraActions.TryGetValue(state, out int[]? point) && point.Length >= 2) return (point[0], point[1]);
        if (_states.TryGetValue(state, out StateRuntime? runtime)) return (runtime.Definition.ActionX, runtime.Definition.ActionY);
        throw new KeyNotFoundException($"Detector pack does not define an action for '{state}'.");
    }

    private double ScoreState(StateRuntime runtime, ImageFrame image, IReadOnlyList<(int X, int Y)> offsets)
    {
        double best = 0;
        foreach ((int offsetX, int offsetY) in offsets)
        {
            List<double> scores = [];
            bool valid = true;
            for (int index = 0; index < runtime.Definition.Regions.Count; index++)
            {
                ScreenRegion region = runtime.Definition.Regions[index].Region.Translate(offsetX, offsetY);
                if (!region.FitsWithin(image.Width, image.Height))
                {
                    valid = false;
                    break;
                }
                ImageFrame reference = runtime.References[index];
                ImageFrame current = VisionScorer.PrepareGray(image.Crop(region), reference.Width, reference.Height);
                scores.Add(VisionScorer.RobustSimilarity(reference, current));
            }
            if (valid && scores.Count != 0) best = Math.Max(best, Median(scores));
        }
        return best;
    }

    private static AdaptiveStateMatch MatchState(StateRuntime runtime, ImageFrame image)
    {
        List<(DetectorRegionReference Definition, AdaptiveRegionMatch Match)> regions = [];
        for (int index = 0; index < runtime.Definition.Regions.Count; index++)
        {
            DetectorRegionReference definition = runtime.Definition.Regions[index];
            AdaptiveRegionMatch match = AdaptiveUiMatcher.Find(runtime.References[index], image, definition.Region);
            regions.Add((definition, match));
        }
        return new AdaptiveStateMatch(Median(regions.Select(region => region.Match.Score).ToArray()), regions);
    }

    private static double ScoreAdaptiveState(string name, StateRuntime runtime, ImageFrame image)
    {
        double score = MatchState(runtime, image).Score;
        if (name is "map_select" or "map_preview" or "confirm" or "extract_confirm" or "disconnect")
        {
            score = Math.Max(score, ActionButtonDetector.Score(image, name));
        }
        return score;
    }

    private double ScoreConfiguredState(string name, StateRuntime runtime, ImageFrame image, bool useSpecializedDetectors)
    {
        double fixedScore = ScoreState(runtime, image, [(0, 0)]);
        if (useSpecializedDetectors)
        {
            if (name.Equals("start", StringComparison.OrdinalIgnoreCase)) return StartDialogDetector.Score(image);
            if (name.Equals("play", StringComparison.OrdinalIgnoreCase))
            {
                (ScreenRegion Region, ImageFrame Reference) title = PlayTitleReference(runtime);
                return Math.Max(fixedScore, PlayScreenDetector.Score(title.Reference, title.Region, image));
            }
            if (name.Equals("checkpoint", StringComparison.OrdinalIgnoreCase)) return PauseButtonDetector.ScoreCheckpoint(image);
            if (name.Equals("continue", StringComparison.OrdinalIgnoreCase)) return PauseButtonDetector.ScoreContinue(image);
            // The legacy three-region template could correlate with ordinary units
            // and effects during gameplay. The specialized detector requires the
            // stable reward header and live Select Upgrade controls instead.
            if (name.Equals("reward", StringComparison.OrdinalIgnoreCase)) return RewardScreenDetector.Score(image);
            if (name is "victory" or "defeat") return Math.Max(fixedScore, TerminalScreenDetector.Score(image, name));
            if (name is "map_select" or "map_preview" or "confirm" or "extract_confirm" or "disconnect")
            {
                return Math.Max(fixedScore, ActionButtonDetector.Score(image, name));
            }
        }
        return fixedScore;
    }

    private static (ScreenRegion Region, ImageFrame Reference) PlayTitleReference(StateRuntime runtime)
    {
        int index = Enumerable.Range(0, runtime.Definition.Regions.Count)
            .OrderBy(candidate => runtime.Definition.Regions[candidate].Region.Width * runtime.Definition.Regions[candidate].Region.Height)
            .First();
        return (runtime.Definition.Regions[index].Region, runtime.References[index]);
    }

    private static double? NodeHue(ImageFrame image, ScreenRegion region)
    {
        if (!region.FitsWithin(image.Width, image.Height)) return null;
        ImageFrame crop = image.Crop(region);
        using Mat rgb = ImageCodec.ToMat(crop);
        using Mat hsv = new();
        Cv2.CvtColor(rgb, hsv, ColorConversionCodes.RGB2HSV);
        List<byte> hues = [];
        int rows = hsv.Rows;
        int columns = hsv.Cols;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vec3b pixel = hsv.At<Vec3b>(y, x);
                if (pixel.Item1 >= 100 && pixel.Item2 >= 100) hues.Add(pixel.Item0);
            }
        }
        if (hues.Count < 12) return null;
        hues.Sort();
        return hues[hues.Count / 2];
    }

    private static ImageFrame ResizeRgb(ImageFrame image, int width, int height)
    {
        if (image.Format != PixelFormat.Rgb24) throw new ArgumentException("Resize input must be RGB.", nameof(image));
        if (image.Width == width && image.Height == height) return image;
        using Mat source = ImageCodec.ToMat(image);
        using Mat resized = new();
        Cv2.Resize(source, resized, new Size(width, height), 0, 0, image.Width > width || image.Height > height ? InterpolationFlags.Area : InterpolationFlags.Linear);
        return ImageCodec.FromMat(resized, PixelFormat.Rgb24);
    }

    private void ValidateClient(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || image.Width != Manifest.ClientWidth || image.Height != Manifest.ClientHeight)
        {
            throw new InvalidDataException($"Detector input must be an RGB {Manifest.ClientWidth} × {Manifest.ClientHeight} client image.");
        }
    }

    private string Resolve(string relative)
    {
        string full = Path.GetFullPath(Path.Combine(Directory, relative.Replace('/', Path.DirectorySeparatorChar)));
        string root = Path.GetFullPath(Directory) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Detector pack contains an unsafe path.");
        return full;
    }

    private ChallengeMapDetector? LoadChallengeMapDetector()
    {
        Dictionary<ChallengeMapId, ImageFrame> references = [];
        foreach ((ChallengeMapId map, string relative) in DetectorPackCapabilities.ChallengeMapReferences)
        {
            string path = Resolve(relative);
            if (!File.Exists(path)) return null;
            references[map] = ImageCodec.Load(path, PixelFormat.Gray8);
        }
        return new ChallengeMapDetector(references);
    }

    private static double HueDistance(double left, double right)
    {
        double difference = Math.Abs(left - right);
        return Math.Min(difference, 180 - difference);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        double[] sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
