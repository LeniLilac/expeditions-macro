using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class StagePresetTests
{
    [Fact]
    public void StoryDraft_CanBeSavedBeforeModelsAreChosen()
    {
        StoryPreset preset = Story();

        preset.Validate();
        Assert.Throws<InvalidDataException>(() => preset.Validate(requireModels: true));
    }

    [Fact]
    public void RaidDraft_CanBeSavedBeforeModelsAreChosen()
    {
        RaidPreset preset = Raid();

        preset.Validate();
        Assert.Throws<InvalidDataException>(() => preset.Validate(requireModels: true));
    }

    [Fact]
    public async Task Repositories_RoundTripStoryAndRaidPresets()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            StoryPresetRepository stories = new(paths);
            RaidPresetRepository raids = new(paths);
            StoryPreset story = Story() with { TeamSlot = 2 };
            RaidPreset raid = Raid() with { Act = RaidAct.Act3, TeamSlot = 4 };

            await stories.SaveAsync(story);
            await raids.SaveAsync(raid);

            StoryPreset loadedStory = Assert.IsType<StoryPreset>(await stories.LoadAsync(story.Id));
            RaidPreset loadedRaid = Assert.IsType<RaidPreset>(await raids.LoadAsync(raid.Id));
            Assert.Equal(2, loadedStory.TeamSlot);
            Assert.Equal(RaidAct.Act3, loadedRaid.Act);
            Assert.Equal(4, loadedRaid.TeamSlot);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task MacroPlanRepository_RoundTripsValidatedProgress()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            MacroPlanRepository repository = new(new AppPaths(root));
            MacroPlan plan = new()
            {
                Id = "daily-plan",
                Name = "Daily plan",
                Tasks =
                [
                    new MacroTaskDefinition
                    {
                        Id = "story-task",
                        Kind = MacroTaskKind.Story,
                        PresetId = "story-route",
                    },
                ],
                Progress = [new MacroTaskProgress { TaskId = "story-task", Victories = 2 }],
            };

            await repository.SaveAsync(plan);

            MacroPlan loaded = Assert.IsType<MacroPlan>(await repository.LoadAsync(plan.Id));
            Assert.Equal(2, loaded.ProgressFor("story-task").Victories);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void MacroPlan_RejectsUnknownOrNegativeProgress()
    {
        MacroTaskDefinition task = new()
        {
            Id = "story-task",
            Kind = MacroTaskKind.Story,
            PresetId = "story-route",
        };
        MacroPlan unknown = new()
        {
            Id = "plan",
            Name = "Plan",
            Tasks = [task],
            Progress = [new MacroTaskProgress { TaskId = "missing" }],
        };
        MacroPlan negative = unknown with
        {
            Progress = [new MacroTaskProgress { TaskId = task.Id, Victories = -1 }],
        };

        Assert.Throws<InvalidDataException>(unknown.Validate);
        Assert.Throws<InvalidDataException>(negative.Validate);
    }

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
