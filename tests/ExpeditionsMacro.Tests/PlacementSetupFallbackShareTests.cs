using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementSetupFallbackShareTests
{
    [Fact]
    public async Task Export_IgnoresStartOnlyExactOverrideAndUsesCategory()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementTarget exact = new()
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = 3,
                StoryRunKind = StoryRunKind.Infinite,
                ActNumber = 1,
            };
            PlacementTarget category = exact with
            {
                StoryRunKind = StoryRunKind.Act,
                ActNumber = PlacementSetupCatalog
                    .SharedStoryActNumber,
            };
            AppPaths paths = new(root);
            PlacementModelRepository repository =
                new(paths);
            await repository.SaveAsync(
                Setup(exact, team: 2, startOnly: true));
            PlacementModel shared =
                Setup(category, team: 7, startOnly: false);
            await repository.SaveAsync(shared);

            FastNoAlignShareService service = new(
                new MacroPlanRepository(paths),
                repository,
                new PresetRepository(paths),
                new ChallengePresetRepository(paths),
                new StoryPresetRepository(paths),
                new RaidPresetRepository(paths));
            MacroPlan plan = new()
            {
                Id = "fallback-plan",
                Name = "Fallback plan",
                Tasks =
                [
                    new MacroTaskDefinition
                    {
                        Id = "story-task",
                        Kind = MacroTaskKind.Story,
                        Name = "Rose Kingdom Infinite",
                        PlacementTarget = exact,
                        TargetRuntimeMinutes = 60,
                        CompleteOnRuntimeDefeat = true,
                    },
                ],
            };

            FastNoAlignShareBundle bundle =
                service.Read(
                    await service.ExportAsync(plan));

            PlacementModel exported =
                Assert.Single(bundle.PlacementSetups);
            Assert.Equal(shared.Id, exported.Id);
            Assert.Equal(7, exported.TeamSlot);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static PlacementModel Setup(
        PlacementTarget target,
        int team,
        bool startOnly) =>
        new()
        {
            Id = PlacementSetupCatalog.IdFor(target),
            Name = PlacementSetupCatalog.NameFor(target),
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = target,
            TeamSlot = team,
            Steps = startOnly
                ?
                [
                    PlacementTimelinePolicy
                        .CreateStartGameStep(),
                ]
                :
                [
                    new PlacementStep
                    {
                        UnitKey = 1,
                        X = 400,
                        Y = 300,
                        DelayAfterMilliseconds = 900,
                    },
                ],
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        };
}
