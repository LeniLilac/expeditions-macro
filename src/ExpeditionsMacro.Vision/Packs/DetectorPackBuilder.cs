using System.Security.Cryptography;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Packs;

public sealed class DetectorPackBuilder
{
    public async Task<string> BuildAsync(
        string datasetsRoot,
        string outputRoot,
        string version,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Version.TryParse(version, out _)) throw new ArgumentException("Detector pack version must use dotted numeric form.", nameof(version));
        string target = Path.Combine(outputRoot, AnimeExpeditionsDetectorSpec.PackId, version);
        string staging = Path.Combine(outputRoot, $".{AnimeExpeditionsDetectorSpec.PackId}.{Guid.NewGuid():N}.staging");
        string backup = $"{target}.{Guid.NewGuid():N}.backup";
        Directory.CreateDirectory(staging);
        try
        {
            List<DetectorStateDefinition> states = [];
            foreach (DatasetStateSpec spec in AnimeExpeditionsDetectorSpec.States)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Compiling {spec.Name} references");
                List<ImageFrame> examples = LoadDatasets(datasetsRoot, spec.Datasets);
                List<DetectorRegionReference> references = [];
                for (int regionIndex = 0; regionIndex < spec.Regions.Count; regionIndex++)
                {
                    ScreenRegion region = spec.Regions[regionIndex];
                    IReadOnlyList<ImageFrame> crops = examples.Select(image => image.Crop(region)).ToArray();
                    ImageFrame reference = VisionScorer.BuildReference(crops).Reference;
                    string relative = Path.Combine("states", spec.Name, $"{regionIndex:D2}.png").Replace('\\', '/');
                    ImageCodec.SavePng(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)), reference);
                    references.Add(new DetectorRegionReference(region, relative));
                }
                states.Add(new DetectorStateDefinition
                {
                    Name = spec.Name,
                    Regions = references,
                    ActionX = spec.ActionX,
                    ActionY = spec.ActionY,
                    Threshold = spec.Threshold,
                });
            }

            List<SelectionDetectorDefinition> mapSelections = [];
            foreach ((int value, string dataset) in AnimeExpeditionsDetectorSpec.MapDatasets)
            {
                ScreenRegion region = new(654, 28, 145, 31);
                ImageFrame reference = VisionScorer.BuildReference(LoadDatasets(datasetsRoot, [dataset]).Select(image => image.Crop(region)).ToArray()).Reference;
                string relative = $"selections/map-{value}.png";
                ImageCodec.SavePng(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)), reference);
                mapSelections.Add(new SelectionDetectorDefinition { Value = value, Region = region, File = relative, MinimumScore = 0.90 });
            }

            List<SelectionDetectorDefinition> difficultySelections = [];
            foreach ((int value, string dataset) in AnimeExpeditionsDetectorSpec.DifficultyDatasets)
            {
                ScreenRegion region = new(636, 386, 151, 41);
                ImageFrame reference = VisionScorer.BuildReference(LoadDatasets(datasetsRoot, [dataset]).Select(image => image.Crop(region)).ToArray()).Reference;
                string relative = $"selections/difficulty-{value}.png";
                ImageCodec.SavePng(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)), reference);
                difficultySelections.Add(new SelectionDetectorDefinition { Value = value, Region = region, File = relative, MinimumScore = 0.97 });
            }

            Dictionary<string, double> nodeHues = [];
            foreach ((string name, string dataset) in AnimeExpeditionsDetectorSpec.NodeDatasets)
            {
                double[] hues = LoadDatasets(datasetsRoot, [dataset])
                    .Select(NodeHue)
                    .Where(value => value is not null)
                    .Select(value => value!.Value)
                    .Order()
                    .ToArray();
                if (hues.Length == 0) throw new InvalidDataException($"No usable node bar colors were found in {dataset}.");
                nodeHues[name] = hues[hues.Length / 2];
            }

            List<ImageFrame> emptyFrames = LoadDatasets(datasetsRoot, ["Expedition_Empty_Unit_Bar"]);
            ImageFrame emptyReference = VisionScorer.Median(emptyFrames);
            const string emptyFile = "empty-hotbar.png";
            ImageCodec.SavePng(Path.Combine(staging, emptyFile), emptyReference);

            string? animeExpeditionsRoot = Directory.GetParent(Path.GetFullPath(datasetsRoot))?.FullName;
            if (animeExpeditionsRoot is null) throw new DirectoryNotFoundException("The Anime Expeditions dataset root could not be located.");
            string challengeRoot = Path.Combine(animeExpeditionsRoot, "challenges");
            foreach (ChallengeMapReferenceSpec spec in AnimeExpeditionsDetectorSpec.ChallengeMapReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string source = Path.Combine(challengeRoot, spec.SourceFile.Replace('/', Path.DirectorySeparatorChar));
                ImageFrame image = LoadImage(source);
                string relative = DetectorPackCapabilities.ChallengeMapReferences[spec.Map];
                ImageCodec.SavePng(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)), image.Crop(spec.Region));
            }

            List<DetectorPackFile> files = [];
            foreach (string file in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileInfo info = new(file);
                await using FileStream stream = File.OpenRead(file);
                string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                files.Add(new DetectorPackFile(Path.GetRelativePath(staging, file).Replace('\\', '/'), hash, info.Length));
            }

            DetectorPackManifest manifest = new()
            {
                PackId = AnimeExpeditionsDetectorSpec.PackId,
                Version = version,
                GameId = "anime-expeditions",
                ModeId = "expeditions",
                MinimumAppVersion = "0.1.0",
                ClientWidth = AnimeExpeditionsDetectorSpec.ClientWidth,
                ClientHeight = AnimeExpeditionsDetectorSpec.ClientHeight,
                States = states,
                MapSelections = mapSelections,
                DifficultySelections = difficultySelections,
                DifficultyHuePrototypes = AnimeExpeditionsDetectorSpec.DifficultyHuePrototypes,
                DifficultyHueRegion = AnimeExpeditionsDetectorSpec.DifficultyHueRegion,
                NodeHuePrototypes = nodeHues,
                NodeHueRegion = AnimeExpeditionsDetectorSpec.NodeBarRegion,
                EmptyHotbarReferenceFile = emptyFile,
                ExtraActions = AnimeExpeditionsDetectorSpec.ExtraActions,
                Files = files,
                BuiltAt = DateTimeOffset.UtcNow,
            };
            manifest.Validate();
            await JsonFileStore.WriteAtomicAsync(Path.Combine(staging, "manifest.json"), manifest, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (Directory.Exists(target)) Directory.Move(target, backup);
            Directory.Move(staging, target);
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
            progress?.Report($"Detector pack {version} compiled successfully");
            return target;
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
        }
    }

    private static List<ImageFrame> LoadDatasets(string root, IReadOnlyList<string> names)
    {
        List<ImageFrame> images = [];
        foreach (string name in names)
        {
            string directory = Path.Combine(root, name);
            IEnumerable<string> paths = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase)
                : Enumerable.Empty<string>();
            foreach (string path in paths)
            {
                ImageFrame image = ImageCodec.Load(path, PixelFormat.Rgb24);
                if (image.Width != AnimeExpeditionsDetectorSpec.ClientWidth || image.Height != AnimeExpeditionsDetectorSpec.ClientHeight)
                {
                    throw new InvalidDataException($"{path} is {image.Width} × {image.Height}, expected {AnimeExpeditionsDetectorSpec.ClientWidth} × {AnimeExpeditionsDetectorSpec.ClientHeight}.");
                }
                images.Add(image);
            }
        }
        if (images.Count == 0) throw new FileNotFoundException($"No PNG examples were found for {string.Join(", ", names)}.");
        return images;
    }

    private static ImageFrame LoadImage(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("A detector source image is missing.", path);
        ImageFrame image = ImageCodec.Load(path, PixelFormat.Rgb24);
        if (image.Width != AnimeExpeditionsDetectorSpec.ClientWidth || image.Height != AnimeExpeditionsDetectorSpec.ClientHeight)
        {
            throw new InvalidDataException($"{path} is {image.Width} by {image.Height}, expected {AnimeExpeditionsDetectorSpec.ClientWidth} by {AnimeExpeditionsDetectorSpec.ClientHeight}.");
        }
        return image;
    }

    private static double? NodeHue(ImageFrame image)
    {
        ImageFrame crop = image.Crop(AnimeExpeditionsDetectorSpec.NodeBarRegion);
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
}
