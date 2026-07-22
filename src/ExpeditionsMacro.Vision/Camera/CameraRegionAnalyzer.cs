using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Infrastructure;
using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Camera;

public static class CameraRegionAnalyzer
{
    public const int SelectedRegionCount = 4;
    private const int CompositeTileWidth = 152;
    private const int CompositeTileHeight = 96;

    private sealed record Candidate(ScreenRegion Region, double Stability, double Texture)
    {
        public double Quality => 0.68 * Stability + 0.32 * Texture;
    }

    public static IReadOnlyList<ScreenRegion> SelectStableRegions(
        IReadOnlyList<ImageFrame> fullClientFrames,
        int desiredCount = SelectedRegionCount)
    {
        if (fullClientFrames.Count < 2) throw new ArgumentException("At least two full-client captures are required.", nameof(fullClientFrames));
        ImageFrame first = fullClientFrames[0];
        if (fullClientFrames.Any(frame => frame.Width != first.Width || frame.Height != first.Height || frame.Format != PixelFormat.Rgb24))
        {
            throw new ArgumentException("Automatic camera-region captures must be identical RGB client images.", nameof(fullClientFrames));
        }
        if (desiredCount is < 2 or > 8) throw new ArgumentOutOfRangeException(nameof(desiredCount));

        List<Candidate> candidates = [];
        foreach (ScreenRegion region in CandidateRegions(first.Width, first.Height))
        {
            ImageFrame[] crops = fullClientFrames.Select(frame => frame.Crop(region)).ToArray();
            (_, ImageFrame reference, double baseline) = VisionScorer.BuildReference(crops);
            double texture = TextureScore(reference);
            if (texture < 0.10 || baseline < 0.45) continue;
            candidates.Add(new Candidate(region, baseline, texture));
        }
        if (candidates.Count < desiredCount)
        {
            throw new InvalidOperationException("The visible map did not contain enough stable, detailed areas for automatic camera alignment. Hide temporary menus and effects, then retry setup.");
        }

        List<Candidate> remaining = candidates.OrderByDescending(candidate => candidate.Quality).ToList();
        List<Candidate> selected = [];
        double diagonal = Math.Sqrt(first.Width * first.Width + first.Height * first.Height);

        // Prefer independent evidence from the left, center, and right of the map. Without this
        // guard, one high-contrast structure can occupy most of the composite and make a local
        // animation or lighting effect disproportionately important.
        if (desiredCount >= 3)
        {
            ILookup<int, Candidate> horizontalBands = candidates.ToLookup(candidate => HorizontalBand(candidate.Region, first.Width));
            if (Enumerable.Range(0, 3).All(band => horizontalBands[band].Any()))
            {
                foreach (int band in Enumerable.Range(0, 3))
                {
                    Candidate best = horizontalBands[band]
                        .OrderByDescending(candidate => candidate.Quality)
                        .ThenBy(candidate => candidate.Region.Y)
                        .First();
                    selected.Add(best);
                    remaining.Remove(best);
                }
            }
        }
        while (selected.Count < desiredCount)
        {
            Candidate next = remaining
                .OrderByDescending(candidate => candidate.Quality + DiversityBonus(candidate, selected, diagonal))
                .ThenBy(candidate => candidate.Region.Y)
                .ThenBy(candidate => candidate.Region.X)
                .First();
            selected.Add(next);
            remaining.Remove(next);
        }
        return selected.Select(candidate => candidate.Region).ToArray();
    }

