using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengeMapDetectorTests
{
    [Fact]
    public async Task NewChallengeRun_RecognizesAllThreeSelectorRows()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_01.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(ChallengeMapId.RoseKingdom, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.FairyKingForest, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    [Fact]
    public async Task EarlierSelectorSet_CoversFlowerForestAndKingsTomb()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_03.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(ChallengeMapId.FlowerForest, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.KingsTomb, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    [Fact]
    public async Task DifferentPcSelectorSet_RecognizesKingsTombRoseAndSchool()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_06.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(ChallengeMapId.KingsTomb, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.RoseKingdom, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    [Fact]
    public async Task DifferentPcReset_RecognizesFairySchoolAndKingsTomb()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_07.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(ChallengeMapId.FairyKingForest, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.KingsTomb, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    [Fact]
    public async Task LargerChallengePanel_RecognizesFairySchoolAndRose()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_09.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(ChallengeMapId.FairyKingForest, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.RoseKingdom, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    [Fact]
    public async Task ChallengeRunThree_RecognizesSchoolRoseAndFlower()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_10.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.RoseKingdom, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.FlowerForest, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    private static async Task<IDetectorPack> LoadDetectorAsync()
    {
        DetectorPackManifest manifest = await JsonFileStore.ReadAsync<DetectorPackManifest>(Path.Combine(TestPaths.DetectorPack, "manifest.json"))
            ?? throw new InvalidDataException("Test detector manifest is missing.");
        return new CompiledDetectorPack(TestPaths.DetectorPack, manifest);
    }
}
