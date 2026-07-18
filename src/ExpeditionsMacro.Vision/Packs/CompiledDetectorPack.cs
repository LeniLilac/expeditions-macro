using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Packs;

public sealed class CompiledDetectorPack : IDetectorPack
{
    private static readonly string[] RecoveryStates = ["disconnect", "lobby", "play", "map_select", "map_preview"];
    private readonly IReadOnlyDictionary<string, StateRuntime> _states;
    private readonly IReadOnlyDictionary<int, SelectionRuntime> _maps;
    private readonly IReadOnlyDictionary<int, SelectionRuntime> _difficulties;
    private readonly ImageFrame _emptyHotbar;

    private sealed record StateRuntime(DetectorStateDefinition Definition, IReadOnlyList<ImageFrame> References);
    private sealed record SelectionRuntime(SelectionDetectorDefinition Definition, ImageFrame Reference);

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
    }

    public string Directory { get; }

    public DetectorPackManifest Manifest { get; }

    public IReadOnlyDictionary<string, double> ScoreStates(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        return _states.ToDictionary(pair => pair.Key, pair => ScoreState(pair.Value, clientImage, [(0, 0)]), StringComparer.OrdinalIgnoreCase);
    }

    public string? Classify(IReadOnlyDictionary<string, double> scores)
    {
        foreach (DetectorStateDefinition definition in Manifest.States)
        {
            if (scores.TryGetValue(definition.Name, out double score) && score >= definition.Threshold) return definition.Name;
        }
        return null;
    }

    public string? RecoveryState(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        foreach (string name in RecoveryStates)
        {
            if (_states.TryGetValue(name, out StateRuntime? state) && ScoreState(state, clientImage, [(0, 0)]) >= state.Definition.Threshold) return name;
        }
        return null;
    }

    public string? CurrentNodeType(ImageFrame clientImage)
    {
        ValidateClient(clientImage);
        double? hue = NodeHue(clientImage);
        if (hue is null) return null;
        (double Distance, string Name)[] ranked = Manifest.NodeHuePrototypes
            .Select(pair => (HueDistance(hue.Value, pair.Value), pair.Key))
            .OrderBy(value => value.Item1)
            .ToArray();
        if (ranked.Length < 2 || ranked[0].Distance > 6 || ranked[1].Distance - ranked[0].Distance < 1.5) return null;
        return ranked[0].Name;
    }

    public int? SelectedMap(ImageFrame clientImage) => BestSelection(_maps, clientImage, 0.90, 0.10, [(0, 0)]);

    public int? SelectedDifficulty(ImageFrame clientImage)
    {
        // Only the fixed center slot represents the active difficulty. Searching
        // vertically can match labels passing through during the carousel animation
        // and make a selected difficulty look like a neighboring one.
        return BestSelection(_difficulties, clientImage, 0.97, 0.01, [(0, 0)]);
    }

    public IReadOnlyList<int> RemainingUnitKeys(ImageFrame clientImage, IReadOnlySet<int> unitKeys)
    {
        ValidateClient(clientImage);
        List<int> remaining = [];
        foreach (int unitKey in unitKeys.OrderBy(key => key == 0 ? 10 : key))
        {
            int slotIndex = unitKey == 0 ? 9 : unitKey - 1;
            ScreenRegion region = new(161 + slotIndex * 49, 542, 46, 50);
            ImageFrame current = clientImage.Crop(region);
            ImageFrame empty = _emptyHotbar.Crop(region);
            double difference = 0;
            for (int index = 0; index < current.Pixels.Length; index++) difference += Math.Abs(current.Pixels[index] - empty.Pixels[index]);
            difference /= current.Pixels.Length * 255d;
            if (difference >= 0.07) remaining.Add(unitKey);
        }
        return remaining;
    }

    public (int X, int Y) ActionFor(string state)
    {
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

    private int? BestSelection(
        IReadOnlyDictionary<int, SelectionRuntime> selections,
        ImageFrame image,
        double minimumScore,
        double minimumGap,
        IReadOnlyList<(int X, int Y)> offsets)
    {
        ValidateClient(image);
        (double Score, int Value)[] ranked = selections
            .Select(pair => (ScoreSelection(pair.Value, image, offsets), pair.Key))
            .OrderByDescending(value => value.Item1)
            .ToArray();
        if (ranked.Length < 2 || ranked[0].Score < minimumScore || ranked[0].Score - ranked[1].Score < minimumGap) return null;
        return ranked[0].Value;
    }

    private static double ScoreSelection(SelectionRuntime runtime, ImageFrame image, IReadOnlyList<(int X, int Y)> offsets)
    {
        double best = 0;
        foreach ((int x, int y) in offsets)
        {
            ScreenRegion region = runtime.Definition.Region.Translate(x, y);
            if (!region.FitsWithin(image.Width, image.Height)) continue;
            ImageFrame current = VisionScorer.PrepareGray(image.Crop(region), runtime.Reference.Width, runtime.Reference.Height);
            best = Math.Max(best, VisionScorer.RobustSimilarity(runtime.Reference, current));
        }
        return best;
    }

    private double? NodeHue(ImageFrame image)
    {
        ImageFrame crop = image.Crop(Manifest.NodeHueRegion);
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
