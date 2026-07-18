using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class TerminalScreenDetector
{
    private static readonly ScreenRegion SearchRegion = new(70, 100, 670, 120);

    private readonly record struct Component(int Count, int Left, int Top, int Width, int Height)
    {
        public double CenterX => Left + (Width - 1) / 2d;

        public double CenterY => Top + (Height - 1) / 2d;
    }

    public static double Score(ImageFrame image, string state)
    {
        if (image.Format != PixelFormat.Rgb24 || !SearchRegion.FitsWithin(image.Width, image.Height)) return 0;
        bool victory = state.Equals("victory", StringComparison.OrdinalIgnoreCase);
        bool defeat = state.Equals("defeat", StringComparison.OrdinalIgnoreCase);
        if (!victory && !defeat) return 0;

        double bannerScore = Components(image, victory)
            .Select(ScoreBanner)
            .DefaultIfEmpty(0)
            .Max();
        double buttonScore = ActionButtonDetector.Score(image, state);
        if (bannerScore < 0.60 || buttonScore < 0.60) return 0;
        return Math.Clamp(0.72 * bannerScore + 0.28 * buttonScore, 0, 1);
    }

    private static double ScoreBanner(Component component)
    {
        if (component.Width is < 120 or > 220 || component.Height is < 15 or > 38) return 0;
        double fill = (double)component.Count / (component.Width * component.Height);
        if (fill is < 0.35 or > 0.95) return 0;
        double horizontal = Plateau(Math.Abs(component.CenterX - 211), 0, 0, 45, 120);
        double vertical = Plateau(Math.Abs(component.CenterY - 163), 0, 0, 28, 65);
        double width = Plateau(component.Width, 120, 145, 190, 220);
        double height = Plateau(component.Height, 15, 20, 31, 38);
        return Math.Clamp(0.30 * horizontal + 0.25 * vertical + 0.25 * width + 0.20 * height, 0, 1);
    }

    private static IReadOnlyList<Component> Components(ImageFrame image, bool victory)
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
                mask[localY * width + localX] = victory
                    ? blue >= 100 && green >= 80 && blue - red >= 35 && green - red >= 25
                    : red >= 90 && red - green >= 35 && red - blue >= 20;
            }
        }

        List<Component> components = [];
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

            if (count >= 150)
            {
                components.Add(new Component(
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

    private static double Plateau(double value, double minimum, double lower, double upper, double maximum)
    {
        if (value < minimum || value > maximum) return 0;
        if (value >= lower && value <= upper) return 1;
        return value < lower
            ? (value - minimum) / (lower - minimum)
            : (maximum - value) / (maximum - upper);
    }
}
