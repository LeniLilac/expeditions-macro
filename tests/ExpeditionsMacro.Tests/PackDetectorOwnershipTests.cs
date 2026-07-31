using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class PackDetectorOwnershipTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void AfkActionRail_DoesNotRequireTheDecorativeTitle()
    {
        string directory = Path.Combine(
            TestPaths.Datasets,
            "AFK_Chamber");
        if (!Directory.Exists(directory))
        {
            return;
        }

        string file = Directory.EnumerateFiles(
                directory,
                "*.png")
            .Order(StringComparer.OrdinalIgnoreCase)
            .First();
        ImageFrame original = ImageCodec.Load(file);
        byte[] pixels = original.Pixels.ToArray();
        Clear(
            pixels,
            original.Width,
            x: 270,
            y: 12,
            width: 270,
            height: 70);
        ImageFrame titleDelayed = new(
            original.Width,
            original.Height,
            original.Format,
            pixels,
            takeOwnership: true);
        CompiledDetectorPack pack = LoadPack();

        Assert.True(
            pack.ScoreStates(titleDelayed)["afk"] >= 0.84);
        Assert.Equal(
            "afk",
            pack.RecoveryState(titleDelayed));
        (int x, int y) =
            pack.ActionFor("afk", titleDelayed);
        Assert.InRange(x, 445, 495);
        Assert.InRange(y, 565, 602);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void AfkActionRail_DoesNotOwnOtherRepresentativeStates()
    {
        string datasetRoot = Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions");
        if (!Directory.Exists(datasetRoot))
        {
            return;
        }

        CompiledDetectorPack pack = LoadPack();
        foreach (string file in RepresentativeSamples(
                     datasetRoot)
                 .Where(path =>
                     !path.Contains(
                         $"{Path.DirectorySeparatorChar}" +
                         "AFK_Chamber" +
                         $"{Path.DirectorySeparatorChar}",
                         StringComparison.OrdinalIgnoreCase)))
        {
            ImageFrame image = ImageCodec.Load(file);
            double score =
                pack.ScoreStates(image, ["afk"])["afk"];
            Assert.True(
                score < 0.84,
                $"{Path.GetRelativePath(datasetRoot, file)} " +
                $"scored {score:P1} as AFK.");
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void MapSelectorOwner_RejectsGameplayGreenComponents()
    {
        string directory = Path.Combine(
            TestPaths.Datasets,
            "Expedition_Recovery_Navigation_Negative");
        if (!Directory.Exists(directory))
        {
            return;
        }

        CompiledDetectorPack pack = LoadPack();
        double threshold = pack.Manifest.States
            .Single(state => state.Name == "map_select")
            .Threshold;

        foreach (string file in Directory.EnumerateFiles(
                     directory,
                     "*.png"))
        {
            ImageFrame image = ImageCodec.Load(file);
            double score =
                pack.ScoreStates(image)["map_select"];
            Assert.True(
                score < threshold,
                $"{Path.GetFileName(file)} scored " +
                $"{score:P1} as a map selector.");
            Assert.Null(pack.SelectedMap(image));
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void LegacyMapSelection_DoesNotAverageInactiveCardArtwork()
    {
        string file = Path.Combine(
            TestPaths.Datasets,
            "Expedition_Map_Select_Selection_Regression",
            "Map1_French.png");
        if (!File.Exists(file))
        {
            return;
        }

        ImageFrame original = ImageCodec.Load(file);
        byte[] pixels = original.Pixels.ToArray();
        Fill(
            pixels,
            original.Width,
            x: 45,
            y: 256,
            width: 130,
            height: 51,
            value: 180);
        Fill(
            pixels,
            original.Width,
            x: 45,
            y: 307,
            width: 130,
            height: 55,
            value: 180);
        ImageFrame changedArtwork = new(
            original.Width,
            original.Height,
            original.Format,
            pixels,
            takeOwnership: true);

        Assert.Equal(
            1,
            LoadPack().SelectedMap(changedArtwork));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void MapAndDifficultySelections_RequireTheSelectorOwnerAcrossRepresentativeStates()
    {
        string datasetRoot = Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions");
        if (!Directory.Exists(datasetRoot))
        {
            return;
        }

        CompiledDetectorPack pack = LoadPack();
        List<string> failures = [];
        foreach (string file in RepresentativeSamples(
                     datasetRoot)
                 .Where(path =>
                     !Path.GetFileName(
                             Path.GetDirectoryName(path)!)
                         .StartsWith(
                             "Expedition_Map_Select_",
                             StringComparison.OrdinalIgnoreCase)))
        {
            ImageFrame image = ImageCodec.Load(file);
            if (image.Width != pack.Manifest.ClientWidth ||
                image.Height != pack.Manifest.ClientHeight)
            {
                continue;
            }

            int? map = pack.SelectedMap(image);
            int? difficulty = pack.SelectedDifficulty(image);
            if (map is not null || difficulty is not null)
            {
                failures.Add(
                    $"{Path.GetRelativePath(datasetRoot, file)}: " +
                    $"map={map?.ToString() ?? "none"}, " +
                    $"difficulty={difficulty?.ToString() ?? "none"}");
            }
        }

        Assert.Empty(failures);
    }

    private static IEnumerable<string> RepresentativeSamples(
        string datasetRoot)
    {
        string expeditionRoot =
            Path.Combine(datasetRoot, "expeditions");
        foreach (string directory in
                 Directory.EnumerateDirectories(
                     expeditionRoot))
        {
            yield return Directory.EnumerateFiles(
                    directory,
                    "*.png")
                .Order(StringComparer.OrdinalIgnoreCase)
                .First();
        }

        foreach (string domain in new[]
                 {
                     "challenges",
                     "events",
                     "navigation-variants",
                     "refuel",
                     "settings",
                     "stages",
                 })
        {
            yield return Directory.EnumerateFiles(
                    Path.Combine(datasetRoot, domain),
                    "*.png",
                    SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
                .First();
        }
    }

    private static CompiledDetectorPack LoadPack()
    {
        DetectorPackManifest manifest =
            JsonFileStore.ReadAsync<DetectorPackManifest>(
                    Path.Combine(
                        TestPaths.DetectorPack,
                        "manifest.json"))
                .GetAwaiter()
                .GetResult() ??
            throw new InvalidDataException(
                "Detector pack manifest is missing.");
        return new CompiledDetectorPack(
            TestPaths.DetectorPack,
            manifest);
    }

    private static void Clear(
        byte[] pixels,
        int imageWidth,
        int x,
        int y,
        int width,
        int height)
    {
        Fill(
            pixels,
            imageWidth,
            x,
            y,
            width,
            height,
            value: 0);
    }

    private static void Fill(
        byte[] pixels,
        int imageWidth,
        int x,
        int y,
        int width,
        int height,
        byte value)
    {
        for (int currentY = y;
             currentY < y + height;
             currentY++)
        {
            Array.Fill(
                pixels,
                value,
                (currentY * imageWidth + x) * 3,
                width * 3);
        }
    }
}
