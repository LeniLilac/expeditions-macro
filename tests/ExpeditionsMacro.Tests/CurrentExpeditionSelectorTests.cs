using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class CurrentExpeditionSelectorTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void Selector_ReportsLiveStateAndActions()
    {
        if (!Directory.Exists(TestPaths.Datasets))
        {
            return;
        }
        CompiledDetectorPack pack = LoadPack();
        ImageFrame image = ImageCodec.Load(
            Path.Combine(
                TestPaths.Datasets,
                "Expedition_Map_Select_Difficultly3",
                "CurrentUI-Map1.png"));

        Assert.Equal(
            "map_select",
            pack.RecoveryState(image));
        Assert.Equal(1, pack.SelectedMap(image));
        Assert.Equal(3, pack.SelectedDifficulty(image));
        Assert.Equal(
            (82, 123),
            pack.ActionFor("map_1", image));
        Assert.Equal(
            (82, 199),
            pack.ActionFor("map_2", image));
        Assert.Equal(
            (82, 273),
            pack.ActionFor("map_3", image));
        Assert.Equal(
            (197, 448),
            pack.ActionFor(
                "difficulty_minus",
                image));
        Assert.Equal(
            (310, 448),
            pack.ActionFor(
                "difficulty_plus",
                image));

        (int stageX, int stageY) =
            pack.ActionFor("select_stage", image);
        Assert.InRange(stageX, 245, 263);
        Assert.InRange(stageY, 582, 596);
    }

    private static CompiledDetectorPack LoadPack()
    {
        DetectorPackManifest manifest =
            JsonFileStore
                .ReadAsync<DetectorPackManifest>(
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
}
