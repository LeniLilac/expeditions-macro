using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed partial class PlacementPlaybackAttemptTests
{
    [Fact]
    public async Task ReconfigureStep_ReopensUnitAndDisablesAutoUpgrade()
    {
        AttemptAutomation automation = new();
        PlacementStep placement = Step(3, 320) with
        {
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Priority2,
        };
        PlacementStep reconfigure = placement with
        {
            Kind = MatchStepKind.ReconfigureUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.Last,
            AutoUpgradeAction =
                MatchAutoUpgradeAction.Disable,
        };

        await PlayStepsAsync(
            automation,
            [placement, reconfigure]);

        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action == "verify:320,280"));
        Assert.Equal(
            1,
            automation.Actions.Count(action =>
                action == "letter:T"));
        Assert.Equal(
            7,
            automation.Actions.Count(action =>
                action == "letter:Y"));
        Assert.DoesNotContain(
            automation.Actions,
            action => action ==
                $"held:{(int)'Y'}:down" ||
                action ==
                $"held:{(int)'Y'}:up");
    }

    [Fact]
    public async Task ReconfigureAutoUpgrade_UsesRememberedForwardCycle()
    {
        AttemptAutomation automation = new();
        PlacementStep placement = Step(1, 320);
        PlacementStep priority3 =
            ReconfigureAutoUpgrade(
                placement,
                MatchAutoUpgradeAction.Priority3);
        PlacementStep priority5 =
            ReconfigureAutoUpgrade(
                placement,
                MatchAutoUpgradeAction.Priority5);
        PlacementStep off =
            ReconfigureAutoUpgrade(
                placement,
                MatchAutoUpgradeAction.Disable);

        await PlayStepsAsync(
            automation,
            [placement, priority3, priority5, off]);

        int[] autoUpgradeTaps =
            AutoUpgradeTapCountsAfterEachSelection(
                automation.Actions);
        Assert.Equal([0, 3, 2, 2], autoUpgradeTaps);
        Assert.DoesNotContain(
            automation.Actions,
            action => action ==
                $"held:{(int)'Y'}:down" ||
                action ==
                $"held:{(int)'Y'}:up");
    }

    [Fact]
    public async Task UpgradeStep_PressesUpgradeConfiguredNumberOfTimes()
    {
        AttemptAutomation automation = new()
        {
            UseAffordableUpgradeFrame = true,
        };
        PlacementStep placement = Step(2, 340);
        PlacementStep upgrade = placement with
        {
            Kind = MatchStepKind.UpgradeUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            UpgradeCount = 3,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
        };

        await PlayStepsAsync(
            automation,
            [placement, upgrade]);

        Assert.Equal(
            3,
            automation.Actions.Count(action =>
                action == "letter:U"));
        Assert.Equal(
            2,
            automation.Actions.Count(action =>
                action == "verify:340,280"));
    }

    [Fact]
    public async Task SellStep_VerifiesTargetAndPressesConfiguredKeyOnce()
    {
        AttemptAutomation automation = new();
        PlacementStep placement = Step(2, 340);
        PlacementStep sell = placement with
        {
            Kind = MatchStepKind.SellUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
        };
        PlacementAdvancedSettings advanced = new()
        {
            Enabled = true,
            VerifySelectedUnitPanelBeforeActions = false,
        };

        await PlayStepsAsync(
            automation,
            [placement, sell],
            advancedSettings: advanced);

        Assert.Contains(
            "verify:340,280",
            automation.Actions);
        Assert.Equal(
            1,
            automation.Actions.Count(action =>
                action == "letter:X"));
    }

    [Fact]
    public async Task SellStep_RequiresSellUnitBinding()
    {
        AttemptAutomation automation = new();
        PlacementStep placement = Step(2, 340);
        PlacementStep sell = placement with
        {
            Kind = MatchStepKind.SellUnit,
            PlacementId = string.Empty,
            TargetPlacementId =
                placement.PlacementId,
            X = 0,
            Y = 0,
            AutoUpgradePriority =
                UnitAutoUpgradePriority.Off,
        };

        InvalidDataException error =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => PlayStepsAsync(
                    automation,
                    [placement, sell],
                    sellKey: null));

        Assert.Contains(
            "set Sell Unit key",
            error.Message,
            StringComparison.Ordinal);
    }
}
