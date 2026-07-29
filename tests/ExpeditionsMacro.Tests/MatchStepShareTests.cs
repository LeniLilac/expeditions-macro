using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class MatchStepShareTests
{
    [Fact]
    public void ShareCode_RoundTripsMatchActionsAndAdvancedSettings()
    {
        PlacementTarget target = new()
        {
            Mode = PlacementTargetMode.Expedition,
            MapNumber = 2,
        };
        PlacementSetupRoute route =
            PlacementSetupCatalog.For(target);
        PlacementModel setup = new()
        {
            Id = route.ModelId,
            Name = route.Name,
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = target,
            TeamSlot = 3,
            AdvancedSettings = new PlacementAdvancedSettings
            {
                Enabled = true,
                UnitSelectionDelayMilliseconds = 180,
                PlacementBurstDurationMilliseconds = 35,
                BeforeSelectionClickMilliseconds = 120,
                BeforeSelectedUnitProofMilliseconds = 250,
                ActionKeyIntervalMilliseconds = 80,
                VerifySelectedUnitPanelBeforeActions = false,
            },
            Steps =
            [
                new PlacementStep
                {
                    Kind = MatchStepKind.Placement,
                    PlacementId = "unit-1a",
                    UnitKey = 1,
                    X = 390,
                    Y = 352,
                    DelayAfterMilliseconds = 900,
                    Phase = PlacementPhase.BeforeStart,
                },
                new PlacementStep
                {
                    Kind = MatchStepKind.ReconfigureUnit,
                    TargetPlacementId = "unit-1a",
                    UnitKey = 1,
                    X = 0,
                    Y = 0,
                    DelayAfterMilliseconds = 0,
                    Phase = PlacementPhase.BeforeStart,
                    ChangeTargetingPriority = true,
                    TargetingPriority =
                        UnitTargetingPriority.Strongest,
                    AutoUpgradeAction =
                        MatchAutoUpgradeAction.Disable,
                },
                new PlacementStep
                {
                    Kind = MatchStepKind.Delay,
                    UnitKey = 0,
                    X = 0,
                    Y = 0,
                    DelayAfterMilliseconds = 0,
                    Phase = PlacementPhase.BeforeStart,
                    DelayDurationMilliseconds = 1250,
                },
                new PlacementStep
                {
                    Kind = MatchStepKind.UpgradeUnit,
                    TargetPlacementId = "unit-1a",
                    UnitKey = 1,
                    X = 0,
                    Y = 0,
                    DelayAfterMilliseconds = 0,
                    Phase = PlacementPhase.BeforeStart,
                    UpgradeCount = 4,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };
        FastNoAlignShareBundle bundle = new()
        {
            Plan = new MacroPlan
            {
                Id = "match-step-plan",
                Name = "Match step plan",
                Tasks =
                [
                    new MacroTaskDefinition
                    {
                        Id = "expedition-task",
                        Kind =
                            MacroTaskKind.Expedition,
                        Name = "Flower Forest",
                        PlacementTarget = target,
                    },
                ],
            },
            PlacementSetups = [setup],
        };

        FastNoAlignShareBundle decoded =
            FastNoAlignShareCodec.Decode(
                FastNoAlignShareCodec.Encode(bundle));
        PlacementModel shared =
            Assert.Single(decoded.PlacementSetups);

        Assert.True(shared.AdvancedSettings.Enabled);
        Assert.False(
            shared.AdvancedSettings
                .VerifySelectedUnitPanelBeforeActions);
        Assert.Equal(
            [
                MatchStepKind.Placement,
                MatchStepKind.ReconfigureUnit,
                MatchStepKind.Delay,
                MatchStepKind.UpgradeUnit,
            ],
            shared.Steps.Select(step => step.Kind));
        Assert.Equal(
            MatchAutoUpgradeAction.Disable,
            shared.Steps[1].AutoUpgradeAction);
        Assert.Equal(
            1250,
            shared.Steps[2].DelayDurationMilliseconds);
        Assert.Equal(4, shared.Steps[3].UpgradeCount);
        Assert.Equal(
            "unit-1a",
            shared.Steps[1].TargetPlacementId);
    }
}
