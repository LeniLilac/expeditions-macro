using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class ExpeditionSelectorDetector
{
    private static readonly ScreenRegion[] MapCards =
    [
        new(7, 87, 151, 72),
        new(7, 164, 151, 69),
        new(7, 239, 151, 68),
    ];

    private static readonly ScreenRegion DifficultyColor =
        new(218, 430, 74, 37);

    public static double Score(
        ImageFrame image) =>
        SelectedMap(image) is not null &&
        SelectStageAction(image) is not null
            ? 0.96
            : 0;

    public static int? SelectedMap(
        ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            SelectStageAction(image) is null)
        {
            return null;
        }

        (int Pixels, int Map)[] ranked =
            MapCards
                .Select((region, index) =>
                    (CountCyanPerimeter(image, region),
                     Map: index + 1))
                .OrderByDescending(value => value.Item1)
                .ToArray();
        return ranked[0].Pixels >= 100 &&
            ranked[0].Pixels - ranked[1].Pixels >= 70
                ? ranked[0].Map
                : null;
    }

    public static int? SelectedDifficulty(
        ImageFrame image)
    {
        if (SelectedMap(image) is null)
        {
            return null;
        }

        (int Count, int Difficulty)[] ranked =
        [
            (CountPixels(image, IsDifficultyOne), 1),
            (CountPixels(image, IsDifficultyTwo), 2),
            (CountPixels(image, IsDifficultyThree), 3),
        ];
        (int Count, int Difficulty)[] ordered =
            ranked
                .OrderByDescending(value => value.Count)
                .ToArray();
        return ordered[0].Count >= 250 &&
            ordered[0].Count - ordered[1].Count >= 150
                ? ordered[0].Difficulty
                : null;
    }

    public static (int X, int Y)? ActionFor(
        ImageFrame image,
        string state)
    {
        if (Score(image) <= 0)
        {
            return null;
        }

        return state switch
        {
            "map_1" => (82, 123),
            "map_2" => (82, 199),
            "map_3" => (82, 273),
            "difficulty_minus" => (197, 448),
            "difficulty_plus" => (310, 448),
            "select_stage" or "map_select" =>
                SelectStageAction(image),
            _ => null,
        };
    }

    private static (int X, int Y)?
        SelectStageAction(ImageFrame image) =>
        ActionButtonDetector.ActionFor(
            image,
            "expedition_current_select_stage");

    private static int CountCyanPerimeter(
        ImageFrame image,
        ScreenRegion region)
    {
        int count = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                if (x >= region.X + 4 &&
                    x < region.Right - 4 &&
                    y >= region.Y + 4 &&
                    y < region.Bottom - 4)
                {
                    continue;
                }

                int pixel =
                    (y * image.Width + x) * 3;
                byte red = image.Pixels[pixel];
                byte green = image.Pixels[pixel + 1];
                byte blue = image.Pixels[pixel + 2];
                if (green >= 70 &&
                    green * 20 >= red * 23 &&
                    blue * 4 >= red * 3)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static int CountPixels(
        ImageFrame image,
        Func<byte, byte, byte, bool> predicate)
    {
        int count = 0;
        for (int y = DifficultyColor.Y;
             y < DifficultyColor.Bottom;
             y++)
        {
            for (int x = DifficultyColor.X;
                 x < DifficultyColor.Right;
                 x++)
            {
                int pixel =
                    (y * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static bool IsDifficultyOne(
        byte red,
        byte green,
        byte blue) =>
        green >= 65 &&
        green - red >= 15 &&
        green - blue >= 10;

    private static bool IsDifficultyTwo(
        byte red,
        byte green,
        byte blue) =>
        red >= 65 &&
        red - green >= 15 &&
        red - blue >= 10;

    private static bool IsDifficultyThree(
        byte red,
        byte green,
        byte blue) =>
        blue >= 55 &&
        red >= 35 &&
        blue - green >= 15 &&
        red - green >= 10;
}
