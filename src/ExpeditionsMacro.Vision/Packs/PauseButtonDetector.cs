using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class PauseButtonDetector
{
    private static readonly ScreenRegion SearchRegion = new(285, 440, 240, 95);

    private readonly record struct ColorComponent(int Count, int Left, int Top, int Width, int Height)
    {
        public int Right => Left + Width - 1;

        public double CenterX => Left + (Width - 1) / 2d;

        public double CenterY => Top + (Height - 1) / 2d;
    }

    private readonly record struct ButtonMatch(double Score, ColorComponent Component);

    private readonly record struct CheckpointMatch(double Score, ColorComponent Extract, ColorComponent Continue);

    public static double ScoreContinue(ImageFrame image) => FindContinue(image)?.Score ?? 0;

    public static double ScoreCheckpoint(ImageFrame image) => FindCheckpoint(image)?.Score ?? 0;

    public static (int X, int Y)? ActionFor(ImageFrame image, string state)
    {
        if (state.Equals("continue", StringComparison.OrdinalIgnoreCase) && FindContinue(image) is ButtonMatch continuation)
        {
            return Center(continuation.Component);
        }

        if (!state.Equals("checkpoint", StringComparison.OrdinalIgnoreCase) &&
            !state.Equals("extract", StringComparison.OrdinalIgnoreCase)) return null;
        if (FindCheckpoint(image) is not CheckpointMatch checkpoint) return null;
        if (state.Equals("checkpoint", StringComparison.OrdinalIgnoreCase)) return Center(checkpoint.Continue);
        if (state.Equals("extract", StringComparison.OrdinalIgnoreCase)) return Center(checkpoint.Extract);
        return null;
    }

    private static ButtonMatch? FindContinue(ImageFrame image)
    {
        if (!Valid(image)) return null;
        IReadOnlyList<ColorComponent> greenComponents = Components(image, IsButtonGreen);
        IReadOnlyList<ColorComponent> redComponents = Components(image, IsButtonRed);
        ButtonMatch? best = null;
        foreach (ColorComponent green in greenComponents)
        {
            double score = ScoreButton(green, expectedCenterX: 404);
            if (score == 0 || HasPairedButtonToLeft(green, redComponents)) continue;
            if (best is null || score > best.Value.Score) best = new ButtonMatch(score, green);
        }
        return best;
    }

    private static CheckpointMatch? FindCheckpoint(ImageFrame image)
    {
        if (!Valid(image)) return null;
        IReadOnlyList<ColorComponent> greenComponents = Components(image, IsButtonGreen);
        IReadOnlyList<ColorComponent> redComponents = Components(image, IsButtonRed);
        CheckpointMatch? best = null;
        foreach (ColorComponent green in greenComponents)
        {
            double greenScore = ScoreButton(green, expectedCenterX: 448);
            if (greenScore == 0) continue;
            foreach (ColorComponent red in redComponents)
            {
                double redScore = ScoreButton(red, expectedCenterX: 360);
                if (redScore == 0) continue;

                double verticalScore = Plateau(Math.Abs(green.CenterY - red.CenterY), 0, 0, 3, 8);
                double gapScore = Plateau(green.Left - red.Right - 1, -4, 1, 12, 22);
                double sizeAgreement = (
                    Plateau(Math.Abs(green.Width - red.Width), 0, 0, 12, 26) +
                    Plateau(Math.Abs(green.Height - red.Height), 0, 0, 4, 10)) / 2;
                if (verticalScore == 0 || gapScore == 0 || sizeAgreement == 0) continue;

                double score = Math.Clamp(
                    0.31 * greenScore +
                    0.31 * redScore +
                    0.16 * verticalScore +
                    0.12 * gapScore +
                    0.10 * sizeAgreement,
                    0,
                    1);
                if (best is null || score > best.Value.Score) best = new CheckpointMatch(score, red, green);
            }
        }
        return best;
    }

    private static bool HasPairedButtonToLeft(ColorComponent green, IReadOnlyList<ColorComponent> redComponents) =>
        redComponents.Any(red =>
            red.CenterX < green.CenterX &&
            Math.Abs(red.CenterY - green.CenterY) <= 8 &&
            green.Left - red.Right - 1 is >= -4 and <= 22 &&
            Math.Abs(red.Width - green.Width) <= 26 &&
            Math.Abs(red.Height - green.Height) <= 10);

    private static double ScoreButton(ColorComponent component, double expectedCenterX)
    {
        if (component.Width is < 65 or > 120 || component.Height is < 14 or > 36) return 0;
        double fill = (double)component.Count / (component.Width * component.Height);
        if (fill is < 0.38 or > 0.92) return 0;

        double widthScore = Plateau(component.Width, 65, 78, 101, 120);
        double heightScore = Plateau(component.Height, 14, 20, 29, 36);
        double horizontalScore = Plateau(Math.Abs(component.CenterX - expectedCenterX), 0, 0, 9, 24);
        double verticalScore = Plateau(component.CenterY, 445, 468, 510, 530);
        double fillScore = Plateau(fill, 0.38, 0.55, 0.82, 0.92);
        if (widthScore == 0 || heightScore == 0 || horizontalScore == 0 || verticalScore == 0 || fillScore == 0) return 0;

        return Math.Clamp(
            0.20 * widthScore +
            0.18 * heightScore +
            0.24 * horizontalScore +
            0.16 * verticalScore +
            0.22 * fillScore,
            0,
            1);
    }

    private static IReadOnlyList<ColorComponent> Components(ImageFrame image, Func<byte, byte, byte, bool> predicate)
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
                mask[localY * width + localX] = predicate(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2]);
            }
        }

        List<ColorComponent> components = [];
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
            while (head < tail)
            {
                int current = queue[head++];
                int x = current % width;
                int y = current / width;
                count++;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);

                if (x > 0) Enqueue(current - 1);
                if (x + 1 < width) Enqueue(current + 1);
                if (y > 0) Enqueue(current - width);
                if (y + 1 < height) Enqueue(current + width);
            }

            if (count >= 100)
            {
                components.Add(new ColorComponent(
                    count,
                    SearchRegion.X + minimumX,
                    SearchRegion.Y + minimumY,
                    maximumX - minimumX + 1,
                    maximumY - minimumY + 1));
            }

            void Enqueue(int index)
            {
                if (!mask[index] || visited[index]) return;
                visited[index] = true;
                queue[tail++] = index;
            }
        }

        return components;
    }

    private static bool Valid(ImageFrame image) =>
        image.Format == PixelFormat.Rgb24 && SearchRegion.FitsWithin(image.Width, image.Height);

    private static bool IsButtonGreen(byte red, byte green, byte blue) =>
        green >= 120 &&
        green - red >= 45 &&
        green - blue >= 35 &&
        green * 4 >= red * 5;

    private static bool IsButtonRed(byte red, byte green, byte blue) =>
        red >= 110 &&
        red - green >= 35 &&
        red - blue >= 25 &&
        red * 4 >= green * 5;

    private static (int X, int Y) Center(ColorComponent component) =>
        (
            (int)Math.Round(component.CenterX, MidpointRounding.AwayFromZero),
            (int)Math.Round(component.CenterY, MidpointRounding.AwayFromZero));

    private static double Plateau(double value, double minimum, double lower, double upper, double maximum)
    {
        if (value < minimum || value > maximum) return 0;
        if (value >= lower && value <= upper) return 1;
        return value < lower
            ? (value - minimum) / (lower - minimum)
            : (maximum - value) / (maximum - upper);
    }
}
