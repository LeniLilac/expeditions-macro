using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PresetDeletionTests
{
    [Fact]
    public async Task DeleteAsync_RemovesEveryUnreferencedPresetType()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            PresetRepository expeditions = new(paths);
            ChallengePresetRepository challenges = new(paths);
            StoryPresetRepository stories = new(paths);
            RaidPresetRepository raids = new(paths);
            MacroPlanRepository plans = new(paths);
            PresetDeletionService deletion = new(expeditions, challenges, stories, raids, plans);

            await expeditions.SaveAsync(Expedition());
            await challenges.SaveAsync(Challenge());
            await stories.SaveAsync(Story());
            await raids.SaveAsync(Raid());

            await deletion.DeleteAsync(MacroTaskKind.Expedition, "expedition-route");
            await deletion.DeleteAsync(MacroTaskKind.Challenge, "challenge-route");
            await deletion.DeleteAsync(MacroTaskKind.Story, "story-route");
            await deletion.DeleteAsync(MacroTaskKind.Raid, "raid-route");

            Assert.Null(await expeditions.LoadAsync("expedition-route"));
            Assert.Null(await challenges.LoadAsync("challenge-route"));
            Assert.Null(await stories.LoadAsync("story-route"));
            Assert.Null(await raids.LoadAsync("raid-route"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task DeleteAsync_BlocksPresetUsedByMacroPlan()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            StoryPresetRepository stories = new(paths);
            MacroPlanRepository plans = new(paths);
            PresetDeletionService deletion = CreateDeletionService(paths);
            await stories.SaveAsync(Story());
            await plans.SaveAsync(new MacroPlan
            {
                Id = "daily-plan",
                Name = "Daily rotation",
                Tasks =
                [
                    new MacroTaskDefinition
                    {
                        Id = "story-task",
                        Kind = MacroTaskKind.Story,
                        PresetId = "story-route",
                    },
                ],
            });

            PresetInUseException error = await Assert.ThrowsAsync<PresetInUseException>(
                () => deletion.DeleteAsync(MacroTaskKind.Story, "story-route"));

            Assert.Contains("Macro plan 'Daily rotation'", error.Message, StringComparison.Ordinal);
            Assert.NotNull(await stories.LoadAsync("story-route"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PresetDeletionService CreateDeletionService(AppPaths paths) => new(
        new PresetRepository(paths),
        new ChallengePresetRepository(paths),
        new StoryPresetRepository(paths),
        new RaidPresetRepository(paths),
        new MacroPlanRepository(paths));

    private static ExpeditionPreset Expedition() => new()
    {
        Id = "expedition-route",
        Name = "Expedition route",
        CameraModelId = "camera-model",
        PlacementModelId = "placement-model",
    };

    private static ChallengePreset Challenge() => new()
    {
        Id = "challenge-route",
        Name = "Challenge rotation",
        Maps = ChallengePreset.EmptyMapProfiles(),
    };

    private static StoryPreset Story() => new()
    {
        Id = "story-route",
        Name = "Story route",
    };

    private static RaidPreset Raid() => new()
    {
        Id = "raid-route",
        Name = "Raid route",
    };
}