    public static ImageFrame BuildComposite(ImageFrame fullClientFrame, IReadOnlyList<ScreenRegion> regions)
    {
        if (regions.Count is < 2 or > 8) throw new ArgumentOutOfRangeException(nameof(regions));
        if (regions.Any(region => !region.FitsWithin(fullClientFrame.Width, fullClientFrame.Height)))
        {
            throw new ArgumentException("A camera region falls outside the client image.", nameof(regions));
        }

        const int columns = 2;
        int rows = (regions.Count + columns - 1) / columns;
        byte[] pixels = new byte[checked(columns * CompositeTileWidth * rows * CompositeTileHeight)];
        int outputWidth = columns * CompositeTileWidth;
        for (int index = 0; index < regions.Count; index++)
        {
            ImageFrame tile = VisionScorer.PrepareGray(
                fullClientFrame.Crop(regions[index]),
                CompositeTileWidth,
                CompositeTileHeight);
            int destinationX = index % columns * CompositeTileWidth;
            int destinationY = index / columns * CompositeTileHeight;
            for (int row = 0; row < CompositeTileHeight; row++)
            {
                Buffer.BlockCopy(
                    tile.Pixels,
                    row * CompositeTileWidth,
                    pixels,
                    (destinationY + row) * outputWidth + destinationX,
                    CompositeTileWidth);
            }
        }
        return new ImageFrame(outputWidth, rows * CompositeTileHeight, PixelFormat.Gray8, pixels, takeOwnership: true);
    }

    public static ImageFrame BuildColorComposite(
        ImageFrame fullClientFrame,
        IReadOnlyList<ScreenRegion> regions,
        int inset = 0)
    {
        if (fullClientFrame.Format != PixelFormat.Rgb24) throw new ArgumentException("Color composite input must be RGB.", nameof(fullClientFrame));
        if (regions.Count is < 2 or > 8) throw new ArgumentOutOfRangeException(nameof(regions));
        if (inset < 0) throw new ArgumentOutOfRangeException(nameof(inset));
        if (regions.Any(region => !region.FitsWithin(fullClientFrame.Width, fullClientFrame.Height)
            || region.Width <= inset * 2
            || region.Height <= inset * 2))
        {
            throw new ArgumentException("A camera region falls outside the client image or is too small for the requested inset.", nameof(regions));
        }

        const int columns = 2;
        int rows = (regions.Count + columns - 1) / columns;
        int outputWidth = columns * CompositeTileWidth;
        byte[] pixels = new byte[checked(outputWidth * rows * CompositeTileHeight * 3)];
        for (int index = 0; index < regions.Count; index++)
        {
            ScreenRegion region = regions[index];
            ScreenRegion inner = new(
                region.X + inset,
                region.Y + inset,
                region.Width - inset * 2,
                region.Height - inset * 2);
            using Mat source = ImageCodec.ToMat(fullClientFrame.Crop(inner));
            using Mat resized = new();
            Cv2.Resize(source, resized, new Size(CompositeTileWidth, CompositeTileHeight), 0, 0, InterpolationFlags.Area);
            ImageFrame tile = ImageCodec.FromMat(resized, PixelFormat.Rgb24);
            int destinationX = index % columns * CompositeTileWidth;
            int destinationY = index / columns * CompositeTileHeight;
            for (int row = 0; row < CompositeTileHeight; row++)
            {
                Buffer.BlockCopy(
                    tile.Pixels,
                    row * CompositeTileWidth * 3,
                    pixels,
                    ((destinationY + row) * outputWidth + destinationX) * 3,
                    CompositeTileWidth * 3);
            }
        }
        return new ImageFrame(outputWidth, rows * CompositeTileHeight, PixelFormat.Rgb24, pixels, takeOwnership: true);
    }

    public static ImageFrame AnnotateGoal(ImageFrame fullClientFrame, IReadOnlyList<ScreenRegion> regions)
    {
        if (fullClientFrame.Format != PixelFormat.Rgb24) throw new ArgumentException("The goal preview must be RGB.", nameof(fullClientFrame));
        byte[] pixels = fullClientFrame.Pixels.ToArray();
        (byte R, byte G, byte B)[] colors =
        [
            (119, 128, 250),
            (85, 184, 135),
            (214, 168, 75),
            (217, 103, 112),
        ];
        for (int index = 0; index < regions.Count; index++)
        {
            ScreenRegion region = regions[index];
            if (!region.FitsWithin(fullClientFrame.Width, fullClientFrame.Height)) continue;
            (byte r, byte g, byte b) = colors[index % colors.Length];
            DrawRectangle(pixels, fullClientFrame.Width, fullClientFrame.Height, region, r, g, b, 3);
        }
        return new ImageFrame(fullClientFrame.Width, fullClientFrame.Height, PixelFormat.Rgb24, pixels, takeOwnership: true);
    }

