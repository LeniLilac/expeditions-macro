using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Tests;

public sealed class AdaptiveUiMatcherTests
{
    [Fact]
    public void MapRegion_MapsHalfOpenEdgesWithoutLeavingMatchedOwner()
    {
        ScreenRegion source = new(10, 20, 2, 2);
        ScreenRegion matched = new(100, 200, 3, 3);
        AdaptiveRegionMatch match = new(
            Score: 1,
            Correlation: 1,
            SourceRegion: source,
            MatchedRegion: matched);

        ScreenRegion mapped = match.MapRegion(
            new ScreenRegion(11, 21, 1, 1));

        Assert.Equal(new ScreenRegion(102, 202, 1, 1), mapped);
        Assert.True(mapped.FitsWithin(
            matched.Right,
            matched.Bottom));
    }

    [Fact]
    public void MapRegion_PreservesEveryFractionalScalePartitionBoundary()
    {
        ScreenRegion source = new(17, 23, 20, 12);
        foreach ((int width, int height) in new[]
                 {
                     (17, 10),
                     (19, 11),
                     (21, 13),
                     (23, 14),
                 })
        {
            ScreenRegion matched = new(101, 203, width, height);
            AdaptiveRegionMatch match = new(
                Score: 1,
                Correlation: 1,
                SourceRegion: source,
                MatchedRegion: matched);

            Assert.Equal(matched, match.MapRegion(source));

            for (int y = source.Y; y < source.Bottom; y++)
            {
                for (int x = source.X; x < source.Right; x++)
                {
                    ScreenRegion trailingPartition = match.MapRegion(
                        new ScreenRegion(
                            x,
                            y,
                            source.Right - x,
                            source.Bottom - y));

                    Assert.Equal(
                        matched.Right,
                        trailingPartition.Right);
                    Assert.Equal(
                        matched.Bottom,
                        trailingPartition.Bottom);
                }
            }
        }
    }
}
