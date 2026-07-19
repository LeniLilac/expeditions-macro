using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class StartDialogDetector
{
    private static readonly ScreenRegion SearchRegion = new(270, 120, 270, 100);

    private readonly record struct GreenComponent(int Count, int Left, int Top, int Width, int Height);

    private readonly record struct ButtonMatch(double Score, GreenComponent Component);

    public static double Score(ImageFrame image) => Find(image)?.Score ?? 0;

    public static (int X, int Y)? ActionFor(ImageFrame image)
    {
        if (Find(image) is not ButtonMatch match) return null;
        return (
            (int)Math.Round(match.Component.Left + (match.Component.Width - 1) / 2d, MidpointRounding.AwayFromZero),
            (int)Math.Round(match.Component.Top + (match.Component.Height - 1) / 2d, MidpointRounding.AwayFromZero));
    }

    private static ButtonMatch? Find(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || !SearchRegion.FitsWithin(image.Width, image.Height)) return null;

        int width = SearchRegion.Width;
        int height = SearchRegion.Height;
        bool[] green = new bool[width * height];
        bool[] visited = new bool[green.Length];
        int[] queue = new int[green.Length];
        for (int localY = 0; localY < height; localY++)
        {
            int y = SearchRegion.Y + localY;
            for (int localX = 0; localX < width; localX++)
            {
                int x = SearchRegion.X + localX;
                int pixel = (y * image.Width + x) * 3;
                green[localY * width + localX] = IsButtonGreen(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2]);
            }
        }

        ButtonMatch? best = null;
        for (int start = 0; start < green.Length; start++)
        {
            if (!green[start] || visited[start]) continue;

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

            if (count < 100) continue;
            GreenComponent component = new(
                count,
                SearchRegion.X + minimumX,
                SearchRegion.Y + minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
            double score = ScoreComponent(image, component);
            if (score > 0 && (best is null || score > best.Value.Score)) best = new ButtonMatch(score, component);

            void Enqueue(int index)
            {
                if (!green[index] || visited[index]) return;
                visited[index] = true;
                queue[tail++] = index;
            }
        }

        return best;
    }

    private static double ScoreComponent(ImageFrame image, GreenComponent component)
    {
        if (component.Width is < 135 or > 210 || component.Height is < 14 or > 30) return 0;
        double fill = (double)component.Count / (component.Width * component.Height);
        // The Start button loses much of its saturated green fill while hovered
        // or while the node transition animation is fading. Geometry plus the
        // dark dialog header remain stable and are the authoritative signals.
        if (fill < 0.35) return 0;

        double centerX = component.Left + (component.Width - 1) / 2d;
        double centerY = component.Top + (component.Height - 1) / 2d;
        double normalizedDistance = Math.Sqrt(
            Math.Pow((centerX - 404) / 45, 2) +
            Math.Pow((centerY - 180) / 45, 2));
        double centerScore = Math.Clamp(1 - normalizedDistance, 0, 1);
        if (centerScore == 0) return 0;

        int panelHeight = (int)Math.Round(component.Height * 3.35);
        int headerHeight = Math.Max(1, (int)Math.Round(panelHeight * 0.58));
        int headerTop = component.Top - panelHeight;
        int headerLeft = Math.Max(0, component.Left - 5);
        int headerRight = Math.Min(image.Width, component.Left + component.Width + 5);
        if (headerTop < 0 || headerRight <= headerLeft) return 0;

        int darkPixels = 0;
        int neutralTextPixels = 0;
        int totalPixels = checked((headerRight - headerLeft) * headerHeight);
        for (int y = headerTop; y < headerTop + headerHeight; y++)
        {
            for (int x = headerLeft; x < headerRight; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                int luminance = (red + green + blue) / 3;
                if (luminance < 55) darkPixels++;
                int maximum = Math.Max(red, Math.Max(green, blue));
                int minimum = Math.Min(red, Math.Min(green, blue));
                if (luminance > 85 && maximum - minimum < 55) neutralTextPixels++;
            }
        }

        double darkFraction = (double)darkPixels / totalPixels;
        double textFraction = (double)neutralTextPixels / totalPixels;
        if (darkFraction < 0.70 || textFraction < 0.02) return 0;

        double fillScore = Ramp(fill, 0.35, 0.88);
        double sizeScore = (
            Plateau(component.Width, 135, 155, 190, 210) +
            Plateau(component.Height, 14, 18, 24, 30)) / 2;
        double darkScore = Ramp(darkFraction, 0.70, 0.90);
        double textScore = Ramp(textFraction, 0.02, 0.055);
        double quality = Math.Clamp(
            0.18 * fillScore +
            0.18 * sizeScore +
            0.16 * centerScore +
            0.28 * darkScore +
            0.20 * textScore,
            0,
            1);
        // Once all independent gates agree this is the central Start dialog,
        // confidence should not collapse merely because the cursor is hovering.
        return 0.82 + 0.18 * quality;
    }

    private static bool IsButtonGreen(byte red, byte green, byte blue) =>
        green >= 120 &&
        green - red >= 45 &&
        green - blue >= 35 &&
        green * 4 >= red * 5;

    private static double Ramp(double value, double minimum, double maximum) =>
        Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static double Plateau(double value, double minimum, double lower, double upper, double maximum)
    {
        if (value < minimum || value > maximum) return 0;
        if (value >= lower && value <= upper) return 1;
        return value < lower
            ? (value - minimum) / (lower - minimum)
            : (maximum - value) / (maximum - upper);
    }
}
