using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class RewardScreenDetector
{
    private static readonly ScreenRegion SearchRegion = new(90, 340, 630, 100);

    private readonly record struct RedMark(double X, double Y);

    private readonly record struct RewardMatch(double Score, RedMark Left, RedMark Middle, RedMark Right)
    {
        public double Scale => ((Middle.X - Left.X) + (Right.X - Middle.X)) / 420d;
    }

    public static double Score(ImageFrame image) => Find(image)?.Score ?? 0;

    public static (int X, int Y)? ActionFor(ImageFrame image)
    {
        if (Find(image) is not RewardMatch match) return null;
        double scale = Math.Clamp(match.Scale, 0.75, 1.25);
        return (
            (int)Math.Round(match.Left.X + 51 * scale, MidpointRounding.AwayFromZero),
            (int)Math.Round((match.Left.Y + match.Middle.Y + match.Right.Y) / 3 + 2 * scale, MidpointRounding.AwayFromZero));
    }

    private static RewardMatch? Find(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || !SearchRegion.FitsWithin(image.Width, image.Height)) return null;
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
                        0.28 * gapScore +
                        0.24 * agreementScore +
                        0.18 * verticalScore +
                        0.15 * centerScore +
                        0.15 * rowScore,
                        0,
                        1);
                    if (best is null || score > best.Value.Score) best = new RewardMatch(score, left, middle, right);
                }
            }
        }
        return best is RewardMatch match && match.Score >= 0.76 ? match : null;
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
}