    private static IReadOnlyList<ScreenRegion> CandidateRegions(int width, int height)
    {
        ScreenRegion[] canonical =
        [
            new(96, 110, 166, 108),
            new(546, 110, 166, 108),
            new(96, 250, 166, 108),
            new(321, 250, 166, 108),
            new(546, 250, 166, 108),
            new(96, 394, 166, 108),
            new(321, 394, 166, 108),
            new(546, 394, 166, 108),
        ];
        double scaleX = (double)width / RobloxClientProfile.Width;
        double scaleY = (double)height / RobloxClientProfile.Height;
        return canonical
            .Select(region => new ScreenRegion(
                (int)Math.Round(region.X * scaleX),
                (int)Math.Round(region.Y * scaleY),
                Math.Max(48, (int)Math.Round(region.Width * scaleX)),
                Math.Max(36, (int)Math.Round(region.Height * scaleY))))
            .Where(region => region.FitsWithin(width, height))
            .ToArray();
    }

    private static double TextureScore(ImageFrame gray)
    {
        double mean = gray.Pixels.Average(value => (double)value);
        double variance = gray.Pixels.Average(value => (value - mean) * (value - mean));
        double deviation = Math.Sqrt(variance);
        double gradientSum = 0;
        int gradientCount = 0;
        for (int y = 1; y < gray.Height; y++)
        {
            int row = y * gray.Width;
            int previousRow = row - gray.Width;
            for (int x = 1; x < gray.Width; x++)
            {
                gradientSum += Math.Abs(gray.Pixels[row + x] - gray.Pixels[row + x - 1]);
                gradientSum += Math.Abs(gray.Pixels[row + x] - gray.Pixels[previousRow + x]);
                gradientCount += 2;
            }
        }
        double gradient = gradientCount == 0 ? 0 : gradientSum / gradientCount;
        return Math.Clamp(0.55 * deviation / 42 + 0.45 * gradient / 24, 0, 1);
    }

    private static double DiversityBonus(Candidate candidate, IReadOnlyList<Candidate> selected, double diagonal)
    {
        if (selected.Count == 0) return 0;
        double centerX = candidate.Region.X + candidate.Region.Width / 2d;
        double centerY = candidate.Region.Y + candidate.Region.Height / 2d;
        double nearest = selected.Min(other =>
        {
            double dx = centerX - (other.Region.X + other.Region.Width / 2d);
            double dy = centerY - (other.Region.Y + other.Region.Height / 2d);
            return Math.Sqrt(dx * dx + dy * dy);
        });
        return 0.12 * nearest / diagonal;
    }

    private static int HorizontalBand(ScreenRegion region, int width)
    {
        double center = region.X + region.Width / 2d;
        return Math.Clamp((int)(3 * center / width), 0, 2);
    }

    private static void DrawRectangle(
        byte[] pixels,
        int width,
        int height,
        ScreenRegion region,
        byte red,
        byte green,
        byte blue,
        int thickness)
    {
        for (int inset = 0; inset < thickness; inset++)
        {
            int left = Math.Clamp(region.X + inset, 0, width - 1);
            int right = Math.Clamp(region.Right - 1 - inset, 0, width - 1);
            int top = Math.Clamp(region.Y + inset, 0, height - 1);
            int bottom = Math.Clamp(region.Bottom - 1 - inset, 0, height - 1);
            for (int x = left; x <= right; x++)
            {
                SetPixel(pixels, width, x, top, red, green, blue);
                SetPixel(pixels, width, x, bottom, red, green, blue);
            }
            for (int y = top; y <= bottom; y++)
            {
                SetPixel(pixels, width, left, y, red, green, blue);
                SetPixel(pixels, width, right, y, red, green, blue);
            }
        }
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, byte red, byte green, byte blue)
    {
        int offset = checked((y * width + x) * 3);
        pixels[offset] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
    }
}
