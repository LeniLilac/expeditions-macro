namespace ExpeditionsMacro.Core.Geometry;

public readonly record struct PlacementMarkerLabelRequest
{
    public PlacementMarkerLabelRequest(
        int key,
        int anchorX,
        int anchorY,
        int labelWidth,
        int labelHeight)
    {
        if (labelWidth <= 0 || labelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(labelWidth),
                "Marker labels must have positive dimensions.");
        }

        Key = key;
        AnchorX = anchorX;
        AnchorY = anchorY;
        LabelWidth = labelWidth;
        LabelHeight = labelHeight;
    }

    public int Key { get; }

    public int AnchorX { get; }

    public int AnchorY { get; }

    public int LabelWidth { get; }

    public int LabelHeight { get; }
}

public readonly record struct PlacementMarkerLabelPlacement(
    int Key,
    ScreenRegion LabelBounds);

public static class PlacementMarkerLabelLayout
{
    private const int EdgeMargin = 4;
    private const int LabelGap = 4;
    private const int PointRadius = 7;
    private const int PreferredHorizontalLanes = 4;

    public static IReadOnlyList<PlacementMarkerLabelPlacement>
        Arrange(
            IReadOnlyCollection<PlacementMarkerLabelRequest>
                requests,
            int surfaceWidth = 808,
            int surfaceHeight = 611)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (surfaceWidth <= EdgeMargin * 2 ||
            surfaceHeight <= EdgeMargin * 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(surfaceWidth),
                "The marker surface is too small.");
        }

        PlacementMarkerLabelRequest[] items =
            requests.ToArray();
        Validate(items, surfaceWidth, surfaceHeight);
        ScreenRegion[] pointExclusions =
            items.Select(PointExclusion).ToArray();
        List<PlacementMarkerLabelPlacement> placements =
            new(items.Length);
        List<ScreenRegion> occupiedLabels =
            new(items.Length);

        foreach (PlacementMarkerLabelRequest item in
                 items.OrderBy(value => value.AnchorY)
                     .ThenBy(value => value.AnchorX)
                     .ThenBy(value => value.Key))
        {
            ScreenRegion[] candidates =
                Candidates(
                        item,
                        surfaceWidth,
                        surfaceHeight)
                    .Distinct()
                    .ToArray();
            ScreenRegion selected =
                candidates.FirstOrDefault(
                    candidate =>
                        !HasCollision(
                            candidate,
                            pointExclusions,
                            occupiedLabels));
            if (selected.Width == 0)
            {
                selected = candidates
                    .OrderBy(candidate =>
                        CollisionArea(
                            candidate,
                            pointExclusions,
                            occupiedLabels))
                    .ThenBy(candidate =>
                        DistanceFromPreferred(
                            item,
                            candidate))
                    .First();
            }

            placements.Add(
                new PlacementMarkerLabelPlacement(
                    item.Key,
                    selected));
            occupiedLabels.Add(selected);
        }

        return placements;
    }

    private static void Validate(
        IReadOnlyCollection<PlacementMarkerLabelRequest>
            requests,
        int surfaceWidth,
        int surfaceHeight)
    {
        if (requests.Select(request => request.Key)
            .Distinct()
            .Count() != requests.Count)
        {
            throw new ArgumentException(
                "Marker layout keys must be unique.",
                nameof(requests));
        }

        foreach (PlacementMarkerLabelRequest request in
                 requests)
        {
            if (request.AnchorX < 0 ||
                request.AnchorX >= surfaceWidth ||
                request.AnchorY < 0 ||
                request.AnchorY >= surfaceHeight)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    "Marker anchors must remain inside the surface.");
            }
            if (request.LabelWidth >
                    surfaceWidth - EdgeMargin * 2 ||
                request.LabelHeight >
                    surfaceHeight - EdgeMargin * 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    "Marker labels must fit inside the surface.");
            }
        }
    }

    private static IEnumerable<ScreenRegion> Candidates(
        PlacementMarkerLabelRequest request,
        int surfaceWidth,
        int surfaceHeight)
    {
        int horizontalStep =
            request.LabelWidth + LabelGap;
        int verticalStep =
            request.LabelHeight + LabelGap;
        int rightStart =
            request.AnchorX + PointRadius + LabelGap;
        int leftStart =
            request.AnchorX - PointRadius -
            LabelGap - request.LabelWidth;
        int aboveStart =
            request.AnchorY - PointRadius -
            LabelGap - request.LabelHeight;
        int belowStart =
            request.AnchorY + PointRadius + LabelGap;
        int horizontalLanes = Math.Max(
            PreferredHorizontalLanes,
            surfaceWidth / horizontalStep + 1);
        int verticalLanes =
            surfaceHeight / verticalStep + 1;

        for (int verticalLane = 0;
             verticalLane < verticalLanes;
             verticalLane++)
        {
            int[] tops =
            [
                aboveStart - verticalLane * verticalStep,
                belowStart + verticalLane * verticalStep,
            ];
            foreach (int top in tops)
            {
                for (int horizontalLane = 0;
                     horizontalLane < horizontalLanes;
                     horizontalLane++)
                {
                    int distance =
                        horizontalLane * horizontalStep;
                    ScreenRegion right = new(
                        rightStart + distance,
                        top,
                        request.LabelWidth,
                        request.LabelHeight);
                    if (FitsWithMargin(
                            right,
                            surfaceWidth,
                            surfaceHeight))
                    {
                        yield return right;
                    }

                    ScreenRegion left = new(
                        leftStart - distance,
                        top,
                        request.LabelWidth,
                        request.LabelHeight);
                    if (FitsWithMargin(
                            left,
                            surfaceWidth,
                            surfaceHeight))
                    {
                        yield return left;
                    }
                }
            }
        }
    }

    private static bool FitsWithMargin(
        ScreenRegion region,
        int width,
        int height) =>
        region.X >= EdgeMargin &&
        region.Y >= EdgeMargin &&
        region.Right <= width - EdgeMargin &&
        region.Bottom <= height - EdgeMargin;

    private static ScreenRegion PointExclusion(
        PlacementMarkerLabelRequest request)
    {
        int radius = PointRadius + LabelGap;
        return new ScreenRegion(
            request.AnchorX - radius,
            request.AnchorY - radius,
            radius * 2 + 1,
            radius * 2 + 1);
    }

    private static bool HasCollision(
        ScreenRegion candidate,
        IReadOnlyCollection<ScreenRegion> pointExclusions,
        IReadOnlyCollection<ScreenRegion> occupiedLabels) =>
        pointExclusions.Any(region =>
            Intersects(candidate, region)) ||
        occupiedLabels.Any(region =>
            Intersects(
                candidate,
                Expand(region, LabelGap)));

    private static long CollisionArea(
        ScreenRegion candidate,
        IReadOnlyCollection<ScreenRegion> pointExclusions,
        IReadOnlyCollection<ScreenRegion> occupiedLabels) =>
        pointExclusions.Sum(region =>
            OverlapArea(candidate, region)) +
        occupiedLabels.Sum(region =>
            OverlapArea(
                candidate,
                Expand(region, LabelGap)));

    private static long DistanceFromPreferred(
        PlacementMarkerLabelRequest request,
        ScreenRegion candidate)
    {
        int preferredX =
            request.AnchorX + PointRadius + LabelGap;
        int preferredY =
            request.AnchorY - PointRadius -
            LabelGap - request.LabelHeight;
        long deltaX = candidate.X - preferredX;
        long deltaY = candidate.Y - preferredY;
        return deltaX * deltaX + deltaY * deltaY;
    }

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

    private static long OverlapArea(
        ScreenRegion first,
        ScreenRegion second)
    {
        int width = Math.Max(
            0,
            Math.Min(first.Right, second.Right) -
            Math.Max(first.X, second.X));
        int height = Math.Max(
            0,
            Math.Min(first.Bottom, second.Bottom) -
            Math.Max(first.Y, second.Y));
        return (long)width * height;
    }
}
