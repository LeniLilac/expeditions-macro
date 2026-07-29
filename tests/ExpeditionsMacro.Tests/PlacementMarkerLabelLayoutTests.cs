using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementMarkerLabelLayoutTests
{
    [Fact]
    public void DenseCluster_FansLabelsIntoNonOverlappingLanes()
    {
        PlacementMarkerLabelRequest[] requests =
        [
            new(0, 398, 350, 48, 24),
            new(1, 410, 362, 48, 24),
            new(2, 422, 374, 48, 24),
            new(3, 398, 386, 48, 24),
            new(4, 410, 398, 48, 24),
            new(5, 422, 410, 48, 24),
        ];

        IReadOnlyList<PlacementMarkerLabelPlacement>
            placements =
                PlacementMarkerLabelLayout.Arrange(
                    requests);

        Assert.Equal(requests.Length, placements.Count);
        Assert.All(
            placements,
            placement => Assert.True(
                placement.LabelBounds.FitsWithin(
                    808,
                    611)));
        AssertPairwiseSeparated(placements);
        Assert.DoesNotContain(
            placements,
            placement => requests.Any(
                request => Intersects(
                    placement.LabelBounds,
                    PointExclusion(request))));
        Assert.True(
            placements.Select(placement =>
                    placement.LabelBounds.X)
                .Distinct()
                .Count() >= 3);
    }

    [Fact]
    public void EdgeAnchors_KeepVariableWidthLabelsInsideMap()
    {
        PlacementMarkerLabelRequest[] requests =
        [
            new(0, 0, 0, 48, 24),
            new(1, 807, 0, 64, 24),
            new(2, 0, 610, 56, 24),
            new(3, 807, 610, 72, 24),
        ];

        IReadOnlyList<PlacementMarkerLabelPlacement>
            placements =
                PlacementMarkerLabelLayout.Arrange(
                    requests);

        Assert.All(
            placements,
            placement =>
            {
                Assert.True(
                    placement.LabelBounds.FitsWithin(
                        808,
                        611));
                Assert.InRange(
                    placement.LabelBounds.X,
                    4,
                    804 -
                    placement.LabelBounds.Width);
                Assert.InRange(
                    placement.LabelBounds.Y,
                    4,
                    607 -
                    placement.LabelBounds.Height);
            });
        AssertPairwiseSeparated(placements);
    }

    [Fact]
    public void InputOrder_DoesNotMoveUniqueAnchors()
    {
        PlacementMarkerLabelRequest[] requests =
        [
            new(4, 120, 170, 48, 24),
            new(7, 132, 182, 56, 24),
            new(9, 144, 194, 64, 24),
        ];

        IReadOnlyDictionary<int, ScreenRegion> first =
            PlacementMarkerLabelLayout
                .Arrange(requests)
                .ToDictionary(
                    placement => placement.Key,
                    placement =>
                        placement.LabelBounds);
        IReadOnlyDictionary<int, ScreenRegion> second =
            PlacementMarkerLabelLayout
                .Arrange(requests.Reverse().ToArray())
                .ToDictionary(
                    placement => placement.Key,
                    placement =>
                        placement.LabelBounds);

        Assert.Equal(first, second);
    }

    private static void AssertPairwiseSeparated(
        IReadOnlyList<PlacementMarkerLabelPlacement>
            placements)
    {
        for (int first = 0;
             first < placements.Count;
             first++)
        {
            for (int second = first + 1;
                 second < placements.Count;
                 second++)
            {
                Assert.False(
                    Intersects(
                        Expand(
                            placements[first]
                                .LabelBounds,
                            4),
                        placements[second]
                            .LabelBounds));
            }
        }
    }

    private static ScreenRegion PointExclusion(
        PlacementMarkerLabelRequest request) =>
        new(
            request.AnchorX - 11,
            request.AnchorY - 11,
            23,
            23);

    private static ScreenRegion Expand(
        ScreenRegion region,
        int amount) =>
        new(
            region.X - amount,
            region.Y - amount,
            region.Width + amount * 2,
            region.Height + amount * 2);

    private static bool Intersects(
        ScreenRegion first,
        ScreenRegion second) =>
        first.X < second.Right &&
        first.Right > second.X &&
        first.Y < second.Bottom &&
        first.Bottom > second.Y;
}
