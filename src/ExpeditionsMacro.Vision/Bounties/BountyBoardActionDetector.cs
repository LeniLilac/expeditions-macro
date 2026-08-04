using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Bounties;

public enum BountyCardActionKind
{
    Reroll,
    Claim,
}

public readonly record struct BountyCardAction(
    BountyCardActionKind Kind,
    int X,
    int Y)
{
    private const int ClaimCenterToCardAnchor = 40;

    public int CardAnchorX =>
        Kind == BountyCardActionKind.Claim
            ? X + ClaimCenterToCardAnchor
            : X;
}

internal static class BountyBoardActionDetector
{
    private static readonly int[] LeftActionColumns =
        [313, 438, 563, 688, 813];
    private static readonly int[] RightActionColumns =
        [263, 388, 513, 638, 763];

    private static readonly ScreenRegion BoardSearch =
        new(180, 215, 628, 285);

    public static IReadOnlyList<BountyCardAction>
        Find(ImageFrame image)
    {
        List<Component> yellow =
            Components(image, IsRerollYellow)
                .Where(component =>
                    component.Count >= 90 &&
                    component.Width is >= 14 and <= 45 &&
                    component.Height is >= 14 and <= 45)
                .ToList();
        List<Component> green =
            Components(image, IsClaimGreen)
                .Where(component =>
                    component.Count >= 450 &&
                    component.Width is >= 42 and <= 68 &&
                    component.Height is >= 13 and <= 24)
                .ToList();

        List<Component> rerolls =
            RerollActions(image, yellow);

        return
        [
            .. rerolls.Select(component =>
                new BountyCardAction(
                    BountyCardActionKind.Reroll,
                    component.CenterX,
                    component.CenterY)),
            .. green.Select(component =>
                new BountyCardAction(
                    BountyCardActionKind.Claim,
                    component.CenterX,
                    component.CenterY)),
        ];
    }

    private static List<Component> RerollActions(
        ImageFrame image,
        IReadOnlyList<Component> yellow)
    {
        IReadOnlyList<Component> left =
            MatchColumns(
                image,
                yellow,
                LeftActionColumns);
        IReadOnlyList<Component> right =
            MatchColumns(
                image,
                yellow,
                RightActionColumns);
        bool useRight =
            right.Count > left.Count ||
            right.Count == left.Count &&
            AlignmentDistance(
                right,
                RightActionColumns) <
            AlignmentDistance(
                left,
                LeftActionColumns);
        return (useRight
                ? right
                : left)
            .ToList();
    }

    private static int AlignmentDistance(
        IReadOnlyList<Component> components,
        IReadOnlyList<int> columns) =>
        components.Sum(component =>
            columns.Min(column =>
                Math.Abs(
                    column -
                    component.CenterX)));

    private static IReadOnlyList<Component>
        MatchColumns(
        ImageFrame image,
        IReadOnlyList<Component> yellow,
        IReadOnlyList<int> columns) =>
        yellow
            .Where(candidate =>
                candidate.Top >= 300 &&
                DarkRailFraction(
                    image,
                    candidate) >= 0.42)
            .Select(candidate => new
            {
                Component = candidate,
                Column = columns
                    .OrderBy(column =>
                        Math.Abs(
                            column -
                            candidate.CenterX))
                    .First(),
                Distance = columns.Min(
                    column =>
                        Math.Abs(
                            column -
                            candidate.CenterX)),
                Darkness =
                    DarkRailFraction(
                        image,
                        candidate),
            })
            .Where(candidate =>
                candidate.Distance <= 15)
            .GroupBy(candidate =>
                candidate.Column)
            .Select(group => group
                .OrderByDescending(candidate =>
                    candidate.Darkness)
                .ThenBy(candidate =>
                    candidate.Distance)
                .First()
                .Component)
            .OrderBy(candidate =>
                candidate.CenterX)
            .ToArray();

