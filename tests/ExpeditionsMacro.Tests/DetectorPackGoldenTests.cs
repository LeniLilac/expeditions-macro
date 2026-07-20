using System.Text;
using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;
using OpenCvSharp;

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

        Assert.Equal(203, checkedImages);
        Assert.True(failures.Length == 0, $"Compiled detector regressions:{Environment.NewLine}{failures}");
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void EveryCapturedUiDataset_ClassifiesAsItsExpectedState()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        StringBuilder failures = new();
        int checkedImages = 0;
        foreach ((string expected, string[] datasets) in StateDatasets)
        {
            foreach (string dataset in datasets)
            {
                foreach (string file in Pngs(dataset))
                {
                    IReadOnlyDictionary<string, double> scores = pack.ScoreStates(ImageCodec.Load(file));
                    string? actual = pack.Classify(scores);
                    checkedImages++;
                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        string passing = string.Join(", ", pack.Manifest.States
                            .Where(state => scores[state.Name] >= state.Threshold)
                            .Select(state => $"{state.Name}={scores[state.Name]:P1}"));
                        failures.AppendLine($"expected {expected,-16} got {actual ?? "none",-16} {dataset}/{Path.GetFileName(file)}; passing: {passing}");
                    }
                }
            }
        }

        Assert.Equal(203, checkedImages);
        Assert.True(failures.Length == 0, $"Cross-state detector regressions:{Environment.NewLine}{failures}");
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void StartDetector_IgnoresThePlayerAvatarRow()
    {
        if (!DatasetsAvailable()) return;
        string file = Pngs("Expedition_Map1_Prestart").Last();
        ImageFrame original = ImageCodec.Load(file);
        byte[] pixels = original.Pixels.ToArray();
        for (int y = 141; y < 168; y++)
        {
            for (int x = 330; x < 478; x++)
            {
                int offset = (y * original.Width + x) * 3;
                bool alternate = (x + y) % 2 == 0;
                pixels[offset] = alternate ? (byte)255 : (byte)225;
                pixels[offset + 1] = alternate ? (byte)0 : (byte)225;
                pixels[offset + 2] = alternate ? (byte)220 : (byte)225;
            }
        }
        ImageFrame changedAvatar = new(original.Width, original.Height, original.Format, pixels, takeOwnership: true);

        double originalScore = Pack.Value.ScoreStates(original)["start"];
        double changedScore = Pack.Value.ScoreStates(changedAvatar)["start"];
        double threshold = Pack.Value.Manifest.States.Single(value => value.Name == "start").Threshold;

        Assert.True(originalScore >= Math.Max(threshold, 0.95), $"Avatar-variant score was {originalScore:P1}.");
        Assert.Equal(originalScore, changedScore, precision: 12);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void PlayDetector_RecognizesVariableMapContentAndUiScale()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame image = ImageCodec.Load(Pngs("Play_UI").Last());
        double threshold = pack.Manifest.States.Single(value => value.Name == "play").Threshold;
        IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);

        Assert.True(scores["play"] >= threshold, $"Variable Play-screen score was {scores["play"]:P1}.");
        Assert.Equal("play", pack.Classify(scores));
        Assert.Equal("play", pack.RecoveryState(image));
        (int x, int y) = pack.ActionFor("play", image);
        Assert.InRange(x, 650, 730);
        Assert.InRange(y, 200, 260);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void PlayDetector_DoesNotStealAScaledLobbyFrame()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame lobby = ImageCodec.Load(Pngs("Lobby_UI").First());
        ImageFrame transformed = Transform(lobby, 0.90, 0, -12);
        double playThreshold = pack.Manifest.States.Single(value => value.Name == "play").Threshold;
        double playScore = pack.ScoreStates(transformed)["play"];

        Assert.True(playScore < playThreshold, $"Scaled lobby received a {playScore:P1} Play score.");
        Assert.Equal("lobby", pack.RecoveryState(transformed));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void StartDetector_RecognizesTheHoveredButtonFromTheReportedStall()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        double threshold = pack.Manifest.States.Single(value => value.Name == "start").Threshold;
        string[] files = Pngs("Expedition_Midgame_Start")
            .Where(path => Path.GetFileName(path).Contains("Hover", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(4, files.Length);
        foreach (string file in files)
        {
            ImageFrame image = ImageCodec.Load(file);
            IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);
            Assert.True(scores["start"] >= threshold, $"Hovered Start score was {scores["start"]:P1} for {Path.GetFileName(file)}.");
            Assert.Equal("start", pack.Classify(scores));
            (int x, int y) = pack.ActionFor("start", image);
            Assert.InRange(x, 390, 418);
            Assert.InRange(y, 165, 195);
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void AfkChamberDetector_ReturnsToLobbyAcrossTheReportedFrames()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        string[] files = Pngs("AFK_Chamber").ToArray();

        Assert.Equal(5, files.Length);
        foreach (string file in files)
        {
            ImageFrame image = ImageCodec.Load(file);
            IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);
            Assert.True(scores["afk"] >= 0.84, $"AFK score was {scores["afk"]:P1} for {Path.GetFileName(file)}.");
            Assert.Equal("afk", pack.Classify(scores));
            Assert.Equal("afk", pack.RecoveryState(image));
            (int x, int y) = pack.ActionFor("afk", image);
            Assert.InRange(x, 445, 495);
            Assert.InRange(y, 565, 602);
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void AfkChamberDetector_DoesNotMatchOtherCapturedUiStates()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        List<string> failures = [];
        foreach (string[] datasets in StateDatasets.Values)
        {
            foreach (string dataset in datasets)
            {
                foreach (string file in Pngs(dataset))
                {
                    double score = pack.ScoreStates(ImageCodec.Load(file))["afk"];
                    if (score >= 0.84) failures.Add($"{score:P1} {dataset}/{Path.GetFileName(file)}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"AFK detector false matches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void AfkChamberDetector_ToleratesUiScaleAndTranslation()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame original = ImageCodec.Load(Pngs("AFK_Chamber").First());
        ImageFrame transformed = Transform(original, 0.90, 0, -12);

        Assert.Equal("afk", pack.RecoveryState(transformed));
        (int x, int y) = pack.ActionFor("afk", transformed);
        (int expectedX, int expectedY) = TransformPoint(469, 584, original.Width, original.Height, 0.90, 0, -12);
        Assert.InRange(Math.Abs(x - expectedX), 0, 14);
        Assert.InRange(Math.Abs(y - expectedY), 0, 14);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void StartDetector_DoesNotMatchOtherCapturedUiStates()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        double threshold = pack.Manifest.States.Single(value => value.Name == "start").Threshold;
        List<string> failures = [];
        // Reward selection can intentionally appear over a live Start dialog.
        // Reward has higher state priority and is covered separately below.
        foreach ((string state, string[] datasets) in StateDatasets.Where(pair => pair.Key is not "start" and not "reward"))
        {
            foreach (string dataset in datasets)
            {
                foreach (string file in Pngs(dataset))
                {
                    double score = pack.ScoreStates(ImageCodec.Load(file))["start"];
                    if (score >= threshold) failures.Add($"{state}: {score:P1} {dataset}/{Path.GetFileName(file)}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"Start detector false matches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void RewardSelection_RemainsAuthoritativeOverAnUnderlyingStartDialog()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame image = ImageCodec.Load(Pngs("Expedition_Reward_Select").First());
        IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);

        Assert.True(scores["start"] >= pack.Manifest.States.Single(value => value.Name == "start").Threshold);
        Assert.Equal("reward", pack.Classify(scores));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void PauseButtonDetectors_DoNotMatchOtherCapturedUiStates()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        double continueThreshold = pack.Manifest.States.Single(value => value.Name == "continue").Threshold;
        double checkpointThreshold = pack.Manifest.States.Single(value => value.Name == "checkpoint").Threshold;
        List<string> failures = [];
        foreach ((string state, string[] datasets) in StateDatasets.Where(pair => pair.Key is not "continue" and not "checkpoint"))
        {
            foreach (string dataset in datasets)
            {
                foreach (string file in Pngs(dataset))
                {
                    IReadOnlyDictionary<string, double> scores = pack.ScoreStates(ImageCodec.Load(file));
                    if (scores["continue"] >= continueThreshold) failures.Add($"continue matched {state}: {scores["continue"]:P1} {dataset}/{Path.GetFileName(file)}");
                    if (scores["checkpoint"] >= checkpointThreshold) failures.Add($"checkpoint matched {state}: {scores["checkpoint"]:P1} {dataset}/{Path.GetFileName(file)}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void ContinueDetector_TracksTheMovedButtonFromTheReportedClip()
    {
        if (!DatasetsAvailable()) return;
        string file = Pngs("Expedition_Continue_Button").Last();
        ImageFrame image = ImageCodec.Load(file);
        CompiledDetectorPack pack = Pack.Value;

        Assert.True(pack.ScoreStates(image)["continue"] >= 0.99);
        (int x, int y) = pack.ActionFor("continue", image);
        Assert.InRange(x, 398, 407);
        Assert.InRange(y, 476, 487);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void CheckpointDetector_UsesTheVisibleExtractAndContinuePair()
    {
        if (!DatasetsAvailable()) return;
        ImageFrame image = ImageCodec.Load(Pngs("Expedition_Checkpoint").First());
        CompiledDetectorPack pack = Pack.Value;

        Assert.True(pack.ScoreStates(image)["checkpoint"] >= 0.99);
        (int continueX, int continueY) = pack.ActionFor("checkpoint", image);
        (int extractX, int extractY) = pack.ActionFor("extract", image);
        Assert.InRange(continueX, 442, 453);
        Assert.InRange(extractX, 354, 365);
        Assert.InRange(Math.Abs(continueY - extractY), 0, 2);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void ReportedContinueExpeditionDialog_ClicksTheModalInsteadOfTheUnderlyingPause()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame image = ImageCodec.Load(Pngs("Expedition_Continue_Button_Confirm").Last());
        IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);

        Assert.Equal("confirm", pack.Classify(scores));
        Assert.True(scores["confirm"] >= 0.98, $"Reported confirmation score was {scores["confirm"]:P1}.");
        (int x, int y) = pack.ActionFor("confirm", image);
        Assert.InRange(x, 332, 348);
        Assert.InRange(y, 332, 348);
    }

    [Theory]
    [InlineData("disconnect", "Roblox_Disconnect")]
    [InlineData("lobby", "Lobby_UI")]
    [InlineData("play", "Play_UI")]
    [InlineData("map_select", "Expedition_Map_Select_Map1")]
    [InlineData("map_preview", "Expedition_Map_Preview_Map1")]
    [InlineData("victory", "Expedition_Victory_UI")]
    [InlineData("defeat", "Expedition_Defeat_UI")]
    [InlineData("extract_confirm", "Expedition_Checkpoint_Extract_Confirm")]
    [InlineData("reward", "Expedition_Reward_Select")]
    [InlineData("confirm", "Expedition_Continue_Button_Confirm")]
    [InlineData("checkpoint", "Expedition_Checkpoint")]
    [InlineData("start", "Expedition_Map1_Prestart")]
    [InlineData("continue", "Expedition_Continue_Button")]
    [Trait("Category", "Golden")]
    public void StateDetectionAndActions_TolerateTwentyPixelVerticalTranslation(string state, string dataset)
    {
        if (!DatasetsAvailable()) return;
        AssertTransformedState(state, dataset, scale: 1, deltaX: 0, deltaY: -20);
    }

    [Theory]
    [InlineData("disconnect", "Roblox_Disconnect")]
    [InlineData("lobby", "Lobby_UI")]
    [InlineData("play", "Play_UI")]
    [InlineData("map_select", "Expedition_Map_Select_Map1")]
    [InlineData("map_preview", "Expedition_Map_Preview_Map1")]
    [InlineData("victory", "Expedition_Victory_UI")]
    [InlineData("defeat", "Expedition_Defeat_UI")]
    [InlineData("extract_confirm", "Expedition_Checkpoint_Extract_Confirm")]
    [InlineData("reward", "Expedition_Reward_Select")]
    [InlineData("confirm", "Expedition_Continue_Button_Confirm")]
    [InlineData("checkpoint", "Expedition_Checkpoint")]
    [InlineData("start", "Expedition_Map1_Prestart")]
    [InlineData("continue", "Expedition_Continue_Button")]
    [Trait("Category", "Golden")]
    public void StateDetectionAndActions_TolerateTenPercentUiScaleDifference(string state, string dataset)
    {
        if (!DatasetsAvailable()) return;
        AssertTransformedState(state, dataset, scale: 0.90, deltaX: 0, deltaY: 0);
    }

    [Theory]
    [InlineData("disconnect", "Roblox_Disconnect")]
    [InlineData("lobby", "Lobby_UI")]
    [InlineData("play", "Play_UI")]
    [InlineData("map_select", "Expedition_Map_Select_Map1")]
    [InlineData("map_preview", "Expedition_Map_Preview_Map1")]
    [InlineData("victory", "Expedition_Victory_UI")]
    [InlineData("defeat", "Expedition_Defeat_UI")]
    [InlineData("extract_confirm", "Expedition_Checkpoint_Extract_Confirm")]
    [InlineData("reward", "Expedition_Reward_Select")]
    [InlineData("confirm", "Expedition_Continue_Button_Confirm")]
    [InlineData("checkpoint", "Expedition_Checkpoint")]
    [InlineData("start", "Expedition_Map1_Prestart")]
    [InlineData("continue", "Expedition_Continue_Button")]
    [Trait("Category", "Golden")]
    public void StateDetectionAndActions_TolerateCombinedScaleAndTranslation(string state, string dataset)
    {
        if (!DatasetsAvailable()) return;
        AssertTransformedState(state, dataset, scale: 0.90, deltaX: 0, deltaY: -12);
    }

    [Theory]
    [InlineData("disconnect", "Roblox_Disconnect")]
    [InlineData("lobby", "Lobby_UI")]
    [InlineData("play", "Play_UI")]
    [InlineData("map_select", "Expedition_Map_Select_Map1")]
    [InlineData("map_preview", "Expedition_Map_Preview_Map1")]
    [Trait("Category", "Golden")]
    public void RecoveryDetection_ToleratesTranslatedAndScaledLayout(string expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        ImageFrame transformed = Transform(ImageCodec.Load(Pngs(dataset).First()), 0.90, 0, -12);
        Assert.Equal(expected, Pack.Value.RecoveryState(transformed));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void MapControls_ClickTheirTransformedLocations()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame original = ImageCodec.Load(Pngs("Expedition_Map_Select_Map1").First());
        const double scale = 0.90;
        const int deltaX = 0;
        const int deltaY = -12;
        ImageFrame transformed = Transform(original, scale, deltaX, deltaY);
        foreach (string action in new[] { "map_1", "map_2", "map_3", "difficulty_minus", "difficulty_plus", "select_stage" })
        {
            (int originalX, int originalY) = pack.ActionFor(action, original);
            (int expectedX, int expectedY) = TransformPoint(originalX, originalY, original.Width, original.Height, scale, deltaX, deltaY);
            (int actualX, int actualY) = pack.ActionFor(action, transformed);
            double distance = Math.Sqrt(Math.Pow(actualX - expectedX, 2) + Math.Pow(actualY - expectedY, 2));
            Assert.True(distance <= 14, $"{action} transformed action was ({actualX}, {actualY}); expected near ({expectedX}, {expectedY}).");
        }
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

    [Fact]
    [Trait("Category", "Golden")]
    public void RewardDetector_RecognizesCollapsedCardTransitionsWithoutTriggeringRecovery()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        DetectorStateDefinition reward = pack.Manifest.States.Single(value => value.Name == "reward");
        foreach (string file in Pngs("Expedition_Reward_Transition"))
        {
            ImageFrame image = ImageCodec.Load(file);
            IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);
            Assert.True(scores["reward"] >= reward.Threshold, $"Transition reward score was {scores["reward"]:P1} for {Path.GetFileName(file)}.");
            Assert.Equal("reward", pack.Classify(scores));
            Assert.Null(pack.RecoveryState(image));
            (int x, int y) = pack.ActionFor("reward", image);
            Assert.InRange(x, 160, 540);
            Assert.InRange(y, 320, 410);
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void RewardAndRecoveryDetectors_IgnoreReportedGameplayFalsePositives()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        DetectorStateDefinition reward = pack.Manifest.States.Single(value => value.Name == "reward");
        foreach (string file in Pngs("Expedition_Gameplay_Negative"))
        {
            ImageFrame image = ImageCodec.Load(file);
            IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);
            Assert.True(scores["reward"] < reward.Threshold, $"Gameplay reward score was {scores["reward"]:P1} for {Path.GetFileName(file)}.");
            Assert.Null(pack.RecoveryState(image));
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void ActiveGameplayNavigationCollisions_CannotStartRecoveryOrClickAnAction()
    {
        if (!DatasetsAvailable()) return;
        CompiledDetectorPack pack = Pack.Value;
        foreach (string file in Pngs("Expedition_Recovery_Navigation_Negative"))
        {
            ImageFrame image = ImageCodec.Load(file);
            IReadOnlyDictionary<string, double> scores = pack.ScoreStates(image);
            string? classified = pack.Classify(scores);
            string? active = ExpeditionRunPolicy.PreferActiveState(pack.Manifest, scores, classified);
            string? recovery = pack.RecoveryState(image);

            Assert.False(
                ExpeditionRunPolicy.CanEnterRecoveryDuringRun(recovery),
                $"Gameplay frame could enter recovery as {recovery} for {Path.GetFileName(file)}.");
            Assert.Null(active);
        }
    }

    [Theory]
    [InlineData(1, "Expedition_Map_Select_Map1")]
    [InlineData(2, "Expedition_Map_Select_Map2")]
    [InlineData(3, "Expedition_Map_Select_Map3")]
    [Trait("Category", "Golden")]
    public void SelectedMap_ToleratesTranslatedAndScaledLayout(int expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        ImageFrame transformed = Transform(ImageCodec.Load(Pngs(dataset).First()), 0.90, 0, -12);
        Assert.Equal(expected, Pack.Value.SelectedMap(transformed));
    }

    [Theory]
    [InlineData(1, "Expedition_Map_Select_Difficultly1")]
    [InlineData(2, "Expedition_Map_Select_Difficultly2")]
    [InlineData(3, "Expedition_Map_Select_Difficultly3")]
    [Trait("Category", "Golden")]
    public void SelectedDifficulty_ToleratesTranslatedAndScaledLayout(int expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        ImageFrame transformed = Transform(ImageCodec.Load(Pngs(dataset).First()), 0.90, 0, -12);
        Assert.Equal(expected, Pack.Value.SelectedDifficulty(transformed));
    }

    [Theory]
    [InlineData("defense", "Expedition_Defense_Node")]
    [InlineData("assault", "Expedition_Assault_Node")]
    [InlineData("elite", "Expedition_Elite_Node")]
    [InlineData("boss", "Expedition_Boss_Node")]
    [InlineData("checkpoint", "Expedition_Checkpoint_Node")]
    [Trait("Category", "Golden")]
    public void NodeHue_ToleratesTranslatedAndScaledProgressBar(string expected, string dataset)
    {
        if (!DatasetsAvailable()) return;
        ImageFrame transformed = Transform(ImageCodec.Load(Pngs(dataset).First()), 0.90, 0, -12);
        Assert.Equal(expected, Pack.Value.CurrentNodeType(transformed));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void EmptyHotbar_ToleratesTranslatedAndScaledLayout()
    {
        if (!DatasetsAvailable()) return;
        HashSet<int> configured = [1, 2, 3, 4, 5, 6];
        ImageFrame transformed = Transform(ImageCodec.Load(Pngs("Expedition_Empty_Unit_Bar").First()), 0.90, 0, -12);
        Assert.Empty(Pack.Value.RemainingUnitKeys(transformed, configured));
    }

    private static void AssertTransformedState(string expectedState, string dataset, double scale, int deltaX, int deltaY)
    {
        CompiledDetectorPack pack = Pack.Value;
        ImageFrame original = ImageCodec.Load(Pngs(dataset).First());
        ImageFrame transformed = Transform(original, scale, deltaX, deltaY);
        IReadOnlyDictionary<string, double> scores = pack.ScoreStates(transformed);
        DetectorStateDefinition definition = pack.Manifest.States.Single(value => value.Name == expectedState);
        Assert.True(scores[expectedState] >= definition.Threshold, $"{expectedState} transformed score was {scores[expectedState]:P1}.");
        Assert.Equal(expectedState, pack.Classify(scores));

        (int originalX, int originalY) = pack.ActionFor(expectedState, original);
        (int expectedX, int expectedY) = TransformPoint(originalX, originalY, original.Width, original.Height, scale, deltaX, deltaY);
        (int actualX, int actualY) = pack.ActionFor(expectedState, transformed);
        double distance = Math.Sqrt(Math.Pow(actualX - expectedX, 2) + Math.Pow(actualY - expectedY, 2));
        Assert.True(distance <= 12, $"{expectedState} transformed action was ({actualX}, {actualY}); expected near ({expectedX}, {expectedY}).");
    }

    private static ImageFrame Transform(ImageFrame image, double scale, int deltaX, int deltaY)
    {
        using Mat source = ImageCodec.ToMat(image);
        using Mat target = new(image.Height, image.Width, source.Type(), Scalar.Black);
        double translateX = (1 - scale) * (image.Width - 1) / 2d + deltaX;
        double translateY = (1 - scale) * (image.Height - 1) / 2d + deltaY;
        using Mat matrix = new(2, 3, MatType.CV_64F);
        matrix.Set(0, 0, scale);
        matrix.Set(0, 1, 0d);
        matrix.Set(0, 2, translateX);
        matrix.Set(1, 0, 0d);
        matrix.Set(1, 1, scale);
        matrix.Set(1, 2, translateY);
        Cv2.WarpAffine(source, target, matrix, new Size(image.Width, image.Height), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        return ImageCodec.FromMat(target, PixelFormat.Rgb24);
    }

    private static (int X, int Y) TransformPoint(int x, int y, int width, int height, double scale, int deltaX, int deltaY) =>
        (
            (int)Math.Round((x - (width - 1) / 2d) * scale + (width - 1) / 2d + deltaX, MidpointRounding.AwayFromZero),
            (int)Math.Round((y - (height - 1) / 2d) * scale + (height - 1) / 2d + deltaY, MidpointRounding.AwayFromZero));

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
