using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class AfkChamberDetector
{
    public const double Threshold = 0.84;

    private static readonly ScreenRegion HeaderRegion = new(270, 12, 270, 70);
    private static readonly ScreenRegion ButtonRegion = new(220, 530, 370, 81);

    private enum ButtonColor
    {
        Yellow,
        Neutral,
    }

    private readonly record struct Component(int Count, int Left, int Top, int Width, int Height)
    {
        public int Right => Left + Width - 1;

        public double CenterX => Left + (Width - 1) / 2d;

        public double CenterY => Top + (Height - 1) / 2d;
    }

    private readonly record struct Match(double Score, Component ReturnToLobby);

    public static double Score(ImageFrame image) => Find(image)?.Score ?? 0;

    public static (int X, int Y)? ActionFor(ImageFrame image)
    {
        if (Find(image) is not Match match) return null;
        return (
            (int)Math.Round(match.ReturnToLobby.CenterX, MidpointRounding.AwayFromZero),
            (int)Math.Round(match.ReturnToLobby.CenterY, MidpointRounding.AwayFromZero));
    }

    private static Match? Find(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            !HeaderRegion.FitsWithin(image.Width, image.Height) ||
            !ButtonRegion.FitsWithin(image.Width, image.Height)) return null;

        double headerScore = ScoreHeader(image);
        if (headerScore == 0) return null;

        IReadOnlyList<Component> yellow = Components(image, ButtonColor.Yellow);
        IReadOnlyList<Component> neutral = Components(image, ButtonColor.Neutral);
        Match? best = null;
        foreach (Component selectStage in yellow)
        {
            double selectScore = ScoreButton(selectStage, expectedCenterX: 336);
            if (selectScore == 0) continue;
            foreach (Component returnToLobby in neutral)
            {
                double returnScore = ScoreButton(returnToLobby, expectedCenterX: 470);
                if (returnScore == 0 || returnToLobby.CenterX <= selectStage.CenterX) continue;

                double vertical = Plateau(Math.Abs(returnToLobby.CenterY - selectStage.CenterY), 0, 0, 3, 10);
                double gap = Plateau(returnToLobby.Left - selectStage.Right - 1, -4, 2, 18, 32);
                double sizeAgreement = (
                    Plateau(Math.Abs(returnToLobby.Width - selectStage.Width), 0, 0, 14, 34) +
                    Plateau(Math.Abs(returnToLobby.Height - selectStage.Height), 0, 0, 5, 12)) / 2;
                if (vertical == 0 || gap == 0 || sizeAgreement == 0) continue;

                double quality = Math.Clamp(
                    0.28 * headerScore +
                    0.22 * selectScore +
                    0.22 * returnScore +
                    0.12 * vertical +
                    0.09 * gap +
                    0.07 * sizeAgreement,
                    0,
                    1);
                double score = 0.84 + 0.16 * quality;
                if (best is null || score > best.Value.Score) best = new Match(score, returnToLobby);
            }
        }
        return best;
    }

    private static double ScoreHeader(ImageFrame image)
    {
        int count = 0;
        int minimumX = HeaderRegion.Right;
        int minimumY = HeaderRegion.Bottom;
        int maximumX = HeaderRegion.X;
        int maximumY = HeaderRegion.Y;
        for (int y = HeaderRegion.Y; y < HeaderRegion.Bottom; y++)
        {
            for (int x = HeaderRegion.X; x < HeaderRegion.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                if (!IsGold(red, green, blue)) continue;
                count++;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
        }

        if (count < 180) return 0;
        int width = maximumX - minimumX + 1;
        int height = maximumY - minimumY + 1;
        double countScore = Plateau(count, 180, 260, 950, 1500);
        double widthScore = Plateau(width, 105, 135, 235, 265);
        double heightScore = Plateau(height, 12, 17, 48, 62);
        double centerX = minimumX + (width - 1) / 2d;
        double centerScore = Plateau(Math.Abs(centerX - 404), 0, 0, 45, 100);
        if (countScore == 0 || widthScore == 0 || heightScore == 0 || centerScore == 0) return 0;
        return Math.Clamp(0.35 * countScore + 0.25 * widthScore + 0.18 * heightScore + 0.22 * centerScore, 0, 1);
    }

    private static double ScoreButton(Component component, double expectedCenterX)
    {
        if (component.Width is < 88 or > 170 || component.Height is < 18 or > 48) return 0;
        double fill = (double)component.Count / (component.Width * component.Height);
        if (fill is < 0.24 or > 0.98) return 0;
        double width = Plateau(component.Width, 88, 110, 145, 170);
        double height = Plateau(component.Height, 18, 25, 40, 48);
        double horizontal = Plateau(Math.Abs(component.CenterX - expectedCenterX), 0, 0, 28, 62);
        // UI scaling is centered in the Roblox client, so bottom controls move
        // upward as they shrink even when the client itself remains 808 Ã— 611.
        double vertical = Plateau(Math.Abs(component.CenterY - 584), 0, 0, 18, 52);
        double fillScore = Plateau(fill, 0.24, 0.35, 0.82, 0.98);
        if (width == 0 || height == 0 || horizontal == 0 || vertical == 0 || fillScore == 0) return 0;
        return Math.Clamp(0.20 * width + 0.18 * height + 0.24 * horizontal + 0.20 * vertical + 0.18 * fillScore, 0, 1);
    }

    private static IReadOnlyList<Component> Components(ImageFrame image, ButtonColor color)
    {
        int width = ButtonRegion.Width;
        int height = ButtonRegion.Height;
        bool[] mask = new bool[width * height];
        bool[] visited = new bool[mask.Length];
        int[] queue = new int[mask.Length];
        for (int localY = 0; localY < height; localY++)
        {
            int y = ButtonRegion.Y + localY;
            for (int localX = 0; localX < width; localX++)
            {
                int x = ButtonRegion.X + localX;
                int pixel = (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                mask[localY * width + localX] = color == ButtonColor.Yellow
                    ? IsGold(red, green, blue)
                    : IsNeutral(red, green, blue);
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

            if (count >= 80)
            {
                components.Add(new Component(
                    count,
                    ButtonRegion.X + minimumX,
                    ButtonRegion.Y + minimumY,
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

    private static bool IsGold(byte red, byte green, byte blue) =>
        red >= 125 && green >= 80 && red - blue >= 55 && green - blue >= 35;

    private static bool IsNeutral(byte red, byte green, byte blue)
    {
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        return minimum >= 55 && maximum - minimum <= 28;
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
