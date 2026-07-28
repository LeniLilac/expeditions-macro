using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Tests;

public sealed class DetectionOptimizationTests
{
    private static readonly string[] ExpeditionActiveStates =
    [
        "defeat",
        "victory",
        "extract_confirm",
        "confirm",
        "checkpoint",
        "continue",
        "start",
        "reward",
    ];

    [Fact]
    [Trait("Category", "Golden")]
    public void ChallengeMatchPath_PreservesOwnedStates()
    {
        AssertMatchEquivalence(
            TestPaths.Datasets,
            frame =>
            {
                ChallengeScreenMatch full =
                    ChallengeScreenDetector.Detect(frame);
                ChallengeScreenMatch optimized =
                    ChallengeScreenDetector
                        .DetectMatchState(frame);
                ChallengeScreenState expected =
                    full.State is
                        ChallengeScreenState.Victory or
                        ChallengeScreenState.Defeat or
                        ChallengeScreenState.GameModeSelector
                        ? full.State
                        : ChallengeScreenState.None;
                Assert.Equal(expected, optimized.State);
                if (expected != ChallengeScreenState.None)
                {
                    Assert.Equal(
                        full.Confidence,
                        optimized.Confidence,
                        precision: 12);
                }
            });
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void StageMatchPath_PreservesOwnedStates()
    {
        AssertMatchEquivalence(
            TestPaths.Datasets,
            frame =>
            {
                StageScreenMatch full =
                    StageScreenDetector.Detect(frame);
                StageScreenMatch optimized =
                    StageScreenDetector
                        .DetectMatchState(frame);
                StageScreenState expected =
                    full.State is
                        StageScreenState.Victory or
                        StageScreenState.Defeat or
                        StageScreenState.GameModeSelector
                        ? full.State
                        : StageScreenState.None;
                Assert.Equal(expected, optimized.State);
                if (expected != StageScreenState.None)
                {
                    Assert.Equal(
                        full.Confidence,
                        optimized.Confidence,
                        precision: 12);
                }
            });
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void EventMatchPath_PreservesOwnedStates()
    {
        AssertMatchEquivalence(
            TestPaths.Datasets,
            frame =>
            {
                EventScreenMatch full =
                    EventScreenDetector.Detect(frame);
                EventScreenMatch optimized =
                    EventScreenDetector
                        .DetectMatchState(frame);
                EventScreenState expected =
                    full.State is
                        EventScreenState.Victory or
                        EventScreenState.Defeat or
                        EventScreenState.GameModeSelector
                        ? full.State
                        : EventScreenState.None;
                Assert.Equal(expected, optimized.State);
                if (expected != EventScreenState.None)
                {
                    Assert.Equal(
                        full.Confidence,
                        optimized.Confidence,
                        precision: 12);
                }
            });
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void RootRecoveryPath_PreservesRootStates()
    {
        CompiledDetectorPack pack = LoadPack();
        foreach (string file in Directory.EnumerateFiles(
                     TestPaths.Datasets,
                     "*.png",
                     SearchOption.AllDirectories))
        {
            ImageFrame frame = ImageCodec.Load(file);
            string? full = pack.RecoveryState(frame);
            string? expected =
                full is "afk" or "disconnect" or "lobby"
                    ? full
                    : null;
            Assert.True(
                string.Equals(
                    expected,
                    pack.RootRecoveryState(frame),
                    StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(file)} expected " +
                $"{expected ?? "none"}.");
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void ExpeditionMatchSubset_PreservesEveryActiveScore()
    {
        CompiledDetectorPack pack = LoadPack();
        foreach (string file in Directory.EnumerateFiles(
                     TestPaths.Datasets,
                     "*.png",
                     SearchOption.AllDirectories))
        {
            ImageFrame frame = ImageCodec.Load(file);
            IReadOnlyDictionary<string, double> full =
                pack.ScoreStates(frame);
            IReadOnlyDictionary<string, double> optimized =
                pack.ScoreStates(
                    frame,
                    ExpeditionActiveStates);
            foreach (string state in ExpeditionActiveStates)
            {
                Assert.Equal(
                    full[state],
                    optimized[state],
                    precision: 12);
            }
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void ExpeditionSubset_IsCaseInsensitiveAndPreservesSyntheticAfk()
    {
        CompiledDetectorPack pack = LoadPack();
        ImageFrame frame = ImageCodec.Load(
            Directory.EnumerateFiles(
                    TestPaths.Datasets,
                    "*.png",
                    SearchOption.AllDirectories)
                .First());
        IReadOnlyDictionary<string, double> full =
            pack.ScoreStates(frame);
        IReadOnlyDictionary<string, double> subset =
            pack.ScoreStates(
                frame,
                ["Victory", "ReWaRd", "AFK"]);

        Assert.Equal(
            full["victory"],
            subset["Victory"],
            precision: 12);
        Assert.Equal(
            full["reward"],
            subset["ReWaRd"],
            precision: 12);
        Assert.Equal(
            full["afk"],
            subset["AFK"],
            precision: 12);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void AdaptiveSubset_IsCaseInsensitiveBeforeFullScoring()
    {
        CompiledDetectorPack pack = LoadPack();
        ImageFrame frame = ImageCodec.Load(
            Directory.EnumerateFiles(
                    Path.Combine(
                        TestPaths.Datasets,
                        "Lobby_UI"),
                    "*.png",
                    SearchOption.AllDirectories)
                .First());

        IReadOnlyDictionary<string, double> subset =
            pack.ScoreStates(
                frame,
                ["Lobby", "PLAY"]);
        IReadOnlyDictionary<string, double> full =
            pack.ScoreStates(frame);

        Assert.Equal(
            full["lobby"],
            subset["Lobby"],
            precision: 12);
        Assert.Equal(
            full["play"],
            subset["PLAY"],
            precision: 12);
    }

    [Fact]
    public async Task Benchmark_ReportsCaptureAndDetectionSeparately()
    {
        ImageFrame frame = ImageCodec.Load(
            Path.Combine(
                TestPaths.StageDatasets,
                "StoryVictory_Act_01.png"));
        CameraPoseTestAutomation automation =
            new(frame);
        DetectionBenchmarkService service =
            new(automation);

        DetectionBenchmarkResult result =
            await service.RunAsync(
                automation.FindWindow()!.Value,
                LoadPack(),
                DetectionBenchmarkMode.StoryRaidMatch,
                samples: 3);

        Assert.Equal(3, result.Samples);
        Assert.Equal("Victory", result.LastModeState);
        Assert.Equal("None", result.LastRecoveryState);
        Assert.True(
            result.Capture.AverageMilliseconds >= 0);
        Assert.True(
            result.ModeDetection.AverageMilliseconds >= 0);
        Assert.True(
            result.RootRecovery.AverageMilliseconds >= 0);
        Assert.True(result.WorkChecksPerSecond > 0);
        Assert.True(
            result.ProductionChecksPerSecond > 0);
        Assert.True(
            result.ProductionChecksPerSecond <
            result.WorkChecksPerSecond);
    }

    private static void AssertMatchEquivalence(
        string directory,
        Action<ImageFrame> assert)
    {
        foreach (string file in Directory.EnumerateFiles(
                     directory,
                     "*.png",
                     SearchOption.AllDirectories))
        {
            ImageFrame frame = ImageCodec.Load(file);
            if (frame.Width == 808 &&
                frame.Height == 611)
            {
                assert(frame);
            }
        }
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
