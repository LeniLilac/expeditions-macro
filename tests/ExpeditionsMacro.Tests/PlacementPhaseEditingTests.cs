using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementPhaseEditingTests
{
    [Fact]
    public void BeforeToAfterMovesToPhaseBoundaryAndPreservesStep()
    {
        PlacementStep changed = Step(
            PlacementPhase.BeforeStart,
            unit: 1,
            x: 120);
        PlacementStep before = Step(
            PlacementPhase.BeforeStart,
            unit: 2,
            x: 220);
        PlacementStep afterFirst = Step(
            PlacementPhase.AfterStart,
            unit: 3,
            x: 320);
        PlacementStep afterSecond = Step(
            PlacementPhase.AfterStart,
            unit: 4,
            x: 420);

        PlacementPhaseChange result =
            PlacementAuthoringRules.ChangePhaseForAuthoring(
                [changed, before, afterFirst, afterSecond],
                sourceIndex: 0,
                PlacementPhase.AfterStart);

        Assert.True(result.Changed);
        Assert.Equal(1, result.ChangedIndex);
        Assert.Equal(
            [
                before,
                changed with
                {
                    Phase = PlacementPhase.AfterStart,
                },
                afterFirst,
                afterSecond,
            ],
            result.Steps);
    }

    [Fact]
    public void AfterToBeforeMovesToPhaseBoundaryAndPreservesStep()
    {
        PlacementStep before = Step(
            PlacementPhase.BeforeStart,
            unit: 1,
            x: 120);
        PlacementStep beforeSecond = Step(
            PlacementPhase.BeforeStart,
            unit: 3,
            x: 320);
        PlacementStep after = Step(
            PlacementPhase.AfterStart,
            unit: 2,
            x: 220);
        PlacementStep changed = Step(
            PlacementPhase.AfterStart,
            unit: 6,
            x: 420) with
        {
            Y = 456,
            DelayAfterMilliseconds = 1_234,
            DelayAfterStartMilliseconds = 9_876,
            TargetingPriority =
                UnitTargetingPriority.Last,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Priority5,
        };

        PlacementPhaseChange result =
            PlacementAuthoringRules.ChangePhaseForAuthoring(
                [before, beforeSecond, after, changed],
                sourceIndex: 3,
                PlacementPhase.BeforeStart);

        PlacementStep expected =
            changed with
            {
                Phase = PlacementPhase.BeforeStart,
            };
        Assert.True(result.Changed);
        Assert.Equal(2, result.ChangedIndex);
        Assert.Equal(
            [before, beforeSecond, expected, after],
            result.Steps);
        Assert.Equal(changed.X, expected.X);
        Assert.Equal(changed.Y, expected.Y);
        Assert.Equal(
            changed.DelayAfterMilliseconds,
            expected.DelayAfterMilliseconds);
        Assert.Equal(
            changed.DelayAfterStartMilliseconds,
            expected.DelayAfterStartMilliseconds);
        Assert.Equal(
            changed.TargetingPriority,
            expected.TargetingPriority);
        Assert.Equal(
            changed.AutoUpgradePriority,
            expected.AutoUpgradePriority);
    }

    [Fact]
    public void SamePhaseIsANoOp()
    {
        PlacementStep before = Step(
            PlacementPhase.BeforeStart,
            unit: 1,
            x: 120);
        PlacementStep after = Step(
            PlacementPhase.AfterStart,
            unit: 2,
            x: 220);

        PlacementPhaseChange result =
            PlacementAuthoringRules.ChangePhaseForAuthoring(
                [before, after],
                sourceIndex: 0,
                PlacementPhase.BeforeStart);

        Assert.False(result.Changed);
        Assert.Equal(0, result.ChangedIndex);
        Assert.Same(before, result.Steps[0]);
        Assert.Same(after, result.Steps[1]);
    }

    [Fact]
    public void InvalidPhaseOrIndexStopsSafely()
    {
        PlacementStep step = Step(
            PlacementPhase.BeforeStart,
            unit: 1,
            x: 120);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                PlacementAuthoringRules
                    .ChangePhaseForAuthoring(
                        [step],
                        sourceIndex: -1,
                        PlacementPhase.AfterStart));
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                PlacementAuthoringRules
                    .ChangePhaseForAuthoring(
                        [step],
                        sourceIndex: 0,
                        (PlacementPhase)999));
        Assert.Throws<InvalidDataException>(
            () =>
                PlacementAuthoringRules
                    .ChangePhaseForAuthoring(
                        [step with
                            {
                                Phase =
                                    (PlacementPhase)999,
                            }],
                        sourceIndex: 0,
                        PlacementPhase.AfterStart));
    }

    [Fact]
    public async Task PhaseChangeIsPersistedByPlacementAutosave()
    {
        PlacementStep before = Step(
            PlacementPhase.BeforeStart,
            unit: 1,
            x: 120);
        PlacementStep changed = Step(
            PlacementPhase.AfterStart,
            unit: 2,
            x: 220);
        PlacementPhaseChange result =
            PlacementAuthoringRules.ChangePhaseForAuthoring(
                [before, changed],
                sourceIndex: 1,
                PlacementPhase.BeforeStart);
        PlacementModel? saved = null;
        PlacementModelAutoSaveSession session =
            new(
                (model, _) =>
                {
                    saved = model;
                    return Task.CompletedTask;
                },
                (_, _) => Task.CompletedTask,
                TimeSpan.FromMinutes(1));
        PlacementModel model = new()
        {
            Id = "placement-phase-autosave",
            Name = "Phase autosave",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber =
                    PlacementSetupCatalog
                        .SharedExpeditionMapNumber,
                ActNumber = 0,
            },
            Steps = result.Steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        session.ScheduleSave(model);

        Assert.True(await session.FlushAsync());
        Assert.NotNull(saved);
        Assert.All(
            saved.Steps,
            step => Assert.Equal(
                PlacementPhase.BeforeStart,
                step.Phase));
    }

    private static PlacementStep Step(
        PlacementPhase phase,
        int unit,
        int x) =>
        new()
        {
            UnitKey = unit,
            X = x,
            Y = 300,
            DelayAfterMilliseconds = 900,
            Phase = phase,
        };
}
