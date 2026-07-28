using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class StoryHardModeNormalizationTests
{
    [Fact]
    public async Task MacroPlanRepository_LoadListAndSaveNormalizeOnlyNonActStoryTasks()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            MacroPlan legacy = Plan(
                DirectStoryTask(
                    "mastery",
                    StoryRunKind.Mastery,
                    hardMode: true),
                DirectStoryTask(
                    "act",
                    StoryRunKind.Act,
                    hardMode: true));
            string path = Path.Combine(
                paths.MacroPlans,
                $"{legacy.Id}.json");
            await JsonFileStore.WriteAtomicAsync(
                path,
                legacy);

            MacroPlanRepository repository =
                new(paths);
            MacroPlan loaded =
                Assert.IsType<MacroPlan>(
                    await repository.LoadAsync(
                        legacy.Id));
            MacroPlan listed =
                Assert.Single(
                    await repository.ListAsync());

            Assert.False(
                loaded.Tasks[0].HardMode);
            Assert.True(
                loaded.Tasks[1].HardMode);
            Assert.False(
                listed.Tasks[0].HardMode);
            Assert.True(
                listed.Tasks[1].HardMode);

            await repository.SaveAsync(legacy);
            MacroPlan persisted =
                Assert.IsType<MacroPlan>(
                    await JsonFileStore
                        .ReadAsync<MacroPlan>(
                            path));
            Assert.False(
                persisted.Tasks[0].HardMode);
            Assert.True(
                persisted.Tasks[1].HardMode);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task StoryPresetRepository_LoadListAndSaveNormalizeOnlyNonActPresets()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            StoryPreset legacyInfinite =
                StoryPresetFor(
                    "legacy-infinite",
                    StoryRunKind.Infinite,
                    hardMode: true);
            StoryPreset legacyAct =
                StoryPresetFor(
                    "legacy-act",
                    StoryRunKind.Act,
                    hardMode: true);
            await JsonFileStore.WriteAtomicAsync(
                Path.Combine(
                    paths.StoryPresets,
                    $"{legacyInfinite.Id}.json"),
                legacyInfinite);
            await JsonFileStore.WriteAtomicAsync(
                Path.Combine(
                    paths.StoryPresets,
                    $"{legacyAct.Id}.json"),
                legacyAct);

            StoryPresetRepository repository =
                new(paths);
            StoryPreset loadedInfinite =
                Assert.IsType<StoryPreset>(
                    await repository.LoadAsync(
                        legacyInfinite.Id));
            StoryPreset loadedAct =
                Assert.IsType<StoryPreset>(
                    await repository.LoadAsync(
                        legacyAct.Id));
            IReadOnlyList<StoryPreset> listed =
                await repository.ListAsync();

            Assert.False(
                loadedInfinite.HardMode);
            Assert.True(loadedAct.HardMode);
            Assert.False(
                listed.Single(preset =>
                    preset.Id ==
                    legacyInfinite.Id)
                    .HardMode);
            Assert.True(
                listed.Single(preset =>
                    preset.Id == legacyAct.Id)
                    .HardMode);

            StoryPreset legacyMastery =
                StoryPresetFor(
                    "legacy-mastery",
                    StoryRunKind.Mastery,
                    hardMode: true);
            await repository.SaveAsync(
                legacyMastery);
            StoryPreset persisted =
                Assert.IsType<StoryPreset>(
                    await JsonFileStore
                        .ReadAsync<StoryPreset>(
                            Path.Combine(
                                paths.StoryPresets,
                                $"{legacyMastery.Id}.json")));
            Assert.False(persisted.HardMode);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void ShareCodec_LoadsLegacyHardFlagsAndWritesCanonicalStoryData()
    {
        PlacementTarget mastery =
            StoryTarget(
                map: 1,
                StoryRunKind.Mastery);
        PlacementTarget act =
            StoryTarget(
                map: 2,
                StoryRunKind.Act);
        PlacementTarget infinite =
            StoryTarget(
                map: 3,
                StoryRunKind.Infinite);
        string infiniteSetupId =
            PlacementSetupCatalog.IdFor(infinite);
        StoryPreset legacyPreset = new()
        {
            Id = "legacy-infinite",
            Name = "Legacy Infinite",
            Map = ChallengeMapId.RoseKingdom,
            RunKind = StoryRunKind.Infinite,
            HardMode = true,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            PrestartPlacementModelId =
                infiniteSetupId,
        };
        FastNoAlignShareBundle legacy = new()
        {
            Plan = Plan(
                DirectStoryTask(
                    "mastery",
                    StoryRunKind.Mastery,
                    hardMode: true,
                    mastery),
                DirectStoryTask(
                    "act",
                    StoryRunKind.Act,
                    hardMode: true,
                    act),
                new MacroTaskDefinition
                {
                    Id = "legacy-preset-task",
                    Kind = MacroTaskKind.Story,
                    PresetId = legacyPreset.Id,
                    Name = legacyPreset.Name,
                    HardMode = true,
                }),
            PlacementSetups =
            [
                Setup(mastery),
                Setup(act),
                Setup(infinite),
            ],
            StoryPresets = [legacyPreset],
        };

        legacy.Validate();
        FastNoAlignShareBundle decoded =
            FastNoAlignShareCodec.Decode(
                EncodeRawBundle(legacy));

        Assert.False(
            decoded.Plan.Tasks[0].HardMode);
        Assert.True(
            decoded.Plan.Tasks[1].HardMode);
        Assert.False(
            Assert.Single(
                decoded.StoryPresets)
                .HardMode);

        string normalizedCode =
            FastNoAlignShareCodec.Encode(
                legacy);
        JsonObject normalized =
            DecodeRawBundle(normalizedCode);
        JsonArray tasks =
            normalized["plan"]!["tasks"]!
                .AsArray();
        JsonArray presets =
            normalized["story_presets"]!
                .AsArray();

        Assert.False(
            tasks[0]!["hard_mode"]!
                .GetValue<bool>());
        Assert.True(
            tasks[1]!["hard_mode"]!
                .GetValue<bool>());
        Assert.False(
            presets[0]!["hard_mode"]!
                .GetValue<bool>());
    }

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) =>
        new()
        {
            Id = "story-hard-mode-plan",
            Name = "Story hard mode plan",
            Tasks = tasks,
        };

    private static MacroTaskDefinition
        DirectStoryTask(
        string id,
        StoryRunKind runKind,
        bool hardMode,
        PlacementTarget? target = null) =>
        new()
        {
            Id = id,
            Kind = MacroTaskKind.Story,
            Name = id,
            PlacementTarget =
                target ??
                StoryTarget(
                    map: 1,
                    runKind),
            HardMode = hardMode,
        };

    private static StoryPreset StoryPresetFor(
        string id,
        StoryRunKind runKind,
        bool hardMode) =>
        new()
        {
            Id = id,
            Name = id,
            RunKind = runKind,
            HardMode = hardMode,
        };

    private static PlacementTarget StoryTarget(
        int map,
        StoryRunKind runKind) =>
        new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = map,
            StoryRunKind = runKind,
            ActNumber = 1,
        };

    private static PlacementModel Setup(
        PlacementTarget target)
    {
        PlacementSetupRoute route =
            PlacementSetupCatalog.For(target);
        return new PlacementModel
        {
            Id = route.ModelId,
            Name = route.Name,
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = target,
            Steps =
            [
                new PlacementStep
                {
                    UnitKey = 1,
                    X = 400,
                    Y = 300,
                    Phase =
                        PlacementPhase.BeforeStart,
                    DelayAfterMilliseconds = 900,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string EncodeRawBundle(
        FastNoAlignShareBundle bundle)
    {
        byte[] json =
            JsonSerializer.SerializeToUtf8Bytes(
                bundle,
                JsonFileStore.Options);
        using MemoryStream compressed = new();
        using (BrotliStream brotli = new(
                   compressed,
                   CompressionLevel.SmallestSize,
                   leaveOpen: true))
        {
            brotli.Write(json);
        }
        return FastNoAlignShareCodec.Prefix +
            Convert.ToBase64String(
                compressed.ToArray());
    }

    private static JsonObject DecodeRawBundle(
        string code)
    {
        byte[] compressed =
            Convert.FromBase64String(
                code[
                    FastNoAlignShareCodec
                        .Prefix.Length..]);
        using MemoryStream source =
            new(compressed);
        using BrotliStream brotli = new(
            source,
            CompressionMode.Decompress);
        using MemoryStream json = new();
        brotli.CopyTo(json);
        return JsonNode.Parse(
                Encoding.UTF8.GetString(
                    json.ToArray()))!
            .AsObject();
    }
}