    public static (int X, int Y)?
        ConfirmationAction(
        ImageFrame image)
    {
        Component? component =
            Components(image, IsClaimGreen)
                .Where(value =>
                    value.Count >= 1400 &&
                    value.Width is >= 95 and <= 130 &&
                    value.Height is >= 18 and <= 30 &&
                    value.CenterX is >= 325 and <= 365 &&
                    value.CenterY is >= 325 and <= 350)
                .OrderByDescending(value =>
                    value.Count)
                .Cast<Component?>()
                .FirstOrDefault();
        return component is Component match
            ? (match.CenterX, match.CenterY)
            : null;
    }

    private static double DarkRailFraction(
        ImageFrame image,
        Component component)
    {
        int left = Math.Max(
            BoardSearch.X,
            component.Left - 48);
        int top = Math.Max(
            BoardSearch.Y,
            component.Top - 2);
        int right = Math.Max(
            left + 1,
            component.Left - 3);
        int bottom = Math.Min(
            BoardSearch.Bottom,
            component.Top +
            component.Height + 2);
        int dark = 0;
        int total = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                int luminance =
                    (image.Pixels[pixel] +
                     image.Pixels[pixel + 1] +
                     image.Pixels[pixel + 2]) / 3;
                if (luminance < 75)
                {
                    dark++;
                }
                total++;
            }
        }
        return (double)dark /
            Math.Max(1, total);
    }

    private static IReadOnlyList<Component>
        Components(
        ImageFrame image,
        Func<byte, byte, byte, bool> predicate)
    {
        int width = BoardSearch.Width;
        int height = BoardSearch.Height;
        bool[] mask = new bool[width * height];
        bool[] visited =
            new bool[mask.Length];
        int[] queue = new int[mask.Length];
        for (int localY = 0;
             localY < height;
             localY++)
        {
            int y = BoardSearch.Y + localY;
            for (int localX = 0;
                 localX < width;
                 localX++)
            {
                int x = BoardSearch.X + localX;
                int pixel =
                    (y * image.Width + x) * 3;
                mask[localY * width + localX] =
                    predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]);
            }
        }

        List<Component> result = [];
        for (int start = 0;
             start < mask.Length;
             start++)
        {
            if (!mask[start] ||
                visited[start])
            {
                continue;
            }
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
                minimumX = Math.Min(
                    minimumX,
                    x);
                minimumY = Math.Min(
                    minimumY,
                    y);
                maximumX = Math.Max(
                    maximumX,
                    x);
                maximumY = Math.Max(
                    maximumY,
                    y);
                Enqueue(x - 1, y);
                Enqueue(x + 1, y);
                Enqueue(x, y - 1);
                Enqueue(x, y + 1);

                void Enqueue(int nextX, int nextY)
                {
                    if (nextX < 0 ||
                        nextY < 0 ||
                        nextX >= width ||
                        nextY >= height)
                    {
                        return;
                    }
                    int index =
                        nextY * width + nextX;
                    if (!mask[index] ||
                        visited[index])
                    {
                        return;
                    }
                    visited[index] = true;
                    queue[tail++] = index;
                }
            }
            result.Add(
                new Component(
                    count,
                    BoardSearch.X + minimumX,
                    BoardSearch.Y + minimumY,
                    maximumX - minimumX + 1,
                    maximumY - minimumY + 1));
        }
        return result;
    }

    private static bool IsRerollYellow(
        byte red,
        byte green,
        byte blue) =>
        red > 150 &&
        green > 80 &&
        green < 230 &&
        blue < 100;

    private static bool IsClaimGreen(
        byte red,
        byte green,
        byte blue) =>
        green > 90 &&
        green > red * 1.15 &&
        green > blue * 1.25;

    private readonly record struct Component(
        int Count,
        int Left,
        int Top,
        int Width,
        int Height)
    {
        public int CenterX =>
            Left + Width / 2;

        public int CenterY =>
            Top + Height / 2;
    }
}
