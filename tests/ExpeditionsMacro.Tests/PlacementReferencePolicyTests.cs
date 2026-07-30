using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementReferencePolicyTests
{
    [Fact]
    public void DisplayLabels_AddLettersOnlyWhenAUnitRepeats()
    {
        PlacementStep[] steps =
        [
            Placement("one-a", 1, 100),
            Placement("six-a", 6, 200),
            Placement("six-b", 6, 300),
            Placement("six-c", 6, 400),
        ];

        IReadOnlyDictionary<string, string> labels =
            PlacementReferencePolicy
                .BuildDisplayLabels(steps);

        Assert.Equal("1", labels["one-a"]);
        Assert.Equal("6a", labels["six-a"]);
        Assert.Equal("6b", labels["six-b"]);
        Assert.Equal("6c", labels["six-c"]);
    }

    [Fact]
    public void StableReference_FollowsAPlacementAfterItsCoordinateChanges()
    {
        PlacementStep placement =
            Placement("six-a", 6, 240);
        PlacementStep action =
            Reconfigure("six-a", 6);
        PlacementStep moved = placement with
        {
            X = 520,
            Y = 410,
        };

        PlacementStep resolved =
            PlacementReferencePolicy.ResolveTarget(
                [moved, action],
                action);

        Assert.Equal(520, resolved.X);
        Assert.Equal(410, resolved.Y);
    }

    [Fact]
    public void LegacyCoordinateAction_MigratesToTheEarlierPlacement()
    {
        PlacementStep placement =
            Placement(string.Empty, 6, 240);
        PlacementStep legacy = placement with
        {
            Kind = MatchStepKind.ReconfigureUnit,
            ChangeTargetingPriority = true,
        };

        IReadOnlyList<PlacementStep> normalized =
            PlacementReferencePolicy.Normalize(
                [placement, legacy]);

        Assert.Equal(
            normalized[0].PlacementId,
            normalized[1].TargetPlacementId);
        Assert.Equal(0, normalized[1].X);
        Assert.Equal(0, normalized[1].Y);
    }

    [Fact]
    public void UnitAction_CannotAppearBeforeItsPlacement()
    {
        PlacementStep action =
            Reconfigure("six-a", 6);
        PlacementStep placement =
            Placement("six-a", 6, 240);

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () =>
                    PlacementReferencePolicy.Validate(
                        [action, placement]));

        Assert.Contains(
            "appears earlier",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RemovingPlacement_RemovesOnlyItsDependentActions()
    {
        PlacementStep first =
            Placement("one-a", 1, 240);
        PlacementStep second =
            Placement("two-a", 2, 360);
        PlacementStep reconfigure =
            Reconfigure("one-a", 1);
        PlacementStep upgrade = new()
        {
            Kind = MatchStepKind.UpgradeUnit,
            TargetPlacementId = "one-a",
            UnitKey = 1,
            X = 0,
            Y = 0,
            UpgradeCount = 2,
            DelayAfterMilliseconds = 0,
        };
        PlacementStep sell = new()
        {
            Kind = MatchStepKind.SellUnit,
            TargetPlacementId = "one-a",
            UnitKey = 1,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
        };
        PlacementStep unrelated =
            Reconfigure("two-a", 2);

        IReadOnlyList<PlacementStep> remaining =
            PlacementReferencePolicy
                .RemovePlacementAndReferences(
                    [
                        first,
                        second,
                        reconfigure,
                        upgrade,
                        sell,
                        unrelated,
                    ],
                    first.PlacementId);

        Assert.Equal(
            [second, unrelated],
            remaining);
        PlacementReferencePolicy.Validate(remaining);
    }

    [Fact]
    public void SoldPlacement_CannotReceiveAnotherUnitAction()
    {
        PlacementStep placement =
            Placement("one-a", 1, 240);
        PlacementStep sell = new()
        {
            Kind = MatchStepKind.SellUnit,
            TargetPlacementId = placement.PlacementId,
            UnitKey = placement.UnitKey,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
        };
        PlacementStep later =
            Reconfigure(
                placement.PlacementId,
                placement.UnitKey);

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => PlacementReferencePolicy.Validate(
                    [placement, sell, later]));

        Assert.Contains(
            "after its Sell Unit step",
            error.Message,
            StringComparison.Ordinal);
    }

    private static PlacementStep Placement(
        string id,
        int unit,
        int x) =>
        new()
        {
            Kind = MatchStepKind.Placement,
            PlacementId = id,
            UnitKey = unit,
            X = x,
            Y = 280,
            DelayAfterMilliseconds = 0,
        };

    private static PlacementStep Reconfigure(
        string targetId,
        int unit) =>
        new()
        {
            Kind = MatchStepKind.ReconfigureUnit,
            TargetPlacementId = targetId,
            UnitKey = unit,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            ChangeTargetingPriority = true,
        };
}
