using System.Text;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class DetectorPackGoldenTests
{
    private static readonly Lazy<CompiledDetectorPack> Pack = new(LoadPack);

    private static readonly IReadOnlyDictionary<string, string[]> StateDatasets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["disconnect"] = ["Roblox_Disconnect"],
        ["lobby"] = ["Lobby_UI", "Lobby_UI2"],
        ["play"] = ["Play_UI"],
        ["map_select"] =
        [
            "Expedition_Map_Select_Map1", "Expedition_Map_Select_Map2", "Expedition_Map_Select_Map3",
            "Expedition_Map_Select_Difficultly1", "Expedition_Map_Select_Difficultly2", "Expedition_Map_Select_Difficultly3",
            "Expedition_Map_Select_Difficultly3_Animation",
        ],
        ["map_preview"] = ["Expedition_Map_Preview_Map1"],
        ["victory"] = ["Expedition_Victory_UI"],
        ["defeat"] = ["Expedition_Defeat_UI"],
        ["extract_confirm"] = ["Expedition_Checkpoint_Extract_Confirm"],
        ["reward"] = ["Expedition_Reward_Select", "Expedition_Reward_Select2", "Expedition_Reward_Select3", "Expedition_Reward_Select4"],
        ["confirm"] = ["Expedition_Continue_Button_Confirm"],
        ["checkpoint"] = ["Expedition_Checkpoint", "Expedition_Checkpoint_Node"],
        ["start"] = ["Expedition_Map1_Prestart", "Expedition_Midgame_Start"],
        ["continue"] = ["Expedition_Continue_Button"],
    };

    [Fact]
    [Trait("Category", "Golden")]
    public void EveryCapturedUiDataset_MeetsItsCompiledStateThreshold()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        StringBuilder failures = new();
        int checkedImages = 0;
        foreach ((string state, string[] datasets) in StateDatasets)
        {
            DetectorStateDefinition definition = pack.Manifest.States.Single(value => value.Name == state);
            foreach (string dataset in datasets)
            {
                foreach (string file in Pngs(dataset))
                {
                    ImageFrame image = ImageCodec.Load(file);
                    double score = pack.ScoreStates(image)[state];
                    checkedImages++;
                    if (score + 1e-9 < definition.Threshold)
                    {
                        failures.AppendLine($"{state,-16} {score:P1} < {definition.Threshold:P0}  {Path.GetFileName(file)} ({dataset})");
                    }
                }
            }
        }

        Assert.Equal(195, checkedImages);
        Assert.True(failures.Length == 0, $"Compiled detector regressions:{Environment.NewLine}{failures}");
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void RewardDetector_IsRarityIndependentAcrossAllFourCardDatasets()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        DetectorStateDefinition reward = pack.Manifest.States.Single(value => value.Name == "reward");
        foreach (string dataset in StateDatasets["reward"])
        {
            double minimum = Pngs(dataset)
                .Select(path => pack.ScoreStates(ImageCodec.Load(path))["reward"])
                .Min();
            Assert.True(minimum >= reward.Threshold, $"{dataset} minimum reward score was {minimum:P1}.");
        }
    }

    [Theory]
    [InlineData(1, "Expedition_Map_Select_Map1")]
    [InlineData(2, "Expedition_Map_Select_Map2")]
    [InlineData(3, "Expedition_Map_Select_Map3")]
    [Trait("Category", "Golden")]
    public void SelectedMap_RecognizesEveryCapturedMap(int expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        foreach (string file in Pngs(dataset)) Assert.Equal(expected, Pack.Value.SelectedMap(ImageCodec.Load(file)));
    }

    [Theory]
    [InlineData(1, "Expedition_Map_Select_Difficultly1")]
    [InlineData(2, "Expedition_Map_Select_Difficultly2")]
    [InlineData(3, "Expedition_Map_Select_Difficultly3")]
    [InlineData(1, "Expedition_Map_Select_Difficultly1_LayoutShift")]
    [InlineData(2, "Expedition_Map_Select_Difficultly2_LayoutShift")]
    [InlineData(3, "Expedition_Map_Select_Difficultly3_LayoutShift")]
    [Trait("Category", "Golden")]
    public void SelectedDifficulty_UsesOneActiveMatch(int expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        foreach (string file in Pngs(dataset)) Assert.Equal(expected, Pack.Value.SelectedDifficulty(ImageCodec.Load(file)));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void DifficultyAnimation_TracksTheVisibleTwoToThreeTransition()
    {
        if (!DatasetsAvailable()) return;
        int?[] selected = Pngs("Expedition_Map_Select_Difficultly3_Animation")
            .Select(file => Pack.Value.SelectedDifficulty(ImageCodec.Load(file)))
            .ToArray();
        Assert.Equal(2, selected[0]);
        Assert.Equal(3, selected[^1]);
        Assert.DoesNotContain(1, selected);
        Assert.All(selected, value => Assert.True(value is 2 or 3, $"Animation produced an ambiguous difficulty value: {value?.ToString() ?? "none"}."));
    }

    [Theory]
    [InlineData("defense", "Expedition_Defense_Node")]
    [InlineData("assault", "Expedition_Assault_Node")]
    [InlineData("elite", "Expedition_Elite_Node")]
    [InlineData("boss", "Expedition_Boss_Node")]
    [InlineData("checkpoint", "Expedition_Checkpoint_Node")]
    [Trait("Category", "Golden")]
    public void NodeHue_RecognizesCapturedNodeBars(string expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        foreach (string file in Pngs(dataset)) Assert.Equal(expected, Pack.Value.CurrentNodeType(ImageCodec.Load(file)));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void EmptyHotbar_HasNoRemainingConfiguredUnits()
    {
        if (!DatasetsAvailable()) return;
        HashSet<int> configured = [1, 2, 3, 4, 5, 6];
        foreach (string file in Pngs("Expedition_Empty_Unit_Bar"))
        {
            Assert.Empty(Pack.Value.RemainingUnitKeys(ImageCodec.Load(file), configured));
        }
    }

    private static CompiledDetectorPack LoadPack()
    {
        DetectorPackManifest manifest = JsonFileStore.ReadAsync<DetectorPackManifest>(Path.Combine(TestPaths.DetectorPack, "manifest.json")).GetAwaiter().GetResult()
            ?? throw new InvalidDataException("Detector pack manifest is missing.");
        return new CompiledDetectorPack(TestPaths.DetectorPack, manifest);
    }

    private static IEnumerable<string> Pngs(string dataset) =>
        Directory.EnumerateFiles(Path.Combine(TestPaths.Datasets, dataset), "*.png").Order(StringComparer.OrdinalIgnoreCase);

    private static bool DatasetsAvailable() => Directory.Exists(TestPaths.Datasets);
}
