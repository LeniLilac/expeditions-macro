using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class ModeDetailVariantTests
{
    private static readonly Lazy<CompiledDetectorPack> Pack = new(LoadPack);

    [Theory]
    [InlineData("Lobby_StoryDetail_01.png", StageScreenState.StoryDetail, 200, 315)]
    [InlineData("PostMatch_StoryDetail_01.png", StageScreenState.StoryDetail, 300, 405)]
    [InlineData("Lobby_RaidDetail_01.png", StageScreenState.RaidDetail, 200, 315)]
    [InlineData("PostMatch_RaidDetail_01.png", StageScreenState.RaidDetail, 300, 405)]
    public void StageDetails_MapSelectStageWithOrWithoutMatchmaking(
        string fileName,
        StageScreenState expected,
        int minimumX,
        int maximumX)
    {
        ImageFrame image = Load(fileName);
        StageScreenMatch match = StageScreenDetector.Detect(image);

        Assert.Equal(expected, match.State);
        Assert.InRange(match.Confidence, 0.78, 1);
        Assert.InRange(match.ActionX!.Value, minimumX, maximumX);
        Assert.InRange(match.ActionY!.Value, 410, 460);
    }

    [Theory]
    [InlineData("Lobby_ChallengeDetail_01.png")]
    [InlineData("PostMatch_ChallengeDetail_01.png")]
    public void ChallengeDetails_MapSelectStageWithOrWithoutMatchmaking(string fileName)
    {
        ImageFrame image = Load(fileName);
        ChallengeScreenMatch match = ChallengeScreenDetector.Detect(image);

        Assert.Equal(ChallengeScreenState.ChallengeAvailable, match.State);
        Assert.InRange(match.Confidence, ChallengeScreenDetector.Threshold(ChallengeScreenState.ChallengeAvailable), 1);
        Assert.InRange(match.ActionX!.Value, 400, 600);
        Assert.InRange(match.ActionY!.Value, 400, 460);
    }

    [Theory]
    [InlineData("Lobby_ExpeditionDetail_01.png")]
    [InlineData("PostMatch_ExpeditionDetail_01.png")]
    public void ExpeditionDetails_MapSelectStageWithOrWithoutMatchmaking(string fileName)
    {
        ImageFrame image = Load(fileName);
        IReadOnlyDictionary<string, double> scores = Pack.Value.ScoreStates(image);
        DetectorStateDefinition mapSelect = Pack.Value.Manifest.States.Single(state => state.Name == "map_select");
        (int x, int y) = Pack.Value.ActionFor("map_select", image);

        Assert.True(scores["map_select"] >= mapSelect.Threshold, $"Map selection scored {scores["map_select"]:P1}.");
        Assert.Equal("map_select", Pack.Value.Classify(scores));
        Assert.InRange(x, 670, 790);
        Assert.InRange(y, 500, 570);
    }

    private static ImageFrame Load(string fileName) =>
        ImageCodec.Load(Path.Combine(TestPaths.NavigationVariantDatasets, fileName));

    private static CompiledDetectorPack LoadPack()
    {
        DetectorPackManifest manifest = JsonFileStore.ReadAsync<DetectorPackManifest>(
            Path.Combine(TestPaths.DetectorPack, "manifest.json")).GetAwaiter().GetResult()
            ?? throw new InvalidDataException("Detector pack manifest is missing.");
        return new CompiledDetectorPack(TestPaths.DetectorPack, manifest);
    }
}
