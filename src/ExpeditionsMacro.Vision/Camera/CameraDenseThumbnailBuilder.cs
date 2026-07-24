using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Camera;

public static class CameraDenseThumbnailBuilder
{
    public const int DefaultWidth = 160;

    public static ImageFrame Build(
        ImageFrame fullClientFrame,
        IReadOnlyList<ScreenRegion> regions,
        int width = DefaultWidth)
    {
        if (fullClientFrame.Format != PixelFormat.Rgb24)
        {
            throw new ArgumentException(
                "Dense camera thumbnails require an RGB client frame.",
                nameof(fullClientFrame));
        }
        if (regions.Count is < 2 or > 8 ||
            regions.Any(region =>
                !region.FitsWithin(
                    fullClientFrame.Width,
                    fullClientFrame.Height)))
        {
            throw new ArgumentException(
                "Dense camera regions are invalid.",
                nameof(regions));
        }
        if (width is < 80 or > 320)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        const int columns = 2;
        int rows = (regions.Count + columns - 1) / columns;
        int compositeWidth =
            columns * CameraRegionAnalyzer.CompositeTileWidth;
        int compositeHeight =
            rows * CameraRegionAnalyzer.CompositeTileHeight;
        int height = Math.Max(
            24,
            (int)Math.Round(
                compositeHeight * (double)width / compositeWidth));
        byte[] output = new byte[width * height];
        for (int index = 0; index < regions.Count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            int x0 = column * width / columns;
            int x1 = (column + 1) * width / columns;
            int y0 = row * height / rows;
            int y1 = (row + 1) * height / rows;
            ResizeRegion(
                fullClientFrame,
                regions[index],
                output,
                width,
                x0,
                y0,
                x1,
                y1);
        }
        NormalizeContrast(output);
        return new ImageFrame(
            width,
            height,
            PixelFormat.Gray8,
            output,
            takeOwnership: true);
    }

    private static void ResizeRegion(
        ImageFrame source,
        ScreenRegion region,
        byte[] destination,
        int destinationWidth,
        int x0,
        int y0,
        int x1,
        int y1)
    {
        int tileWidth = x1 - x0;
        int tileHeight = y1 - y0;
        for (int y = 0; y < tileHeight; y++)
        {
            int sourceY0 =
                region.Y + y * region.Height / tileHeight;
            int sourceY1 =
                region.Y +
                Math.Max(
                    0,
                    (y + 1) * region.Height / tileHeight - 1);
            for (int x = 0; x < tileWidth; x++)
            {
                int sourceX0 =
                    region.X + x * region.Width / tileWidth;
                int sourceX1 =
                    region.X +
                    Math.Max(
                        0,
                        (x + 1) * region.Width / tileWidth - 1);
                int total =
                    Luminance(source, sourceX0, sourceY0) +
                    Luminance(source, sourceX1, sourceY0) +
                    Luminance(source, sourceX0, sourceY1) +
                    Luminance(source, sourceX1, sourceY1);
                destination[(y0 + y) * destinationWidth + x0 + x] =
                    (byte)(total / 4);
            }
        }
    }

    private static int Luminance(
        ImageFrame source,
        int x,
        int y)
    {
        int offset = (y * source.Width + x) * 3;
        int red = source.Pixels[offset];
        int green = source.Pixels[offset + 1];
        int blue = source.Pixels[offset + 2];
        return (77 * red + 150 * green + 29 * blue) >> 8;
    }

    private static void NormalizeContrast(byte[] pixels)
    {
        long sum = 0;
        long squareSum = 0;
        foreach (byte value in pixels)
        {
            sum += value;
            squareSum += value * value;
        }
        double mean = (double)sum / pixels.Length;
        double variance = Math.Max(
            0,
            (double)squareSum / pixels.Length - mean * mean);
        double deviation = Math.Sqrt(variance);
        if (deviation < 1) return;
        double scale = Math.Clamp(42 / deviation, 0.65, 2.4);
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)Math.Clamp(
                (int)Math.Round(
                    128 + (pixels[index] - mean) * scale),
                0,
                255);
        }
    }
}
