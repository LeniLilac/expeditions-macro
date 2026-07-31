using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Packs;

internal static class MapSelectionDetector
{
    // Map names and the large preview are localized and can change independently.
    // The selected entry always adds a wide cyan marker in the fixed left gutter,
    // while inactive entries keep only a narrow accent line.
    private static readonly ScreenRegion[] MarkerRegions =
    [
        new(16, 190, 24, 65),
        new(16, 256, 24, 51),
        new(16, 307, 24, 55),
    ];

    private const int MinimumSelectedPixels = 320;
    private const int MinimumPixelGap = 150;

    public static int? Detect(ImageFrame clientImage)
    {
        if (clientImage.Format != PixelFormat.Rgb24) return null;
        // Cyan scenery can cross the same left gutter during active gameplay.
        // Require the live Select Stage control before interpreting any marker
        // geometry as a map choice.
        if (ActionButtonDetector.Score(clientImage, "map_select") <= 0) return null;

        using Mat rgb = ImageCodec.ToMat(clientImage);
        using Mat hsv = new();
        Cv2.CvtColor(rgb, hsv, ColorConversionCodes.RGB2HSV);

        (int Pixels, int Map)[] ranked = MarkerRegions
            .Select((region, index) => (CountCyanPixels(hsv, region), Map: index + 1))
            .OrderByDescending(candidate => candidate.Item1)
            .ToArray();
        if (ranked.Length < 2 ||
            ranked[0].Pixels < MinimumSelectedPixels ||
            ranked[0].Pixels - ranked[1].Pixels < MinimumPixelGap)
        {
            return null;
        }

        return ranked[0].Map;
    }

    private static int CountCyanPixels(Mat hsv, ScreenRegion region)
    {
        if (!region.FitsWithin(hsv.Width, hsv.Height)) return 0;

        int count = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                Vec3b pixel = hsv.At<Vec3b>(y, x);
                if (pixel.Item0 is >= 65 and <= 110 && pixel.Item1 >= 80 && pixel.Item2 >= 70) count++;
            }
        }

        return count;
    }
}
