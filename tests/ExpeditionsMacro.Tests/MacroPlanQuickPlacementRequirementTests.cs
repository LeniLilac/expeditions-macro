using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class MacroPlanQuickPlacementRequirementTests
{
    [Fact]
    public async Task PlanStart_DirectStepModeRowsRequireQuickPlacementKey()
    {
        MacroTaskDefinition direct = DirectTask("direct");
        MacroTaskDefinition legacy = LegacyTask("legacy");
        List<MacroTaskDefinition> resolved = [];

        bool required = await PlacementControlRequirements
            .PlanRequiresQuickPlacementKeyAsync(
                Plan(direct, legacy),
                (task, _) =>
                {
                    resolved.Add(task);
                    return Task.FromResult<
                        IReadOnlyList<PlacementModel>>(
                        task.UsesPlacementSetup
                            ? [Placement([Step()])]
                            : [Placement(
                                [Step()],
                                "recording-1")]);
                },
                CancellationToken.None);

        Assert.True(required);
        Assert.Equal([direct], resolved);
    }

    [Fact]
    public async Task PlanStart_LegacyStepModeRowsRequireQuickPlacementKey()
    {
        MacroTaskDefinition direct = DirectTask("direct");
        MacroTaskDefinition legacy = LegacyTask("legacy");
        List<MacroTaskDefinition> resolved = [];

        bool required = await PlacementControlRequirements
            .PlanRequiresQuickPlacementKeyAsync(
                Plan(direct, legacy),
                (task, _) =>
                {
                    resolved.Add(task);
                    return Task.FromResult<
                        IReadOnlyList<PlacementModel>>(
                        task.UsesPlacementSetup
                            ? [Placement(
                                [Step()],
                                "recording-1")]
                            : [Placement([Step()])]);
                },
                CancellationToken.None);

        Assert.True(required);
        Assert.Equal([direct, legacy], resolved);
    }

    [Fact]
    public async Task PlanStart_RecordingAndEmptyStepModesRemainExempt()
    {
        MacroTaskDefinition direct = DirectTask("direct");
        MacroTaskDefinition legacy = LegacyTask("legacy");
        List<MacroTaskDefinition> resolved = [];

        bool required = await PlacementControlRequirements
            .PlanRequiresQuickPlacementKeyAsync(
                Plan(direct, legacy),
                (task, _) =>
                {
                    resolved.Add(task);
                    return Task.FromResult<
                        IReadOnlyList<PlacementModel>>(
                        task.UsesPlacementSetup
                            ? [Placement(
                                [Step()],
                                "recording-1")]
                            : [Placement([])]);
                },
                CancellationToken.None);

        Assert.False(required);
        Assert.Equal([direct, legacy], resolved);
    }

    [Fact]
    public async Task PlanStart_AnyResolvedChallengeMapCanRequireTheKey()
    {
        MacroTaskDefinition directChallenge = new()
        {
            Id = "challenge",
            Kind = MacroTaskKind.Challenge,
        };

        bool required = await PlacementControlRequirements
            .PlanRequiresQuickPlacementKeyAsync(
                Plan(directChallenge),
                (task, _) =>
                {
                    Assert.Same(directChallenge, task);
                    return Task.FromResult<
                        IReadOnlyList<PlacementModel>>(
                        [
                            Placement(
                                [Step()],
                                "recording-1"),
                            Placement([]),
                            Placement([Step()]),
                        ]);
                },
                CancellationToken.None);

        Assert.True(required);
    }

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) =>
        new()
        {
            Id = "plan",
            Name = "Plan",
            Tasks = tasks,
        };

    private static MacroTaskDefinition DirectTask(
        string id) =>
        new()
        {
            Id = id,
            Kind = MacroTaskKind.Expedition,
            PlacementTarget = new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 1,
            },
        };

    private static MacroTaskDefinition LegacyTask(
        string id) =>
        new()
        {
            Id = id,
            Kind = MacroTaskKind.Expedition,
            PresetId = $"{id}-preset",
        };

    private static PlacementModel Placement(
        IReadOnlyList<PlacementStep> steps,
        string? recordingId = null) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Placement",
            ClientWidth = RobloxClientProfile.Width,
            ClientHeight = RobloxClientProfile.Height,
            Steps = steps,
            ManualInputRecordingId = recordingId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PlacementStep Step() =>
        new()
        {
            UnitKey = 1,
            X = 100,
            Y = 100,
            DelayAfterMilliseconds = 0,
        };
}
