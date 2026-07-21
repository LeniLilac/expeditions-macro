using ExpeditionsMacro.Automation.Challenges;
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

    [Fact]
    public async Task ReportedPublicReleaseSelector_RecognizesKingsSchoolAndRose()
    {
        IDetectorPack detector = await LoadDetectorAsync();
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_12.png");
        var frame = ImageCodec.Load(file);

        Assert.True(detector.SupportsChallengeMaps);
        Assert.Equal(ChallengeMapId.KingsTomb, detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Equal(ChallengeMapId.SchoolGrounds, detector.ChallengeMapForType(frame, ChallengeType.Stat));
        Assert.Equal(ChallengeMapId.RoseKingdom, detector.ChallengeMapForType(frame, ChallengeType.Sprite));
    }

    [Fact]
    public async Task LegacyPack_ReportsMissingChallengeReferencesInsteadOfAThumbnailMiss()
    {
        IDetectorPack detector = await LoadDetectorAsync(TestPaths.LegacyDetectorPack);
        string file = Path.Combine(TestPaths.ChallengeDatasets, "ChallengeList", "ChallengeList_12.png");
        var frame = ImageCodec.Load(file);

        Assert.Equal(29, detector.Manifest.Files.Count);
        Assert.False(detector.SupportsChallengeMaps);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => detector.ChallengeMapForType(frame, ChallengeType.Trait));
        Assert.Contains("Update the detector pack in Settings or reinstall Expeditions Macro", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("thumbnail", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChallengeRunner_FailsFastForTheUnsupported29FilePack()
    {
        IDetectorPack detector = await LoadDetectorAsync(TestPaths.LegacyDetectorPack);
        ChallengePreset preset = new()
        {
            Id = "unsupported-pack",
            Name = "Unsupported pack",
            Maps = Enum.GetValues<ChallengeMapId>()
                .Select(map => new ChallengeMapProfile
                {
                    Map = map,
                    CameraModelId = "camera",
                    PrestartPlacementModelId = "placement",
                })
                .ToArray(),
        };
        ChallengeMacroRunner runner = new(null!, null!, null!, null!);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(
            preset,
            new Dictionary<ChallengeMapId, ChallengeMapRuntimeModels>(),
            detector,
            string.Empty,
            'P'));

        Assert.Contains("does not include the Challenge map references", error.Message, StringComparison.Ordinal);
        Assert.Contains("Update the detector pack in Settings or reinstall Expeditions Macro", error.Message, StringComparison.Ordinal);
    }

    private static async Task<IDetectorPack> LoadDetectorAsync(string? directory = null)
    {
        directory ??= TestPaths.DetectorPack;
        DetectorPackManifest manifest = await JsonFileStore.ReadAsync<DetectorPackManifest>(Path.Combine(directory, "manifest.json"))
            ?? throw new InvalidDataException("Test detector manifest is missing.");
        return new CompiledDetectorPack(directory, manifest);
    }
}
