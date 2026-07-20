using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class RewardScreenDetector
{
    private static readonly ScreenRegion HeaderRegion = new(140, 150, 530, 55);
    private static readonly ScreenRegion SearchRegion = new(90, 340, 630, 100);

    private readonly record struct RedMark(double X, double Y);

    private readonly record struct HeaderMatch(double Score, int ProgressY);

    private readonly record struct RewardMatch(double Score, RedMark ActionMark, double Scale);

    public static double Score(ImageFrame image) => Find(image)?.Score ?? 0;

    public static bool HasHeader(ImageFrame image) => FindHeader(image) is not null;

    public static (int X, int Y)? ActionFor(ImageFrame image)
    {
        if (Find(image) is not RewardMatch match) return null;
        double scale = Math.Clamp(match.Scale, 0.75, 1.25);
        return (
            (int)Math.Round(match.ActionMark.X + 51 * scale, MidpointRounding.AwayFromZero),
            (int)Math.Round(match.ActionMark.Y + 2 * scale, MidpointRounding.AwayFromZero));
    }

    private static RewardMatch? Find(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || !SearchRegion.FitsWithin(image.Width, image.Height)) return null;
        if (FindHeader(image) is not HeaderMatch header) return null;
        IReadOnlyList<RedMark> marks = Marks(image);
        RewardMatch? best = null;
        for (int leftIndex = 0; leftIndex < marks.Count - 2; leftIndex++)
        {
            for (int middleIndex = leftIndex + 1; middleIndex < marks.Count - 1; middleIndex++)
            {
                for (int rightIndex = middleIndex + 1; rightIndex < marks.Count; rightIndex++)
                {
                    RedMark left = marks[leftIndex];
                    RedMark middle = marks[middleIndex];
                    RedMark right = marks[rightIndex];
                    double firstGap = middle.X - left.X;
                    double secondGap = right.X - middle.X;
                    double verticalSpread = new[] { left.Y, middle.Y, right.Y }.Max() - new[] { left.Y, middle.Y, right.Y }.Min();
                    if (firstGap is < 150 or > 260 || secondGap is < 150 or > 260 || verticalSpread > 12 ||
                        Math.Abs(middle.X - 353) > 75 || Math.Abs(middle.Y - 388) > 40) continue;

                    double gapScore = (
                        Plateau(firstGap, 150, 180, 235, 260) +
                        Plateau(secondGap, 150, 180, 235, 260)) / 2;
                    double agreementScore = Plateau(Math.Abs(firstGap - secondGap), 0, 0, 18, 40);
                    double verticalScore = Plateau(verticalSpread, 0, 0, 5, 12);
                    double centerScore = Plateau(Math.Abs(middle.X - 353), 0, 0, 35, 75);
                    double rowScore = Plateau(Math.Abs(middle.Y - 388), 0, 0, 24, 40);
                    double score = Math.Clamp(
                        0.20 * header.Score +
                        0.23 * gapScore +
                        0.20 * agreementScore +
                        0.15 * verticalScore +
                        0.11 * centerScore +
                        0.11 * rowScore,
                        0,
                        1);
                    double scale = ((middle.X - left.X) + (right.X - middle.X)) / 420d;
                    if (best is null || score > best.Value.Score) best = new RewardMatch(score, left, scale);
                }
            }
        }

        // During the card entrance animation Roblox can leave only two complete
        // Select Upgrade buttons on screen. Pair the surviving mouse marks instead
        // of requiring all three cards to have reached their final positions.
        for (int leftIndex = 0; leftIndex < marks.Count - 1; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < marks.Count; rightIndex++)
            {
                RedMark left = marks[leftIndex];
                RedMark right = marks[rightIndex];
                double gap = right.X - left.X;
                double verticalSpread = Math.Abs(right.Y - left.Y);
                double rowOffset = ((left.Y + right.Y) / 2) - header.ProgressY;
                if (gap is < 120 or > 290 || verticalSpread > 18 || rowOffset is < 145 or > 250) continue;

                double gapScore = Plateau(gap, 120, 165, 255, 290);
                double verticalScore = Plateau(verticalSpread, 0, 0, 7, 18);
                double rowScore = Plateau(rowOffset, 145, 165, 225, 250);
                double quality = Math.Clamp(
                    0.45 * header.Score +
                    0.30 * gapScore +
                    0.15 * verticalScore +
                    0.10 * rowScore,
                    0,
                    1);
                double score = 0.78 + 0.22 * quality;
                double scale = Math.Clamp(gap / 210d, 0.75, 1.15);
                if (best is null || score > best.Value.Score) best = new RewardMatch(score, left, scale);
            }
        }

        return best is RewardMatch match && match.Score >= 0.76 ? match : null;
    }

    private static HeaderMatch? FindHeader(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || !HeaderRegion.FitsWithin(image.Width, image.Height)) return null;
        // The reward chooser applies a strong blue/cyan wash to the entire game
        // view. Requiring that overlay prevents bright sky or scenery behind a
        // Start dialog from masquerading as the cyan reward progress bar.
        if (BlueOverlayFraction(image) < 0.60) return null;
        double bestRow = 0;
        int bestY = 0;
        int supportingRows = 0;
        int currentSupportingRun = 0;
        int longestSupportingRun = 0;
        for (int y = HeaderRegion.Y; y < HeaderRegion.Bottom; y++)
        {
            int count = 0;
            int minimumX = HeaderRegion.Right;
            int maximumX = -1;
            for (int x = HeaderRegion.X; x < HeaderRegion.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                if (green < 85 || blue < 55 || green - red < 18 || blue - red < 8) continue;
                count++;
                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
            }

            int span = maximumX < 0 ? 0 : maximumX - minimumX + 1;
            double density = span == 0 ? 0 : (double)count / span;
            if (count < 105 || span < 190 || density < 0.30)
            {
                currentSupportingRun = 0;
                continue;
            }
            supportingRows++;
            currentSupportingRun++;
            longestSupportingRun = Math.Max(longestSupportingRun, currentSupportingRun);
            double rowScore = Math.Clamp(
                0.40 * Ramp(count, 105, 300) +
                0.25 * Ramp(span, 190, 360) +
                0.35 * Ramp(density, 0.30, 0.90),
                0,
                1);
            if (rowScore <= bestRow) continue;
            bestRow = rowScore;
            bestY = y;
        }

        // The actual reward progress bar is a thin horizontal strip. A translated
        // frame may also expose a separate card-art run, so constrain contiguous
        // thickness instead of the total row count. Very blue maps can satisfy
        // the same pixel test across most of this region; treating that broad
        // field as a header can suppress a valid Start dialog.
        if (supportingRows < 3 || longestSupportingRun > 18) return null;
        double quality = 0.80 * bestRow + 0.20 * Ramp(supportingRows, 3, 8);
        return new HeaderMatch(0.70 + 0.30 * quality, bestY);
    }

    private static double BlueOverlayFraction(ImageFrame image)
    {
        int blue = 0;
        int total = 0;
        for (int y = 90; y < image.Height - 11; y += 3)
        {
            for (int x = 5; x < image.Width - 5; x += 3)
            {
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte channelBlue = image.Pixels[pixel + 2];
                if (channelBlue - red >= 25 && green - red >= 8) blue++;
                total++;
            }
        }
        return total == 0 ? 0 : (double)blue / total;
    }

    private static IReadOnlyList<RedMark> Marks(ImageFrame image)
    {
        int width = SearchRegion.Width;
        int height = SearchRegion.Height;
        bool[] mask = new bool[width * height];
        bool[] visited = new bool[mask.Length];
        int[] queue = new int[mask.Length];
        for (int localY = 0; localY < height; localY++)
        {
            int y = SearchRegion.Y + localY;
            for (int localX = 0; localX < width; localX++)
            {
                int x = SearchRegion.X + localX;
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                mask[localY * width + localX] = red >= 85 && red - green >= 15 && red - blue >= 10;
            }
        }

        List<RedMark> marks = [];
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            visited[start] = true;
            int count = 0;
            int minimumX = width;
            int minimumY = height;
            int maximumX = 0;
            int maximumY = 0;
            double sumX = 0;
            double sumY = 0;
            while (head < tail)
            {
                int current = queue[head++];
                int x = current % width;
                int y = current / width;
                count++;
                sumX += x;
                sumY += y;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                if (x > 0) Enqueue(current - 1);
                if (x + 1 < width) Enqueue(current + 1);
                if (y > 0) Enqueue(current - width);
                if (y + 1 < height) Enqueue(current + width);
            }

            int componentWidth = maximumX - minimumX + 1;
            int componentHeight = maximumY - minimumY + 1;
            if (count is >= 2 and <= 40 && componentWidth <= 12 && componentHeight <= 18)
            {
                marks.Add(new RedMark(SearchRegion.X + sumX / count, SearchRegion.Y + sumY / count));
            }

            void Enqueue(int index)
            {
                if (!mask[index] || visited[index]) return;
                visited[index] = true;
                queue[tail++] = index;
            }
        }
        return marks.OrderBy(mark => mark.X).ToArray();
    }

    private static double Plateau(double value, double minimum, double lower, double upper, double maximum)
    {
        if (value < minimum || value > maximum) return 0;
        if (value >= lower && value <= upper) return 1;
        return value < lower
            ? (value - minimum) / (lower - minimum)
            : (maximum - value) / (maximum - upper);
    }

    private static double Ramp(double value, double minimum, double maximum) =>
        Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
}
